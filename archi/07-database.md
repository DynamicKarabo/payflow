# PayFlow — Database Schema & EF Core

---

## Schema Overview

All PayFlow tables live in the `payflow` schema. Hangfire uses its own `HangfireSchema` schema on the same server.

```
payflow.Tenants
payflow.ApiKeys
payflow.Payments
payflow.Refunds
payflow.SettlementBatches
payflow.SettlementBatchPayments       ← join table
payflow.WebhookEndpoints
payflow.WebhookDeliveries
```

---

## Table Definitions

### `payflow.Tenants`

```sql
CREATE TABLE payflow.Tenants (
    Id                   UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    Name                 NVARCHAR(200)       NOT NULL,
    Status               NVARCHAR(20)        NOT NULL,   -- Active | Suspended | Closed
    WebhookEndpointUrl   NVARCHAR(2048)      NULL,
    WebhookHmacSecret    NVARCHAR(512)       NULL,       -- AES-256 encrypted
    WebhookEvents        NVARCHAR(MAX)       NULL,       -- JSON array
    SettlementCurrency   NCHAR(3)            NOT NULL,
    SettlementCutoffHours INT               NOT NULL DEFAULT 24,
    PercentageFee        DECIMAL(6,4)        NOT NULL DEFAULT 0.014,
    FixedFeeAmount       DECIMAL(18,4)       NOT NULL DEFAULT 0.20,
    DailyLimitAmount     DECIMAL(18,4)       NOT NULL DEFAULT 50000.00,
    DailyLimitCurrency   NCHAR(3)            NOT NULL DEFAULT 'GBP',
    CreatedAt            DATETIMEOFFSET      NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    UpdatedAt            DATETIMEOFFSET      NOT NULL DEFAULT SYSDATETIMEOFFSET(),

    CONSTRAINT PK_Tenants PRIMARY KEY (Id)
);
```

### `payflow.ApiKeys`

```sql
CREATE TABLE payflow.ApiKeys (
    Id              UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    TenantId        UNIQUEIDENTIFIER    NOT NULL,
    KeyPrefix       NVARCHAR(12)        NOT NULL,   -- "pk_live_xxxxx" (12 chars)
    HashedSecret    NVARCHAR(255)       NOT NULL,   -- bcrypt hash
    Mode            NVARCHAR(10)        NOT NULL,   -- Live | Test
    Status          NVARCHAR(10)        NOT NULL,   -- Active | Revoked
    ExpiresAt       DATETIMEOFFSET      NULL,
    CreatedAt       DATETIMEOFFSET      NOT NULL DEFAULT SYSDATETIMEOFFSET(),

    CONSTRAINT PK_ApiKeys PRIMARY KEY (Id),
    CONSTRAINT FK_ApiKeys_Tenants FOREIGN KEY (TenantId) REFERENCES payflow.Tenants(Id),
    CONSTRAINT UQ_ApiKeys_KeyPrefix UNIQUE (KeyPrefix)
);

CREATE INDEX IX_ApiKeys_TenantId ON payflow.ApiKeys (TenantId);
CREATE INDEX IX_ApiKeys_KeyPrefix_Status ON payflow.ApiKeys (KeyPrefix, Status);
```

### `payflow.Payments`

```sql
CREATE TABLE payflow.Payments (
    Id                   UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    TenantId             UNIQUEIDENTIFIER    NOT NULL,
    IdempotencyKey       NVARCHAR(64)        NOT NULL,
    Amount               DECIMAL(18,4)       NOT NULL,
    Currency             NCHAR(3)            NOT NULL,
    Status               NVARCHAR(20)        NOT NULL,
    Mode                 NVARCHAR(10)        NOT NULL,   -- Live | Test
    CustomerId           UNIQUEIDENTIFIER    NOT NULL,
    PaymentMethodType    NVARCHAR(20)        NOT NULL,
    PaymentMethodLast4   NCHAR(4)            NULL,
    PaymentMethodBrand   NVARCHAR(20)        NULL,
    PaymentMethodExpiry  NVARCHAR(7)         NULL,       -- "MM/YYYY"
    GatewayReference     NVARCHAR(255)       NULL,
    FailureReason        NVARCHAR(500)       NULL,
    Metadata             NVARCHAR(MAX)       NULL,       -- JSON
    CreatedAt            DATETIMEOFFSET      NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    UpdatedAt            DATETIMEOFFSET      NOT NULL DEFAULT SYSDATETIMEOFFSET(),

    CONSTRAINT PK_Payments PRIMARY KEY (Id),
    CONSTRAINT FK_Payments_Tenants FOREIGN KEY (TenantId) REFERENCES payflow.Tenants(Id),
    CONSTRAINT CK_Payments_Amount CHECK (Amount > 0),
    CONSTRAINT UQ_Payments_IdempotencyKey UNIQUE (TenantId, IdempotencyKey)
);

CREATE INDEX IX_Payments_TenantId_Status ON payflow.Payments (TenantId, Status);
CREATE INDEX IX_Payments_TenantId_CreatedAt ON payflow.Payments (TenantId, CreatedAt DESC);
CREATE INDEX IX_Payments_TenantId_Mode_Status ON payflow.Payments (TenantId, Mode, Status)
    WHERE Status = 'Captured';  -- filtered index for settlement query
```

