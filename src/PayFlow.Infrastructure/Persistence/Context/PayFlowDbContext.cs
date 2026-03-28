using Microsoft.EntityFrameworkCore;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;
using PayFlow.Domain.ValueObjects;
using PayFlow.Infrastructure.MultiTenancy;

namespace PayFlow.Infrastructure.Persistence.Context;

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
        
        ConfigurePayment(modelBuilder);
        ConfigureRefund(modelBuilder);
        ConfigureTenant(modelBuilder);
        ConfigureApiKey(modelBuilder);
        ConfigureSettlementBatch(modelBuilder);
        ConfigureWebhookDelivery(modelBuilder);
    }

    private void ConfigurePayment(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(builder =>
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

            builder.Property(p => p.Currency)
                .HasConversion(c => c.Code, v => Currency.FromCode(v))
                .HasMaxLength(3)
                .IsRequired();

            builder.Property(p => p.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(p => p.Mode)
                .HasConversion<string>()
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(p => p.CustomerId)
                .HasConversion(id => id.Value, v => new CustomerId(v));

            builder.Property(p => p.IdempotencyKey)
                .HasConversion(k => k.Value, v => new IdempotencyKey(v))
                .HasMaxLength(64)
                .IsRequired();

            builder.OwnsOne(p => p.PaymentMethod, pm =>
            {
                pm.Property(m => m.Type).HasColumnName("PaymentMethodType").HasMaxLength(20);
                pm.Property(m => m.Last4).HasColumnName("PaymentMethodLast4").HasMaxLength(4);
                pm.Property(m => m.Brand).HasColumnName("PaymentMethodBrand").HasMaxLength(20);
                pm.Property(m => m.ExpiryMonth).HasColumnName("PaymentMethodExpiryMonth").HasMaxLength(2);
                pm.Property(m => m.ExpiryYear).HasColumnName("PaymentMethodExpiryYear").HasMaxLength(4);
                pm.Property(m => m.BankName).HasColumnName("PaymentMethodBankName").HasMaxLength(100);
            });

            builder.HasMany(p => p.Refunds)
                .WithOne()
                .HasForeignKey(r => r.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(p => new { p.TenantId, p.IdempotencyKey }).IsUnique();
            builder.HasIndex(p => new { p.TenantId, p.Status });

            builder.HasQueryFilter(p => p.TenantId == _tenantContext.TenantId);

            builder.Property<byte[]>("RowVersion")
                .IsRowVersion()
                .IsConcurrencyToken();
        });
    }

    private void ConfigureRefund(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Refund>(builder =>
        {
            builder.ToTable("Refunds");
            builder.HasKey(r => r.Id);

            builder.Property(r => r.Id)
                .HasConversion(id => id.Value, v => new RefundId(v));

            builder.Property(r => r.PaymentId)
                .HasConversion(id => id.Value, v => new PaymentId(v));

            builder.Property(r => r.TenantId)
                .HasConversion(id => id.Value, v => new TenantId(v))
                .IsRequired();

            builder.Property(r => r.Amount)
                .HasColumnName("Amount")
                .HasPrecision(18, 4)
                .IsRequired();

            builder.Property(r => r.CurrencyCode)
                .HasColumnName("Currency")
                .HasMaxLength(3)
                .IsRequired();

            builder.Ignore(r => r.Amount);
            builder.Ignore(r => r.CurrencyCode);
            builder.Ignore(r => r.Money);

            builder.Property(r => r.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(r => r.Reason)
                .HasMaxLength(500)
                .IsRequired();

            builder.HasIndex(r => r.PaymentId);
            builder.HasIndex(r => new { r.TenantId, r.Status });

            builder.HasQueryFilter(r => r.TenantId == _tenantContext.TenantId);
        });
    }

    private void ConfigureTenant(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(builder =>
        {
            builder.ToTable("Tenants");
            builder.HasKey(t => t.Id);

            builder.Property(t => t.Id)
                .HasConversion(id => id.Value, v => new TenantId(v));

            builder.Property(t => t.Name)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(t => t.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(t => t.SettlementCurrency)
                .HasConversion(c => c.Code, v => Currency.FromCode(v))
                .HasMaxLength(3)
                .IsRequired();

            builder.Property(t => t.FixedFeeAmount)
                .HasPrecision(18, 4)
                .HasColumnName("FixedFeeAmount");

            builder.Property(t => t.DailyLimit)
                .HasPrecision(18, 4)
                .HasColumnName("DailyLimitAmount");
        });
    }

    private void ConfigureApiKey(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiKey>(builder =>
        {
            builder.ToTable("ApiKeys");
            builder.HasKey(k => k.Id);

            builder.Property(k => k.Id)
                .HasConversion(id => id.Value, v => new ApiKeyId(v));

            builder.Property(k => k.TenantId)
                .HasConversion(id => id.Value, v => new TenantId(v))
                .IsRequired();

            builder.Property(k => k.KeyPrefix)
                .HasMaxLength(12)
                .IsRequired();

            builder.Property(k => k.HashedSecret)
                .HasMaxLength(255)
                .IsRequired();

            builder.Property(k => k.Mode)
                .HasConversion<string>()
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(k => k.Status)
                .HasConversion<string>()
                .HasMaxLength(10)
                .IsRequired();

            builder.HasIndex(k => k.KeyPrefix).IsUnique();
            builder.HasIndex(k => new { k.TenantId, k.Status });

            builder.HasQueryFilter(k => k.TenantId == _tenantContext.TenantId);
        });
    }

    private void ConfigureSettlementBatch(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SettlementBatch>(builder =>
        {
            builder.ToTable("SettlementBatches");
            builder.HasKey(b => b.Id);

            builder.Property(b => b.Id)
                .HasConversion(id => id.Value, v => new SettlementBatchId(v));

            builder.Property(b => b.TenantId)
                .HasConversion(id => id.Value, v => new TenantId(v))
                .IsRequired();

            builder.Property(b => b.GrossAmountValue)
                .HasColumnName("GrossAmount")
                .HasPrecision(18, 4);

            builder.Property(b => b.GrossCurrencyCode)
                .HasColumnName("GrossCurrency")
                .HasMaxLength(3);

            builder.Property(b => b.FeeAmountValue)
                .HasColumnName("FeeAmount")
                .HasPrecision(18, 4);

            builder.Property(b => b.FeeCurrencyCode)
                .HasColumnName("FeeCurrency")
                .HasMaxLength(3);

            builder.Property(b => b.NetAmountValue)
                .HasColumnName("NetAmount")
                .HasPrecision(18, 4);

            builder.Property(b => b.NetCurrencyCode)
                .HasColumnName("NetCurrency")
                .HasMaxLength(3);

            builder.Ignore(b => b.GrossAmount);
            builder.Ignore(b => b.FeeAmount);
            builder.Ignore(b => b.NetAmount);

            builder.Property(b => b.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.HasIndex(b => new { b.TenantId, b.SettlementDate }).IsUnique();

            builder.HasQueryFilter(b => b.TenantId == _tenantContext.TenantId);
        });
    }

    private void ConfigureWebhookDelivery(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WebhookDelivery>(builder =>
        {
            builder.ToTable("WebhookDeliveries");
            builder.HasKey(w => w.Id);

            builder.Property(w => w.Id)
                .HasConversion(id => id.Value, v => new WebhookDeliveryId(v));

            builder.Property(w => w.TenantId)
                .HasConversion(id => id.Value, v => new TenantId(v))
                .IsRequired();

            builder.Property(w => w.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.HasIndex(w => new { w.TenantId, w.Status });
            builder.HasIndex(w => w.EventId);

            builder.HasQueryFilter(w => w.TenantId == _tenantContext.TenantId);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<Entity>()
            .Where(e => e.State == EntityState.Modified))
        {
            entry.Property(nameof(Entity.UpdatedAt)).CurrentValue = DateTimeOffset.UtcNow;
        }
        return base.SaveChangesAsync(ct);
    }
}
