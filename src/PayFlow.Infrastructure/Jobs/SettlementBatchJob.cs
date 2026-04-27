using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;
using PayFlow.Domain.ValueObjects;
using PayFlow.Infrastructure.Persistence.Context;
using StackExchange.Redis;

namespace PayFlow.Infrastructure.Jobs;

public class SettlementBatchJob
{
    private readonly AdminDbContext _dbContext;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<SettlementBatchJob> _logger;

    public SettlementBatchJob(
        AdminDbContext dbContext,
        IConnectionMultiplexer redis,
        ILogger<SettlementBatchJob> logger)
    {
        _dbContext = dbContext;
        _redis = redis;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var cutoffDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var tenantIds = await _dbContext.AllPayments
            .Where(p => p.Status == PaymentStatus.Captured && p.Mode == PaymentMode.Live)
            .Where(p => p.UpdatedAt < DateTimeOffset.UtcNow.AddDays(-1))
            .Select(p => p.TenantId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var tenantId in tenantIds)
        {
            await ProcessTenantSettlementAsync(tenantId, cutoffDate, ct);
        }

        _logger.LogInformation("Settlement batch job completed");
    }

    private async Task ProcessTenantSettlementAsync(TenantId tenantId, DateOnly cutoffDate, CancellationToken ct)
    {
        var lockKey = $"payflow:lock:settle:{tenantId.Value}:{cutoffDate:yyyy-MM-dd}";
        var db = _redis.GetDatabase();

        try
        {
            var lockAcquired = await db.StringSetAsync(
                lockKey,
                "locked",
                expiry: TimeSpan.FromMinutes(5),
                when: When.NotExists);

            if (!lockAcquired)
            {
                _logger.LogWarning("Settlement lock already held for tenant {TenantId} on {Date}", tenantId, cutoffDate);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to acquire settlement lock for tenant {TenantId}", tenantId);
        }

        var existingBatch = await _dbContext.AllSettlementBatches
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.SettlementDate == cutoffDate, ct);

        if (existingBatch != null)
        {
            _logger.LogWarning("Settlement batch already exists for tenant {TenantId} on {Date}", tenantId, cutoffDate);
            return;
        }

        var capturedPayments = await _dbContext.AllPayments
            .Where(p => p.TenantId == tenantId)
            .Where(p => p.Status == PaymentStatus.Captured)
            .Where(p => p.UpdatedAt < DateTimeOffset.UtcNow.AddDays(-1))
            .ToListAsync(ct);

        if (!capturedPayments.Any())
        {
            _logger.LogInformation("No captured payments to settle for tenant {TenantId}", tenantId);
            return;
        }

        var grossAmount = capturedPayments.Aggregate(
            Money.Zero(Currency.GBP),
            (sum, p) => sum.Add(new Money(p.Amount, p.Currency)));

        var totalFees = capturedPayments.Sum(p => p.Amount * 0.014m + 0.20m);
        var feeAmount = new Money(totalFees, grossAmount.Currency);
        var netAmount = grossAmount.Subtract(feeAmount);

        var batch = new SettlementBatch(
            tenantId,
            cutoffDate,
            grossAmount,
            feeAmount,
            netAmount,
            capturedPayments.Count);

        foreach (var payment in capturedPayments)
        {
            payment.Settle();
            batch.AddPayment(payment.Id);
        }

        batch.Complete();

        _dbContext.SettlementBatches.Add(batch);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Settlement batch created for tenant {TenantId}: {Count} payments, gross={Gross}, net={Net}",
            tenantId, capturedPayments.Count, grossAmount.Amount, netAmount.Amount);
    }
}
