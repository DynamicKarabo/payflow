using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Application.DTOs;

public record PaymentResponse(
    string Id,
    string Status,
    decimal Amount,
    string Currency,
    string Mode,
    string? GatewayReference,
    DateTimeOffset CreatedAt,
    string? FailureReason,
    Dictionary<string, string>? Metadata)
{
    public static PaymentResponse FromPayment(Payment payment) => new(
        Id: $"pay_{payment.Id.Value:N}",
        Status: payment.Status.ToString().ToLowerInvariant(),
        Amount: payment.Amount.Amount,
        Currency: payment.Currency.Code,
        Mode: payment.Mode.ToString().ToLowerInvariant(),
        GatewayReference: payment.GatewayReference,
        CreatedAt: payment.CreatedAt,
        FailureReason: payment.FailureReason,
        Metadata: null);
}

public record RefundResponse(
    string Id,
    string PaymentId,
    string Status,
    decimal Amount,
    string Currency,
    DateTimeOffset CreatedAt)
{
    public static RefundResponse FromRefund(Refund refund, string paymentId) => new(
        Id: $"ref_{refund.Id.Value:N}",
        PaymentId: paymentId,
        Status: refund.Status.ToString().ToLowerInvariant(),
        Amount: refund.Amount,
        Currency: refund.CurrencyCode,
        CreatedAt: refund.CreatedAt);
}