### `payflow.Refunds`

```sql
CREATE TABLE payflow.Refunds (
    Id                   UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    PaymentId            UNIQUEIDENTIFIER    NOT NULL,
    TenantId             UNIQUEIDENTIFIER    NOT NULL,
    Amount               DECIMAL(18,4)       NOT NULL,
    Currency             NCHAR(3)            NOT NULL,
    Status               NVARCHAR(20)        NOT NULL,
    Reason               NVARCHAR(500)       NOT NULL,
    GatewayReference     NVARCHAR(255)       NULL,
    FailureReason        NVARCHAR(500)       NULL,
    CreatedAt            DATETIMEOFFSET      NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    UpdatedAt            DATETIMEOFFSET      NOT NULL DEFAULT SYSDATETIMEOFFSET(),

    CONSTRAINT PK_Refunds PRIMARY KEY (Id),
    CONSTRAINT FK_Refunds_Payments FOREIGN KEY (PaymentId) REFERENCES payflow.Payments(Id),
    CONSTRAINT CK_Refunds_Amount CHECK (Amount > 0)
);

CREATE INDEX IX_Refunds_PaymentId ON payflow.Refunds (PaymentId);
CREATE INDEX IX_Refunds_TenantId_Status ON payflow.Refunds (TenantId, Status);
```

### `payflow.SettlementBatches`

```sql
CREATE TABLE payflow.SettlementBatches (
    Id                   UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    TenantId             UNIQUEIDENTIFIER    NOT NULL,
    SettlementDate       DATE                NOT NULL,
    GrossAmount          DECIMAL(18,4)       NOT NULL,
    FeeAmount            DECIMAL(18,4)       NOT NULL,
    NetAmount            DECIMAL(18,4)       NOT NULL,
    Currency             NCHAR(3)            NOT NULL,
    PaymentCount         INT                 NOT NULL,
    Status               NVARCHAR(20)        NOT NULL,
    CompletedAt          DATETIMEOFFSET      NULL,
    CreatedAt            DATETIMEOFFSET      NOT NULL DEFAULT SYSDATETIMEOFFSET(),

    CONSTRAINT PK_SettlementBatches PRIMARY KEY (Id),
    CONSTRAINT FK_SettlementBatches_Tenants FOREIGN KEY (TenantId) REFERENCES payflow.Tenants(Id),
    CONSTRAINT UQ_SettlementBatches_TenantDate UNIQUE (TenantId, SettlementDate)
);
```

### `payflow.SettlementBatchPayments`

```sql
CREATE TABLE payflow.SettlementBatchPayments (
    SettlementBatchId    UNIQUEIDENTIFIER    NOT NULL,
    PaymentId            UNIQUEIDENTIFIER    NOT NULL,

    CONSTRAINT PK_SettlementBatchPayments PRIMARY KEY (SettlementBatchId, PaymentId),
    CONSTRAINT FK_SBP_Batches  FOREIGN KEY (SettlementBatchId)
        REFERENCES payflow.SettlementBatches(Id),
    CONSTRAINT FK_SBP_Payments FOREIGN KEY (PaymentId)
        REFERENCES payflow.Payments(Id)
);
```

### `payflow.WebhookDeliveries`

