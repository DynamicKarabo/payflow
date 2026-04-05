using FluentValidation;
using MediatR;
using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Exceptions;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Application.Commands;

public sealed record CancelPaymentCommand(string PaymentId) : IRequest<PaymentResponse>;

public sealed class CancelPaymentCommandValidator : AbstractValidator<CancelPaymentCommand>
{
    public CancelPaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId)
            .NotEmpty()
            .WithMessage("Payment ID is required");
    }
}

public sealed class CancelPaymentCommandHandler : IRequestHandler<CancelPaymentCommand, PaymentResponse>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ITenantContext _tenantContext;

    public CancelPaymentCommandHandler(
        IPaymentRepository paymentRepository,
        ITenantContext tenantContext)
    {
        _paymentRepository = paymentRepository;
        _tenantContext = tenantContext;
    }

    public async Task<PaymentResponse> Handle(CancelPaymentCommand request, CancellationToken ct)
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

        payment.Cancel();
        await _paymentRepository.UpdateAsync(payment, ct);

        return PaymentResponse.FromPayment(payment);
    }
}