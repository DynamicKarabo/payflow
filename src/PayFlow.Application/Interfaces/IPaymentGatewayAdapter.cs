using PayFlow.Domain.ValueObjects;

namespace PayFlow.Application.Interfaces;

public interface IPaymentGatewayAdapter
{
    Task<GatewayAuthoriseResult> AuthoriseAsync(AuthoriseRequest request, CancellationToken ct);
    Task<GatewayCaptureResult> CaptureAsync(string gatewayReference, Money amount, CancellationToken ct);
    Task<GatewayRefundResult> RefundAsync(string gatewayReference, Money amount, CancellationToken ct);
}

public record AuthoriseRequest(
    Money Amount,
    Currency Currency,
    string PaymentToken,
    CustomerId CustomerId);

public record GatewayAuthoriseResult(
    bool Succeeded,
    string? GatewayReference,
    string? FailureReason,
    string? FailureCode);

public record GatewayCaptureResult(
    bool Succeeded,
    string? FailureReason,
    string? FailureCode);

public record GatewayRefundResult(
    bool Succeeded,
    string? GatewayReference,
    string? FailureReason,
    string? FailureCode);
