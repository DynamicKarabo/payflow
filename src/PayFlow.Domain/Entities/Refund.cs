using PayFlow.Domain.Enums;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Domain.Entities;

public sealed class Refund : Entity
{
    public RefundId Id { get; private set; }
    public PaymentId PaymentId { get; private set; }
    public TenantId TenantId { get; private set; }

    private decimal _amount;
    public decimal Amount
    {
        get => _amount;
        private set => _amount = value;
    }

    private string _currencyCode = null!;
    public string CurrencyCode
    {
        get => _currencyCode;
        private set => _currencyCode = value;
    }

    public Currency Currency => Currency.FromCode(CurrencyCode);
    public Money Money => new(Amount, Currency);

    public RefundStatus Status { get; private set; }
    public string Reason { get; private set; }
    public string? GatewayReference { get; private set; }
    public string? FailureReason { get; private set; }

    private Refund() { }

    internal Refund(RefundId id, PaymentId paymentId, TenantId tenantId, Money amount, string reason)
    {
        Id = id;
        PaymentId = paymentId;
        TenantId = tenantId;
        _amount = amount.Amount;
        _currencyCode = amount.Currency.Code;
        Status = RefundStatus.Pending;
        Reason = reason;
    }

    internal void MarkSucceeded(string gatewayReference)
    {
        GatewayReference = gatewayReference;
        Status = RefundStatus.Succeeded;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    internal void MarkFailed(string failureReason)
    {
        FailureReason = failureReason;
        Status = RefundStatus.Failed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
