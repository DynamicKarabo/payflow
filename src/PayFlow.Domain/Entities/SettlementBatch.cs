using PayFlow.Domain.Enums;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Domain.Entities;

public sealed class SettlementBatch : AggregateRoot
{
    public SettlementBatchId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public DateOnly SettlementDate { get; private set; }

    private decimal _grossAmount;
    public decimal GrossAmountValue
    {
        get => _grossAmount;
        private set => _grossAmount = value;
    }

    private string _grossCurrency = null!;
    public string GrossCurrencyCode
    {
        get => _grossCurrency;
        private set => _grossCurrency = value;
    }

    public Money GrossAmount => new(GrossAmountValue, Currency.FromCode(GrossCurrencyCode));

    private decimal _feeAmount;
    public decimal FeeAmountValue
    {
        get => _feeAmount;
        private set => _feeAmount = value;
    }

    private string _feeCurrency = null!;
    public string FeeCurrencyCode
    {
        get => _feeCurrency;
        private set => _feeCurrency = value;
    }

    public Money FeeAmount => new(FeeAmountValue, Currency.FromCode(FeeCurrencyCode));

    private decimal _netAmount;
    public decimal NetAmountValue
    {
        get => _netAmount;
        private set => _netAmount = value;
    }

    private string _netCurrency = null!;
    public string NetCurrencyCode
    {
        get => _netCurrency;
        private set => _netCurrency = value;
    }

    public Money NetAmount => new(NetAmountValue, Currency.FromCode(NetCurrencyCode));

    public int PaymentCount { get; private set; }
    public SettlementBatchStatus Status { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private readonly List<PaymentId> _paymentIds = new();
    public IReadOnlyList<PaymentId> PaymentIds => _paymentIds.AsReadOnly();

    private SettlementBatch() { }

    public SettlementBatch(TenantId tenantId, DateOnly settlementDate, Money grossAmount, Money feeAmount, Money netAmount, int paymentCount)
    {
        Id = new SettlementBatchId(Guid.NewGuid());
        TenantId = tenantId;
        SettlementDate = settlementDate;
        _grossAmount = grossAmount.Amount;
        _grossCurrency = grossAmount.Currency.Code;
        _feeAmount = feeAmount.Amount;
        _feeCurrency = feeAmount.Currency.Code;
        _netAmount = netAmount.Amount;
        _netCurrency = netAmount.Currency.Code;
        PaymentCount = paymentCount;
        Status = SettlementBatchStatus.Pending;
    }

    public void Complete()
    {
        Status = SettlementBatchStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Fail()
    {
        Status = SettlementBatchStatus.Failed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddPayment(PaymentId paymentId)
    {
        _paymentIds.Add(paymentId);
    }
}

public enum SettlementBatchStatus
{
    Pending,
    Completed,
    Failed
}
