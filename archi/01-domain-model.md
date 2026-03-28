# PayFlow — Domain Model

## Design Principles

The domain layer has zero infrastructure dependencies. It contains entities, value objects, domain events, and the payment state machine. All state transitions are enforced here — no caller can move a payment to an invalid state.

---

## Aggregates

### `Payment` (Aggregate Root)

The central aggregate. Owns its state machine and emits domain events on every transition.

```csharp
public sealed class Payment : AggregateRoot
{
    public PaymentId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public IdempotencyKey IdempotencyKey { get; private set; }
    public Money Amount { get; private set; }
    public Currency Currency { get; private set; }
    public PaymentStatus Status { get; private set; }
    public PaymentMode Mode { get; private set; }          // Live | Test
    public CustomerId CustomerId { get; private set; }
    public PaymentMethodSnapshot PaymentMethod { get; private set; }
    public string? GatewayReference { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<Refund> _refunds = new();
    public IReadOnlyList<Refund> Refunds => _refunds.AsReadOnly();

    // State machine transitions — all throw DomainException on invalid transition
    public void Authorise(string gatewayReference) { ... }
    public void Capture() { ... }
    public void Settle() { ... }
    public void Fail(string reason) { ... }
    public void Cancel() { ... }
    public Refund Refund(Money amount, string reason) { ... }
}
```

**Invariants:**
- `TenantId` is immutable after creation.
- `Amount` must be positive and within tenant's configured limit.
- Total refunded amount across all `Refund` records cannot exceed `Amount`.
- `Mode` is immutable — a test payment can never become a live payment.

---

### `Refund` (Entity, owned by `Payment`)

```csharp
public sealed class Refund : Entity
{
    public RefundId Id { get; private set; }
    public PaymentId PaymentId { get; private set; }
    public TenantId TenantId { get; private set; }
    public Money Amount { get; private set; }
    public RefundStatus Status { get; private set; }   // Pending | Succeeded | Failed
    public string Reason { get; private set; }
    public string? GatewayReference { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
```

---

### `Tenant` (Aggregate Root)

```csharp
public sealed class Tenant : AggregateRoot
{
    public TenantId Id { get; private set; }
    public string Name { get; private set; }
    public TenantStatus Status { get; private set; }   // Active | Suspended | Closed
    public WebhookConfig? WebhookConfig { get; private set; }
    public SettlementConfig SettlementConfig { get; private set; }
    public Money DailyLimit { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
```

---

### `ApiKey` (Entity)

```csharp
public sealed class ApiKey : Entity
{
    public ApiKeyId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string KeyPrefix { get; private set; }      // e.g. "pk_live_" | "pk_test_"
    public string HashedSecret { get; private set; }   // bcrypt hash
    public PaymentMode Mode { get; private set; }
    public ApiKeyStatus Status { get; private set; }   // Active | Revoked
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
```

---

## Value Objects

```csharp
// Strongly-typed IDs (record structs — no implicit GUID leakage)
public readonly record struct PaymentId(Guid Value);
public readonly record struct TenantId(Guid Value);
public readonly record struct RefundId(Guid Value);
public readonly record struct CustomerId(Guid Value);

// Money — immutable, currency-aware
public readonly record struct Money(decimal Amount, Currency Currency)
{
    public Money Add(Money other) { /* currency guard */ }
    public Money Subtract(Money other) { /* negativity guard */ }
    public bool IsPositive => Amount > 0;
}

// Currency — ISO 4217
public readonly record struct Currency(string Code)  // "GBP", "USD", "ZAR"
{
    public int DecimalPlaces { get; }   // 2 for most; 0 for JPY etc.
}

// Idempotency key — caller-supplied, max 64 chars, scoped to tenant
public readonly record struct IdempotencyKey(string Value)
{
    // Validated in constructor
}

// Payment method snapshot — stored at payment creation; never mutated
public sealed record PaymentMethodSnapshot(
    string Type,           // "card" | "bank_transfer"
    string Last4,
    string? Brand,         // "visa" | "mastercard"
    string? ExpiryMonth,
    string? ExpiryYear,
    string? BankName
);

// Webhook config — owned by tenant
public sealed record WebhookConfig(
    string EndpointUrl,
    string HmacSecret,     // encrypted at rest
    IReadOnlyList<string> EventSubscriptions
);
```

---

## Payment State Machine

```
                     ┌─────────┐
                     │ Created │
                     └────┬────┘
                          │ Authorise(gatewayRef)
                          ▼
                    ┌────────────┐
                    │ Authorised │◄──────────────────┐
                    └─────┬──────┘                   │
                          │ Capture()                │
                          ▼                          │ (auto-capture = false
                    ┌──────────┐                     │  allows window here)
                    │ Captured │
                    └─────┬────┘
                          │ Settle()   [batch job only]
                          ▼
                    ┌──────────┐
                    │ Settled  │
                    └──────────┘

  From Created | Authorised | Captured:
        │
        ├──► Fail(reason)   → ┌────────┐
        │                     │ Failed │
        └──► Cancel()       → └────────┘
                               ┌──────────┐
                               │Cancelled │
                               └──────────┘
```

**Allowed Transitions Table:**

| From | Event | To |
|---|---|---|
| `Created` | `Authorise` | `Authorised` |
| `Created` | `Fail` | `Failed` |
| `Created` | `Cancel` | `Cancelled` |
| `Authorised` | `Capture` | `Captured` |
| `Authorised` | `Fail` | `Failed` |
| `Authorised` | `Cancel` | `Cancelled` |
| `Captured` | `Settle` | `Settled` |
| `Captured` | `Fail` | `Failed` |
| `Settled` | `Refund` | `Settled` *(refund child entity added)* |

Any other transition throws `InvalidPaymentTransitionException`.

---

## Domain Events

All events implement `IDomainEvent` and are collected on the aggregate, then dispatched post-commit by `DomainEventDispatcher`.

```csharp
public sealed record PaymentCreated(
    PaymentId PaymentId, TenantId TenantId, Money Amount,
    PaymentMode Mode, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PaymentAuthorised(
    PaymentId PaymentId, TenantId TenantId, string GatewayReference,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PaymentCaptured(
    PaymentId PaymentId, TenantId TenantId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PaymentSettled(
    PaymentId PaymentId, TenantId TenantId, Money Amount,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PaymentFailed(
    PaymentId PaymentId, TenantId TenantId, string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PaymentCancelled(
    PaymentId PaymentId, TenantId TenantId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record RefundCreated(
    RefundId RefundId, PaymentId PaymentId, TenantId TenantId,
    Money Amount, DateTimeOffset OccurredAt) : IDomainEvent;
```

---

## Domain Exceptions

```csharp
public sealed class InvalidPaymentTransitionException : DomainException
{
    public PaymentStatus From { get; }
    public string AttemptedEvent { get; }
}

public sealed class InsufficientRefundableAmountException : DomainException
{
    public Money Requested { get; }
    public Money Available { get; }
}

public sealed class TenantSuspendedException : DomainException;

public sealed class PaymentModeMismatchException : DomainException
{
    public PaymentMode ApiKeyMode { get; }
    public PaymentMode PaymentMode { get; }
}
```

---

## Enumerations

```csharp
public enum PaymentStatus
{
    Created,
    Authorised,
    Captured,
    Settled,
    Failed,
    Cancelled
}

public enum PaymentMode { Live, Test }

public enum RefundStatus { Pending, Succeeded, Failed }

public enum TenantStatus { Active, Suspended, Closed }

public enum ApiKeyStatus { Active, Revoked }
```
