using FluentValidation;
using MediatR;
using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Exceptions;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Application.Commands;

public sealed record RefundPaymentCommand(
    string PaymentId,
    decimal Amount,
    string Currency,
    string Reason) : IRequest<RefundResponse>;

public sealed class RefundPaymentCommandValidator : AbstractValidator<RefundPaymentCommand>
{
    public RefundPaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId)
            .NotEmpty()
            .WithMessage("Payment ID is required");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than zero");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3)
            .WithMessage("Currency must be a 3-letter ISO code");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Reason is required");
    }
}

public sealed class RefundPaymentCommandHandler : IRequestHandler<RefundPaymentCommand, RefundResponse>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentGatewayAdapter _gatewayAdapter;
    private readonly ITenantContext _tenantContext;

    public RefundPaymentCommandHandler(
        IPaymentRepository paymentRepository,
        IPaymentGatewayAdapter gatewayAdapter,
        ITenantContext tenantContext)
    {
        _paymentRepository = paymentRepository;
        _gatewayAdapter = gatewayAdapter;
        _tenantContext = tenantContext;
    }

    public async Task<RefundResponse> Handle(RefundPaymentCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(request.PaymentId, out var paymentGuid))
        {
            throw new PaymentNotFoundException();
        }

        var paymentId = new PaymentId(paymentGuid);
        var payment = await _paymentRepository.GetByIdAsync(paymentId, ct);

        if (payment == null)
        {
            throw new PaymentNotFoundException();
        }

        var currency = Currency.FromCode(request.Currency);
        var amount = new Money(request.Amount, currency);

        var refund = payment.Refund(amount, request.Reason);

        var refundResult = await _gatewayAdapter.RefundAsync(
            payment.GatewayReference!,
            amount,
            ct);

        if (refundResult.Succeeded)
        {
            refund.MarkSucceeded(refundResult.GatewayReference!);
        }
        else
        {
            refund.MarkFailed(refundResult.FailureReason ?? "Unknown failure");
        }

        await _paymentRepository.UpdateAsync(payment, ct);

        return RefundResponse.FromRefund(refund, $"pay_{payment.Id.Value:N}");
    }
}