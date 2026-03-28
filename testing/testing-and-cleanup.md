### Prompt 1: Domain Logic & State Machine Tests
**Copy and paste this prompt:**
> **Context:** We are testing the core Domain layer of PayFlow, specifically the `Payment` aggregate and its strict state machine.
> 
> **Task:** Generate a suite of unit tests for the `Payment` and `Refund` entities.
> 1. Test that `TenantId` and `Mode` (Live/Test) are immutable once set on creation.
> 2. Test the allowed state transitions: Created → Authorised, Created → Fail, Created → Cancel, Authorised → Capture, Authorised → Fail, Authorised → Cancel, Captured → Settle, Captured → Fail, Settled → Refund.
> 3. Assert that any invalid state transition throws an `InvalidPaymentTransitionException`.
> 4. Test the refund calculation logic: Refunds can only be added when the payment is `Settled`, and the total sum of `Refund` amounts cannot exceed the original payment `Amount`. Ensure failed refunds do not reduce the refundable balance.
> 5. Verify that appropriate domain events (implementing `IDomainEvent`) are added to the aggregate's event collection on every successful state transition.

### Prompt 2: Integration Testing for Multi-Tenancy & Concurrency
**Copy and paste this prompt:**
> **Context:** PayFlow relies heavily on EF Core global query filters for multi-tenant isolation and rowversion for optimistic concurrency.
> 
> **Task:** Generate integration tests that validate database security and concurrency.
> 1. **Cross-Tenant Isolation:** Create a test where two separate tenants exist. Authenticate as Tenant A and attempt to GET/Capture/Refund a payment owned by Tenant B. Assert that the API returns a 404 Not Found or 403 Forbidden.
> 2. Validate that the API request body `TenantId` is completely ignored, and the system strictly uses the `ITenantContext` resolved from the API key.
> 3. **Concurrency:** Simulate a race condition where two concurrent threads try to transition the same `Payment` from Authorised to Captured. The first should succeed, and the second should trigger a `DbUpdateConcurrencyException` due to the `rowversion` mismatch. Assert that the API layer catches this and returns a 409 Conflict with the error type `payment_concurrency_conflict`.

### Prompt 3: Idempotency & Gateway Resilience Tests
**Copy and paste this prompt:**
> **Context:** PayFlow uses Redis `SET NX` for idempotency and Polly for gateway resilience.
> 
> **Task:** Generate tests to verify the idempotency edge cases and the gateway adapter retry policy.
> 1. Mock the Redis service and test the following idempotency scenarios based on the `Idempotency-Key` header:
>    - First request: Process normally.
>    - Retry after success: Assert the API returns a 200 OK with the cached serialised body.
>    - Retry while in-flight: Assert it returns a 409 `payment_in_flight`.
>    - Request body mismatch against cached key body: Assert it returns a 422 `idempotency_conflict`.
>    - Redis unavailable: Assert the system falls through, processes the payment without the idempotency guarantee, and logs a warning.
> 2. Test the `IPaymentGatewayAdapter` Polly policy: Mock the upstream API to fail continuously. Assert that the adapter retries exactly 3 times (with jittered backoff), that the circuit breaker opens after 5 consecutive failures, and that a 10s timeout is enforced per attempt.

### Prompt 4: Security Audit & Webhook Validation
**Copy and paste this prompt:**
> **Context:** PayFlow must strictly validate webhook configuration and payloads.
> 
> **Task:** Generate tests and cleanup code for webhook delivery security.
> 1. Implement a validation rule for webhook registration: Plaintext HTTP endpoints must be rejected; TLS/HTTPS is strictly enforced.
> 2. Ensure that event payloads are scrubbed and contain no full PAN or CVV data (only tokenised/masked information).
> 3. Generate a receiver-side verification test for the HMAC-SHA256 payload signing. Verify that the `PayFlow-Signature` header (format `t={timestamp},v1={hex_signature}`) is parsed correctly, and that the signature is compared using `CryptographicOperations.FixedTimeEquals` to prevent timing attacks.
> 4. Test the timestamp check: Reject the webhook event if `|now - t| > 300 seconds` to prevent replay attacks.

### Prompt 5: Infrastructure Cleanup & Startup Orchestration
**Copy and paste this prompt:**
> **Context:** The final cleanup phase ensures database migrations run reliably on startup and internal background jobs are configured correctly.
> 
> **Task:** Implement the application startup orchestration and infrastructure cleanup.
> 1. Wrap the EF Core Code-First migrations (`dbContext.Database.MigrateAsync()`) in a Polly retry policy on startup to handle SQL Server container startup delays.
> 2. Implement the development-only seed data injection using `IDbSeeder`, called explicitly after migrations are complete.
> 3. Create a static code analysis rule or a test that scans the codebase to ensure `IgnoreQueryFilters()` is **only** used inside `Infrastructure/Jobs/` (via `AdminDbContext`) and flag any other usage as a security failure.
> 4. Create the Dead-Letter Queue (DLQ) inspection job. Register a Hangfire recurring job that runs every 30 minutes to check the Azure Service Bus DLQ and create an alert if unprocessed messages accumulate.