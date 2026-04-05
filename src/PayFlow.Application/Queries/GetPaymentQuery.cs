using MediatR;
using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Exceptions;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Application.Queries;

public sealed record GetPaymentQuery(string PaymentId) : IRequest<PaymentResponse>;

public sealed class GetPaymentQueryHandler : IRequestHandler<GetPaymentQuery, PaymentResponse>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ITenantContext _tenantContext;

    public GetPaymentQueryHandler(
        IPaymentRepository paymentRepository,
        ITenantContext tenantContext)
    {
        _paymentRepository = paymentRepository;
        _tenantContext = tenantContext;
    }

    public async Task<PaymentResponse> Handle(GetPaymentQuery request, CancellationToken ct)
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

        return PaymentResponse.FromPayment(payment);
    }
}