```sql
CREATE TABLE payflow.WebhookDeliveries (
    Id                   UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    TenantId             UNIQUEIDENTIFIER    NOT NULL,
    EventId              NVARCHAR(64)        NOT NULL,
    EventType            NVARCHAR(100)       NOT NULL,
    EndpointUrl          NVARCHAR(2048)      NOT NULL,
    Payload              NVARCHAR(MAX)       NOT NULL,
    Status               NVARCHAR(20)        NOT NULL,  -- Pending | Delivered | Dead
    AttemptCount         INT                 NOT NULL DEFAULT 0,
    LastHttpStatus       INT                 NULL,
    LastFailureReason    NVARCHAR(500)       NULL,
    DeliveredAt          DATETIMEOFFSET      NULL,
    NextAttemptAt        DATETIMEOFFSET      NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CreatedAt            DATETIMEOFFSET      NOT NULL DEFAULT SYSDATETIMEOFFSET(),

    CONSTRAINT PK_WebhookDeliveries PRIMARY KEY (Id)
);

CREATE INDEX IX_WebhookDeliveries_TenantId_Status ON payflow.WebhookDeliveries (TenantId, Status);
CREATE INDEX IX_WebhookDeliveries_EventId ON payflow.WebhookDeliveries (EventId);
```

---

## EF Core Configuration

### DbContext

```csharp
public class PayFlowDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public PayFlowDbContext(
        DbContextOptions<PayFlowDbContext> options,
        ITenantContext tenantContext) : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<SettlementBatch> SettlementBatches => Set<SettlementBatch>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("payflow");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PayFlowDbContext).Assembly);
    }

    // Intercept SaveChanges to auto-update UpdatedAt
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<Entity>()
            .Where(e => e.State == EntityState.Modified))
        {
            entry.Property(nameof(Entity.UpdatedAt)).CurrentValue =
                DateTimeOffset.UtcNow;
        }
        return base.SaveChangesAsync(ct);
    }
}
```

### Entity Configuration Example

```csharp
// Infrastructure/Persistence/Configurations/PaymentConfiguration.cs

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, v => new PaymentId(v));

        builder.Property(p => p.TenantId)
            .HasConversion(id => id.Value, v => new TenantId(v))
            .IsRequired();

        builder.Property(p => p.Amount)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.Mode)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.OwnsOne(p => p.PaymentMethod, pm => {
            pm.Property(m => m.Type).HasColumnName("PaymentMethodType").HasMaxLength(20);
            pm.Property(m => m.Last4).HasColumnName("PaymentMethodLast4").HasMaxLength(4);
            pm.Property(m => m.Brand).HasColumnName("PaymentMethodBrand").HasMaxLength(20);
        });

        builder.HasMany(p => p.Refunds)
            .WithOne()
            .HasForeignKey(r => r.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(p =>
            p.TenantId == _tenantContext.TenantId);

        builder.HasIndex(p => new { p.TenantId, p.IdempotencyKey }).IsUnique();
        builder.HasIndex(p => new { p.TenantId, p.Status });
    }

    // _tenantContext injected via constructor
}
```

---

## Migrations Strategy

- EF Core Code-First migrations.
- Migrations are applied at startup in production via `dbContext.Database.MigrateAsync()` wrapped in a retry policy (Polly — handles SQL Server not-yet-ready on container startup).
- Migration files live in `Infrastructure/Persistence/Migrations/`.
- Each migration is reviewed for:
  - No data loss on existing rows.
  - `NOT NULL` columns always have a `DEFAULT` or migration-time backfill.
  - Index additions are `CREATE INDEX ... WITH (ONLINE = ON)` where possible (handled via raw SQL in migration).

### Seed Data

Test tenants and API keys are seeded via `IDbSeeder` on development environment only, called after migration.

---

## Concurrency

### Optimistic Concurrency on Payments

`Payment` uses a SQL Server `rowversion` column to prevent lost updates when concurrent requests attempt to transition the same payment.

```csharp
builder.Property<byte[]>("RowVersion")
    .IsRowVersion()
    .IsConcurrencyToken();
```

On conflict, EF Core throws `DbUpdateConcurrencyException`, which is caught by the application layer and returned as `409 Conflict` with `payment_concurrency_conflict` error type.

### Settlement Job Mutex

Before the settlement job begins processing a tenant's payments, it acquires a Redis distributed lock:

```csharp
var lockKey = $"payflow:lock:settle:{tenantId}:{settlementDate:yyyy-MM-dd}";
await using var lock = await _redis.AcquireLockAsync(lockKey, expiry: TimeSpan.FromMinutes(5));
if (lock is null) return;  // another job instance holds the lock
```

This prevents double-settlement if two Hangfire workers pick up the same job due to a visibility timeout edge case.
