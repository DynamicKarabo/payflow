# PayFlow — Refunds & Settlement Batching

---

## Refunds

### Rules

1. Refunds can only be issued against payments in `Settled` status.
2. The sum of all refund amounts for a payment cannot exceed the original `Amount`.
3. Multiple partial refunds are allowed until the refundable balance is exhausted.
4. A refund cannot be reversed once submitted.

### Refundable Amount Calculation

```csharp
// Domain — Payment aggregate

public Money RefundableAmount =>
    Amount.Subtract(
        _refunds
            .Where(r => r.Status != RefundStatus.Failed)
            .Aggregate(Money.Zero(Currency), (acc, r) => acc.Add(r.Amount)));
```

`Failed` refunds do not reduce the refundable balance — the amount is considered unspent.

### Refund Flow

```
POST /v1/payments/{id}/refund
Idempotency-Key: refund_9f3a2b

{
  "amount": 1000,        // partial refund in minor units
  "reason": "customer_request"
}

[1] Load Payment (tenant-scoped)
[2] Assert Status == Settled → else 409
[3] Assert amount <= RefundableAmount → else 422 insufficient_refundable_amount
[4] payment.Refund(amount, reason) → creates Refund child entity (Status=Pending)
[5] Persist
[6] Enqueue RefundGatewayJob (Hangfire)
[7] Publish RefundCreated domain event → Service Bus
[8] Return 201 with Refund response
```

Refund processing is asynchronous — the `Refund` record is created immediately but gateway submission happens in a background job. This prevents the API from being blocked by gateway latency.

### RefundGatewayJob

```csharp
public sealed class RefundGatewayJob(
    IPaymentRepository payments,
    IPaymentGatewayAdapter gateway,
    IUnitOfWork uow)
{
    public async Task ExecuteAsync(RefundId refundId, CancellationToken ct)
    {
        var payment = await payments.GetByRefundIdAsync(refundId, ct);
        var refund = payment.Refunds.Single(r => r.Id == refundId);

        var result = await gateway.RefundAsync(
            payment.GatewayReference!, refund.Amount, ct);

        if (result.Succeeded)
            refund.MarkSucceeded(result.GatewayReference);
        else
            refund.MarkFailed(result.FailureReason);

        await uow.CommitAsync(ct);   // dispatches RefundSucceeded/RefundFailed domain event
    }
}
```

Hangfire automatic retry (3 attempts with backoff) handles transient gateway failures.

### Refund API

```
POST /v1/payments/{id}/refund     → Create partial or full refund
GET  /v1/payments/{id}/refunds    → List refunds for a payment
GET  /v1/refunds/{id}             → Get individual refund
```

---

## Settlement Batching

### Purpose

Settlement aggregates `Captured` payments into a settlement batch, transitions them to `Settled`, and produces a settlement summary per tenant. This mirrors how real-world payment processors settle funds.

### Schedule

A Hangfire `RecurringJob` runs nightly at **00:30 UTC**. The time is offset from midnight to avoid contention with other scheduled tasks.

```csharp
// Registered at startup
RecurringJob.AddOrUpdate<SettlementBatchJob>(
    "settlement-nightly",
    j => j.ExecuteAsync(CancellationToken.None),
    Cron.Daily(hour: 0, minute: 30));
```

### Settlement Job Flow

```
[1] AdminDbContext.AllPayments
    WHERE Status = Captured
      AND Mode = Live           ← test payments never settled
      AND CreatedAt < cutoff    ← configurable, default T-1 day
    GROUP BY TenantId

[2] For each tenant batch:
    a. Create SettlementBatch record
    b. For each payment:
         payment.Settle()
         link to batch
    c. Sum gross / net amounts
    d. Commit batch

[3] Publish SettlementCompleted event per tenant → Service Bus
    (triggers webhook: settlement.completed)

[4] Log batch summary
```

### Settlement Cutoff

The cutoff prevents same-day captures from being settled immediately, giving time for disputes/cancellations. Default is payments captured before midnight of the previous day. Configurable per tenant via `SettlementConfig.CutoffHours`.

### SettlementBatch Entity

```csharp
public sealed class SettlementBatch : AggregateRoot
{
    public SettlementBatchId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public DateOnly SettlementDate { get; private set; }
    public Money GrossAmount { get; private set; }
    public Money FeeAmount { get; private set; }
    public Money NetAmount { get; private set; }
    public int PaymentCount { get; private set; }
    public SettlementStatus Status { get; private set; }  // Pending | Completed | Failed
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private readonly List<PaymentId> _paymentIds = new();
    public IReadOnlyList<PaymentId> PaymentIds => _paymentIds.AsReadOnly();
}
```

### Settlement API

```
GET /v1/settlements
    ?from=2026-03-01&to=2026-03-28

GET /v1/settlements/{id}

GET /v1/settlements/{id}/payments    → paginated list of payments in batch
```

---

## Fee Calculation

Fees are calculated at settlement time based on tenant's `SettlementConfig`:

```csharp
public sealed record SettlementConfig(
    decimal PercentageFee,   // e.g. 0.014 = 1.4%
    Money FixedFee,          // e.g. 0.20 GBP per transaction
    Currency SettlementCurrency
);

// Applied per payment:
// fee = (amount * percentageFee) + fixedFee
// net = amount - fee
```

Fee calculation is deterministic and versioned — the `SettlementConfig` snapshot used is recorded on the batch for auditability.

---

## Idempotency for Settlement

The nightly job is idempotent: before creating a new `SettlementBatch`, it checks whether a batch already exists for the tenant and date. If it does (e.g. job ran twice due to Hangfire restart), it skips without duplicating.

```csharp
var existing = await _repo.FindBatchAsync(tenantId, settlementDate, ct);
if (existing is not null)
{
    _logger.LogWarning("Settlement batch already exists for {TenantId} {Date}", ...);
    return;
}
```

---

## Reconciliation

Each settlement batch produces a reconciliation record containing:

- Batch ID
- Settlement date
- List of `PaymentId` values included
- Gross, fee, net amounts
- Per-currency breakdown (if tenant processes multiple currencies)

This record is queryable via `/v1/settlements/{id}` and is also delivered as a webhook `settlement.completed` event with the summary in the payload.
