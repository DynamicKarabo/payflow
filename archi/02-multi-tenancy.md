# PayFlow — Multi-Tenancy

## Strategy

PayFlow uses a **shared database, shared schema** multi-tenancy model. Every tenant row-level record carries a `TenantId` column. Isolation is enforced at the EF Core query filter layer so no application code can accidentally read cross-tenant data — the filter is always-on and cannot be bypassed without an explicit, named override.

---

## Tenant Resolution

Tenants are identified via their API key on every request. Tenant context is resolved early in the middleware pipeline and stored in `ITenantContext` for the lifetime of the request.

```
Request
  │
  ├─ [ApiKeyAuthenticationMiddleware]
  │    Extracts Bearer token from Authorization header.
  │    Looks up ApiKey record by key prefix (fast index).
  │    Validates HMAC of full key against stored hash.
  │    Resolves TenantId + Mode (Live/Test).
  │
  ├─ [TenantContextMiddleware]
  │    Populates ITenantContext with TenantId, Mode, Tenant config.
  │    Short-circuits with 401 if tenant is Suspended or Closed.
  │
  └─ Handler / Use Case
       Uses ITenantContext.TenantId — never trusts caller-supplied tenant.
```

```csharp
public interface ITenantContext
{
    TenantId TenantId { get; }
    PaymentMode Mode { get; }
    bool IsLive => Mode == PaymentMode.Live;
}
```

`ITenantContext` is registered as `Scoped`. It is populated once per request by middleware and is read-only to all downstream consumers.

---

## EF Core Global Query Filters

Every entity with tenant-scoped data has a global query filter applied in `OnModelCreating`. This appends a `WHERE TenantId = @currentTenantId` to every query transparently.

```csharp
// Infrastructure/Persistence/PayFlowDbContext.cs

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Payment
    modelBuilder.Entity<Payment>()
        .HasQueryFilter(p => p.TenantId == _tenantContext.TenantId);

    // Refund
    modelBuilder.Entity<Refund>()
        .HasQueryFilter(r => r.TenantId == _tenantContext.TenantId);

    // ApiKey — filtered so a tenant cannot enumerate other tenants' keys
    modelBuilder.Entity<ApiKey>()
        .HasQueryFilter(k => k.TenantId == _tenantContext.TenantId);
}
```

`_tenantContext` is injected into `DbContext` via constructor. Because `DbContext` is `Scoped`, this resolves safely per-request.

### Filter Bypass (Internal Jobs Only)

Background jobs (settlement batcher, webhook dispatcher) operate across all tenants. They use a dedicated `AdminDbContext` subclass that calls `IgnoreQueryFilters()` at the repository level, not globally.

```csharp
// Only used in Hangfire job implementations — never exposed to API layer
public sealed class AdminDbContext : PayFlowDbContext
{
    public IQueryable<Payment> AllPayments => 
        Set<Payment>().IgnoreQueryFilters();
}
```

Reviewers should flag any use of `IgnoreQueryFilters()` outside of `Infrastructure/Jobs/` as a security finding.

---

## API Key Design

Keys are issued in two modes:

| Prefix | Mode | Usage |
|---|---|---|
| `pk_live_` | Live | Real payments; billable |
| `pk_test_` | Test | Sandbox only; no real money movement |

**Key format:** `pk_live_{base58(32 random bytes)}`

**Storage:**
- The key prefix (first 12 chars) is stored in plaintext for fast lookup.
- The full key hash is stored as `bcrypt(key, cost=12)`.
- The plaintext key is returned **once** at creation and never stored.

**Validation flow:**
```
1. Extract raw key from Authorization: Bearer <key>
2. Read prefix → SELECT ApiKey WHERE KeyPrefix = @prefix AND Status = 'Active'
3. bcrypt.Verify(rawKey, storedHash)
4. If match → resolve TenantId; if mismatch → 401
5. Check ApiKey.ExpiresAt — 401 if expired
6. Populate ITenantContext
```

---

## Mode Isolation

Test and live payments are logically separated at every layer:

- API keys carry a `Mode` field — a `pk_test_` key cannot create a live payment.
- `Payment.Mode` is set at creation from the API key mode and is immutable.
- Queries in dashboards and reports always filter by both `TenantId` and `Mode`.
- The payment gateway adapter short-circuits in test mode and returns a stubbed response.
- Settlement jobs skip test-mode payments entirely.

---

## Tenant Lifecycle

| Status | API Behaviour |
|---|---|
| `Active` | All operations permitted |
| `Suspended` | 403 on all write operations; reads allowed for dispute resolution |
| `Closed` | 403 on all operations; data retained per retention policy |

Tenant status is checked once in `TenantContextMiddleware` and cached in `ITenantContext` for the request.

---

## Data Isolation Audit Checklist

- [ ] Every tenant-scoped entity has `TenantId` column with `NOT NULL` constraint.
- [ ] Every EF entity configuration applies the global query filter.
- [ ] No raw SQL in the codebase bypasses the filter without explicit `AdminDbContext`.
- [ ] `TenantId` in API request bodies is **always ignored** — resolved from `ITenantContext` only.
- [ ] Integration tests include a cross-tenant access test that expects 404/403.
- [ ] All Hangfire job parameters that carry `TenantId` are validated before use.
