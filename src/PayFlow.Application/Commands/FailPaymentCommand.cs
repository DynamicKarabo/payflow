using FluentValidation;
using MediatR;
using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Exceptions;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Application.Commands;

public sealed record FailPaymentCommand(
    string PaymentId,
    string Reason) : IRequest<PaymentResponse>;

public sealed class FailPaymentCommandValidator : AbstractValidator<FailPaymentCommand>
{
    public FailPaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId)
            .NotEmpty()
            .WithMessage("Payment ID is required");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Failure reason is required");
    }
}

public sealed class FailPaymentCommandHandler : IRequestHandler<FailPaymentCommand, PaymentResponse>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ITenantContext _tenantContext;

    public FailPaymentCommandHandler(
        IPaymentRepository paymentRepository,
        ITenantContext tenantContext)
    {
        _paymentRepository = paymentRepository;
        _tenantContext = tenantContext;
    }

    public async Task<PaymentResponse> Handle(FailPaymentCommand request, CancellationToken ct)
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

        payment.Fail(request.Reason);
        await _paymentRepository.UpdateAsync(payment, ct);

        return PaymentResponse.FromPayment(payment);
    }
}