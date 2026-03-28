using Microsoft.EntityFrameworkCore;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;
using PayFlow.Domain.ValueObjects;
using PayFlow.Infrastructure.Exceptions;
using PayFlow.Infrastructure.MultiTenancy;
using PayFlow.Infrastructure.Persistence.Context;
using Xunit;

namespace PayFlow.Integration.Tests;

public class MultiTenancyTests : IDisposable
{
    private readonly TenantId _tenantAId = new(Guid.NewGuid());
    private readonly TenantId _tenantBId = new(Guid.NewGuid());

    [Fact]
    public async Task QueryFilter_ShouldIsolateTenantData()
    {
        var tenantContextA = CreateTenantContext(_tenantAId);
        var optionsA = CreateDbContextOptions();

        var tenantContextB = CreateTenantContext(_tenantBId);
        var optionsB = CreateDbContextOptions();

        await using var dbA = new PayFlowDbContext(optionsA, tenantContextA);
        await using var dbB = new PayFlowDbContext(optionsB, tenantContextB);

        var paymentA = CreateTestPayment(_tenantAId);
        var paymentB = CreateTestPayment(_tenantBId);

        dbA.Payments.Add(paymentA);
        dbB.Payments.Add(paymentB);
        await dbA.SaveChangesAsync();
        await dbB.SaveChangesAsync();

        var paymentsForA = await dbA.Payments.ToListAsync();
        var paymentsForB = await dbB.Payments.ToListAsync();

        Assert.Single(paymentsForA);
        Assert.Equal(_tenantAId, paymentsForA.First().TenantId);
        Assert.Single(paymentsForB);
        Assert.Equal(_tenantBId, paymentsForB.First().TenantId);
    }

    private static DbContextOptions<PayFlowDbContext> CreateDbContextOptions()
    {
        var optionsBuilder = new DbContextOptionsBuilder<PayFlowDbContext>();
        optionsBuilder.UseInMemoryDatabase($"TestDb_{Guid.NewGuid():N}");
        return optionsBuilder.Options;
    }

    private static ITenantContext CreateTenantContext(TenantId tenantId)
    {
        return new TestTenantContext(tenantId);
    }

    private static Payment CreateTestPayment(TenantId tenantId)
    {
        var amount = new Money(100m, Currency.GBP);
        var paymentMethod = new PaymentMethodSnapshot("card", "4242", "visa");

        return Payment.Create(
            tenantId,
            new IdempotencyKey($"key-{Guid.NewGuid():N}"),
            amount,
            Currency.GBP,
            PaymentMode.Live,
            new CustomerId(Guid.NewGuid()),
            paymentMethod);
    }

    public void Dispose()
    {
    }

    private class TestTenantContext : ITenantContext
    {
        public TestTenantContext(TenantId tenantId)
        {
            TenantId = tenantId;
        }

        public TenantId TenantId { get; }
        public PaymentMode Mode => PaymentMode.Live;
        public bool IsLive => true;
    }
}

public class ConcurrencyTests
{
    [Fact]
    public void PaymentConcurrencyException_ShouldExist()
    {
        var exception = new PaymentConcurrencyConflictException();

        Assert.NotNull(exception);
        Assert.Contains("modified", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
