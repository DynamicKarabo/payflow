# PayFlow — Payment Processing

## Payment Intent Flow

A payment moves through a well-defined sequence of operations. The API layer handles command dispatch; the domain enforces state rules; the infrastructure layer handles persistence, idempotency, and event publishing.

```
POST /v1/payments
        │
        ▼
[1] Resolve TenantId + Mode from API key
        │
        ▼
[2] Idempotency check (Redis SET NX)
    ├── Key exists → return cached response (200, no duplicate charge)
    └── Key absent → continue
        │
        ▼
[3] Validate request (FluentValidation)
        │
        ▼
[4] Create Payment aggregate (Domain)
    Status = Created, Mode = from API key
        │
        ▼
[5] Persist to SQL Server (EF Core)
        │
        ▼
[6] Call Gateway Adapter → Authorise
    ├── Success → Payment.Authorise(gatewayRef), Status = Authorised
    └── Failure → Payment.Fail(reason), Status = Failed
        │
        ▼
[7] If auto_capture = true → Payment.Capture(), Status = Captured
        │
        ▼
[8] Save state changes
        │
        ▼
[9] Dispatch domain events → Azure Service Bus
        │
        ▼
[10] Store response in Redis idempotency cache (TTL 24h)
        │
        ▼
[11] Return PaymentResponse
```

---

## Idempotency

### Problem

Payment APIs are called over unreliable networks. A client timeout does not mean the server failed — it may mean the response was lost. Without idempotency, a retry creates a duplicate charge.

### Mechanism

Redis `SET NX` (Set if Not eXists) is used as an atomic lock-and-cache per payment intent.

**Key format:**
```
idempotency:{tenantId}:{idempotencyKey}
```

Scoping by `tenantId` prevents cross-tenant key collision even if clients reuse the same key string.

**Flow:**

```csharp
// Application/Idempotency/IdempotencyService.cs

public async Task<IdempotencyResult> CheckOrReserveAsync(
    TenantId tenantId,
    IdempotencyKey key,
    CancellationToken ct)
{
    var redisKey = $"idempotency:{tenantId.Value}:{key.Value}";

    // Try to claim the key — NX means only succeeds if absent
    var claimed = await _redis.StringSetAsync(
        redisKey,
        PROCESSING_SENTINEL,
        expiry: TimeSpan.FromHours(24),
        when: When.NotExists);

    if (!claimed)
    {
        // Key exists — fetch cached response
        var cached = await _redis.StringGetAsync(redisKey);
        if (cached == PROCESSING_SENTINEL)
            return IdempotencyResult.InFlight();  // 409 — duplicate in-flight

        return IdempotencyResult.Duplicate(
            JsonSerializer.Deserialize<PaymentResponse>(cached!));
    }

    return IdempotencyResult.NewRequest(redisKey);
}

public async Task CommitAsync(string redisKey, PaymentResponse response)
{
    // Replace sentinel with actual response
    await _redis.StringSetAsync(
        redisKey,
        JsonSerializer.Serialize(response),
        expiry: TimeSpan.FromHours(24));
}
```

**Edge cases:**

| Scenario | Behaviour |
|---|---|
| First request | `SET NX` succeeds → process normally |
| Retry after success | Redis has serialised response → return 200 with cached body |
| Retry while in-flight | Redis has sentinel → return 409 `payment_in_flight` |
| Redis unavailable | Fall through — process without idempotency guarantee; log warning |
| TTL expired (>24h) | Treat as new request — client must handle |

### Idempotency Key Rules

- Supplied by caller in `Idempotency-Key` request header.
- Maximum 64 characters, alphanumeric + hyphens.
- Required for `POST /v1/payments` and `POST /v1/payments/{id}/refund`.
- Optional for all other endpoints.
- Mismatch of request body against cached key body → 422 `idempotency_conflict`.

---

## API Endpoints

### Create Payment

