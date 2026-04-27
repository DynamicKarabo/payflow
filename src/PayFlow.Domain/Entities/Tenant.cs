using PayFlow.Domain.Enums;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Domain.Entities;

public sealed class Tenant : AggregateRoot
{
    public TenantId Id { get; private set; }
    public string Name { get; private set; }
    public TenantStatus Status { get; private set; }
    public Currency SettlementCurrency { get; private set; }
    public int SettlementCutoffHours { get; private set; }
    public decimal PercentageFee { get; private set; }
    public decimal FixedFeeAmount { get; private set; }
    public decimal DailyLimit { get; private set; }

    private Tenant() { }

    public Tenant(TenantId id, string name, Currency settlementCurrency)
    {
        Id = id;
        Name = name;
        Status = TenantStatus.Active;
        SettlementCurrency = settlementCurrency;
        SettlementCutoffHours = 24;
        PercentageFee = 0.014m;
        FixedFeeAmount = 0.20m;
        DailyLimit = 50000m;
    }

    public void UpdateStatus(TenantStatus status)
    {
        Status = status;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateFeeConfig(decimal percentageFee, decimal fixedFeeAmount)
    {
        PercentageFee = percentageFee;
        FixedFeeAmount = fixedFeeAmount;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateDailyLimit(decimal dailyLimit)
    {
        DailyLimit = dailyLimit;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateSettlementConfig(int cutoffHours, Currency currency)
    {
        SettlementCutoffHours = cutoffHours;
        SettlementCurrency = currency;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool CanProcessPayment(Money amount)
    {
        return Status == TenantStatus.Active && amount.Amount <= DailyLimit;
    }
}
