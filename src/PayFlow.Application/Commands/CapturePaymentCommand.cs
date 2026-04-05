using FluentValidation;
using MediatR;
using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Exceptions;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Application.Commands;

public sealed record CapturePaymentCommand(string PaymentId) : IRequest<PaymentResponse>;

public sealed class CapturePaymentCommandValidator : AbstractValidator<CapturePaymentCommand>
{
    public CapturePaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId)
            .NotEmpty()
            .WithMessage("Payment ID is required");
    }
}

public sealed class CapturePaymentCommandHandler : IRequestHandler<CapturePaymentCommand, PaymentResponse>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentGatewayAdapter _gatewayAdapter;
    private readonly ITenantContext _tenantContext;

    public CapturePaymentCommandHandler(
        IPaymentRepository paymentRepository,
        IPaymentGatewayAdapter gatewayAdapter,
        ITenantContext tenantContext)
    {
        _paymentRepository = paymentRepository;
        _gatewayAdapter = gatewayAdapter;
        _tenantContext = tenantContext;
    }

    public async Task<PaymentResponse> Handle(CapturePaymentCommand request, CancellationToken ct)
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

        var captureResult = await _gatewayAdapter.CaptureAsync(
            payment.GatewayReference!,
            payment.Amount,
            ct);

        if (!captureResult.Succeeded)
        {
            throw new InvalidPaymentStateException(
                $"Capture failed: {captureResult.FailureReason ?? "Unknown failure"}");
        }

        payment.Capture();
        await _paymentRepository.UpdateAsync(payment, ct);

        return PaymentResponse.FromPayment(payment);
    }
}