```
POST /v1/payments
Authorization: Bearer pk_live_...
Idempotency-Key: order_9f3a2b

{
  "amount": 4999,          // minor units (pence, cents)
  "currency": "GBP",
  "customer_id": "cus_...",
  "payment_method": {
    "type": "card",
    "token": "tok_..."     // tokenised by front-end SDK; never raw PAN
  },
  "auto_capture": true,
  "metadata": {
    "order_id": "ord_8823"
  }
}

→ 201 Created
{
  "id": "pay_...",
  "status": "captured",
  "amount": 4999,
  "currency": "GBP",
  "mode": "live",
  "gateway_reference": "ch_...",
  "created_at": "2026-03-28T10:00:00Z"
}
```

### Get Payment

```
GET /v1/payments/{id}

→ 200 OK  (PaymentResponse)
→ 404     (not found or cross-tenant — same response to prevent enumeration)
```

### List Payments

```
GET /v1/payments?status=captured&from=2026-03-01&to=2026-03-28&limit=20&cursor=...

→ 200 OK  { "data": [...], "next_cursor": "...", "has_more": true }
```

### Capture Payment

```
POST /v1/payments/{id}/capture

→ 200 OK  (PaymentResponse with status=captured)
→ 409     (payment not in authorised state)
```

### Cancel Payment

```
POST /v1/payments/{id}/cancel

→ 200 OK  (PaymentResponse with status=cancelled)
→ 409     (payment already captured or settled)
```

---

## Gateway Adapter

The gateway is abstracted behind `IPaymentGatewayAdapter`. The real adapter calls the upstream processor (e.g. Stripe, Adyen). The test-mode adapter is a stub.

```csharp
public interface IPaymentGatewayAdapter
{
    Task<GatewayAuthoriseResult> AuthoriseAsync(
        AuthoriseRequest request, CancellationToken ct);

    Task<GatewayCaptureResult> CaptureAsync(
        string gatewayReference, Money amount, CancellationToken ct);

    Task<GatewayRefundResult> RefundAsync(
        string gatewayReference, Money amount, CancellationToken ct);
}

// Registered based on PaymentMode:
//   Live  → RealGatewayAdapter
//   Test  → StubGatewayAdapter (returns configurable success/failure)
```

**Resilience policy (Polly):**
- 3 retries with 200ms, 500ms, 1s backoff (jittered).
- Circuit breaker: opens after 5 consecutive failures; half-open after 30s.
- Timeout: 10s per attempt.

Retries are safe because gateway calls use idempotency keys passed to the upstream API.

---

## Command / Handler Pattern

```
CreatePaymentCommand
    → CreatePaymentCommandHandler
        → IdempotencyService.CheckOrReserveAsync()
        → Payment.Create(...)             [domain factory]
        → IPaymentRepository.AddAsync()
        → IGatewayAdapter.AuthoriseAsync()
        → payment.Authorise(ref)          [domain transition]
        → IUnitOfWork.CommitAsync()       [saves + dispatches domain events]
        → IdempotencyService.CommitAsync()
        → returns PaymentResponse
```

All commands are dispatched via MediatR. Pipeline behaviours handle:
- Validation (FluentValidation)
- Logging
- Unhandled exception mapping to `ProblemDetails`

---

## Error Responses

All errors follow RFC 9457 `application/problem+json`.

```json
{
  "type": "https://payflow.io/errors/insufficient-funds",
  "title": "Payment declined",
  "status": 402,
  "detail": "Card declined by issuer — insufficient funds.",
  "payment_id": "pay_abc123",
  "gateway_code": "insufficient_funds"
}
```

| Scenario | HTTP Status | Error Type |
|---|---|---|
| Validation failure | 422 | `validation_error` |
| Duplicate in-flight | 409 | `payment_in_flight` |
| Idempotency body mismatch | 422 | `idempotency_conflict` |
| Card declined | 402 | `payment_declined` |
| Invalid state transition | 409 | `invalid_payment_state` |
| Not found / cross-tenant | 404 | `not_found` |
| Gateway timeout | 502 | `gateway_unavailable` |
| Rate limit exceeded | 429 | `rate_limit_exceeded` |
