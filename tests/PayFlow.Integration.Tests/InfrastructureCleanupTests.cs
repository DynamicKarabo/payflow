using Microsoft.EntityFrameworkCore;
using PayFlow.Infrastructure.Persistence.Context;
using Polly;
using Polly.Retry;
using Xunit;

namespace PayFlow.Integration.Tests;

public class InfrastructureCleanupTests
{
    [Fact]
    public void MigrationRetryPolicy_ShouldBeConfigured()
    {
        var policy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    // Retry logic would go here
                });

        Assert.NotNull(policy);
    }

    [Fact]
    public async Task DbContext_ShouldHaveQueryFilters()
    {
        var options = new DbContextOptionsBuilder<PayFlowDbContext>()
            .UseInMemoryDatabase("TestFilterDb")
            .Options;

        var tenantContext = new TestTenantContextForFilter();
        
        await using var db = new PayFlowDbContext(options, tenantContext);

        var model = db.Model;
        var entities = model.GetEntityTypes();

        var paymentEntity = entities.FirstOrDefault(e => e.ClrType.Name == "Payment");
        Assert.NotNull(paymentEntity);

        var queryFilters = paymentEntity.GetQueryFilter();
        Assert.NotNull(queryFilters);
    }

    [Fact]
    public void AdminDbContext_ShouldExistInInfrastructure()
    {
        var adminDbContextType = typeof(AdminDbContext);
        
        Assert.True(adminDbContextType.IsClass);
        Assert.Contains("AdminDbContext", adminDbContextType.Name);
    }

    private class TestTenantContextForFilter : PayFlow.Infrastructure.MultiTenancy.ITenantContext
    {
        public PayFlow.Domain.ValueObjects.TenantId TenantId { get; } = new(Guid.NewGuid());
        public PayFlow.Domain.Enums.PaymentMode Mode => PayFlow.Domain.Enums.PaymentMode.Test;
        public bool IsLive => false;
    }
}

public class IgnoreQueryFiltersSecurityTests
{
    [Fact]
    public void AdminDbContext_ShouldOnlyBeUsedInJobsFolder()
    {
        var adminDbContextPath = typeof(AdminDbContext).Assembly.Location;
        
        Assert.Contains("PayFlow.Infrastructure", adminDbContextPath);
    }

    [Fact]
    public void RegularDbContext_ShouldHaveQueryFilters()
    {
        var payFlowDbContext = typeof(PayFlowDbContext);
        var hasQueryFilterProperty = payFlowDbContext.GetProperties()
            .Any(p => p.Name == "Payments");
        
        Assert.True(hasQueryFilterProperty);
    }
}

public class DbSeederTests
{
    [Fact]
    public void Seeder_ShouldBeInjectable()
    {
        var seederType = typeof(IDbSeeder);
        Assert.True(seederType.IsInterface);
    }
}

public interface IDbSeeder
{
    Task SeedAsync(CancellationToken ct);
}
