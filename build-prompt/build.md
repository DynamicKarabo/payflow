### Prompt 1: The Domain Layer (Entities & State Machine)
**Copy and paste this prompt:**
> **Context:** I am building "PayFlow", a multi-tenant payment processing platform in C# .NET 8 using Clean Architecture. The domain layer must have zero infrastructure dependencies.
> 
> **Task:** Generate the core Domain entities, value objects, and domain events.
> 1. Create a `Payment` Aggregate Root. It must have an immutable `TenantId` and `Mode` (Live or Test) set at creation. It needs properties for `Amount` (must be positive) and a collection of child `Refund` entities. 
> 2. Implement a strict State Machine within the `Payment` aggregate. The allowed states are: Created, Authorised, Captured, Settled, Failed, and Cancelled. 
> 3. Enforce these specific transitions using methods: Created → Authorised, Created → Fail, Created → Cancel, Authorised → Capture, Authorised → Fail, Authorised → Cancel, Captured → Settle, Captured → Fail, Settled → Refund (adds a refund child entity). Any invalid transition must throw an `InvalidPaymentTransitionException`.
> 4. Ensure that the total refunded amount across all `Refund` records cannot exceed the payment's `Amount`.
> 5. Have the aggregate emit domain events (implementing `IDomainEvent`) on every state transition.
> 6. Create the `Tenant`, `ApiKey`, and `Refund` entities.

### Prompt 2: Multi-Tenancy & Persistence (EF Core)
**Copy and paste this prompt:**
> **Context:** PayFlow uses a shared database/shared schema multi-tenancy model backed by SQL Server 2022 and EF Core 8.
> 
> **Task:** Generate the Infrastructure persistence layer focusing on strict multi-tenant data isolation and optimistic concurrency.
> 1. Create a scoped `ITenantContext` interface that holds the current `TenantId`.
> 2. Set up the primary `DbContext`. In `OnModelCreating`, configure a Global Query Filter for every entity with a `TenantId` so that it transparently appends `WHERE TenantId = @currentTenantId` using the injected `ITenantContext`.
> 3. Create a derived `AdminDbContext` for background jobs that explicitly calls `IgnoreQueryFilters()` at the repository level to bypass the global filters safely.
> 4. Map all tables to a custom `payflow` schema.
> 5. Configure optimistic concurrency on the `Payment` entity using a SQL Server `rowversion` column. When a `DbUpdateConcurrencyException` is caught, map it to a custom exception representing a concurrency conflict.

### Prompt 3: Application Layer & Payment Processing (MediatR & Redis)
**Copy and paste this prompt:**
> **Context:** The application layer uses MediatR for command dispatch, FluentValidation, and Redis for idempotency and distributed locking.
> 
> **Task:** Generate the MediatR command handlers and the Redis idempotency service for the payment flow.
> 1. Create a MediatR command and handler for "Create Payment".
> 2. Implement an Idempotency service using Redis (via `StackExchange.Redis`). Use the `SET NX` (Set if Not eXists) command to create an atomic lock and cache per payment intent. 
> 3. The Redis key format must be `payflow:idempotency:{tenantId}:{key}` with a 24-hour TTL.
> 4. Handle edge cases: If `SET NX` succeeds, proceed. If a retry happens after success, return the cached serialised response. If a retry happens while in-flight, throw a specific exception for "payment_in_flight" (409). If Redis is unavailable, skip the check but log a warning (trade safety for availability).
> 5. Create an `IPaymentGatewayAdapter` interface. Implement a Polly resilience policy for the real adapter: 3 retries with jittered backoff (200ms, 500ms, 1s), a circuit breaker (opens after 5 failures, half-open after 30s), and a 10s timeout per attempt.

### Prompt 4: Webhooks & Background Jobs (Hangfire & Service Bus)
**Copy and paste this prompt:**
> **Context:** PayFlow decouples domain events via Azure Service Bus and uses Hangfire (backed by SQL Server in a `HangfireSchema`) for reliable async background processing.
> 
> **Task:** Implement the webhook delivery and settlement background jobs.
> 1. **Webhooks:** Create a Hangfire `WebhookDeliveryJob`. It must sign the JSON payload using HMAC-SHA256 with the tenant's secret. The signature must be added to a `PayFlow-Signature` header in the format `t={timestamp},v1={hex_signature}`.
> 2. Configure a Hangfire retry policy with fixed-delay exponential backoff with jitter (Immediate, 30s, 5m, 30m, 2h, 5h, 24h). After 7 attempts, mark it as Dead.
> 3. **Settlement:** Create a Hangfire Recurring Job (`SettlementBatchJob`) that runs nightly at 00:30 UTC. 
> 4. Add a Redis distributed lock for the settlement job using the key `payflow:lock:settle:{tenantId}:{date}` with a 5-minute TTL to prevent double-settlement if two workers pick it up. The job should aggregate captured payments, transition them to Settled, and generate a `SettlementBatch` entity.

### Prompt 5: The API Layer & Error Handling
**Copy and paste this prompt:**
> **Context:** The outermost layer is built with ASP.NET Core Minimal APIs.
> 
> **Task:** Generate the API endpoints, authentication middleware, and error handling.
> 1. Create an API Key authentication middleware. It should extract the key, validate the format (e.g., `pk_live_` or `pk_test_` followed by base58), hash it using bcrypt (cost=12), and verify it against the database. 
> 2. Once verified, populate the scoped `ITenantContext` with the tenant's ID and Mode for the lifetime of the request.
> 3. Implement global error handling that maps domain exceptions to RFC 9457 `application/problem+json` standard responses. 
> 4. Map the following specifically: Validation failure → 422 (`validation_error`), duplicate in-flight → 409 (`payment_in_flight`), concurrency/invalid state → 409 (`invalid_payment_state`), and missing tenant data → 404 (`not_found`).
> 5. Scaffold out the Minimal API endpoints for POST `/v1/payments`, GET `/v1/payments/{id}`, and POST `/v1/payments/{id}/refund`. Require the `Idempotency-Key` header on POST requests (max 64 chars, alphanumeric + hyphens).

### Why this structure works for your design:
*   **Separation of Concerns:** It strictly adheres to your requirement that the domain has zero infrastructure dependencies.
*   **Safety First:** It prioritizes your primary mechanisms for safety: EF Core global query filters for cross-tenant isolation, Redis `SET NX` for duplicate charge prevention, and optimistic concurrency (`rowversion`) to prevent lost updates.
*   **Auditability & Reliability:** It incorporates the strict state machine, HMAC payload signing, and Hangfire exponential backoff schedules precisely as documented in your specs.