using MediatR;
using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Exceptions;
using PayFlow.Domain.ValueObjects;
using StackExchange.Redis;

namespace PayFlow.Application.Queries;

public sealed record GetPaymentQuery(string PaymentId) : IRequest<PaymentResponse>;

public sealed class GetPaymentQueryHandler : IRequestHandler<GetPaymentQuery, PaymentResponse>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ITenantContext _tenantContext;
    private readonly IConnectionMultiplexer _redis;

    public GetPaymentQueryHandler(
        IPaymentRepository paymentRepository,
        ITenantContext tenantContext,
        IConnectionMultiplexer redis)
    {
        _paymentRepository = paymentRepository;
        _tenantContext = tenantContext;
        _redis = redis;
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

        var redisKey = $"fraud:payment:{payment.Id.Value}";
        var scoreValue = await _redis.GetDatabase().StringGetAsync(redisKey);
        double? fraudScore = null;
        if (scoreValue.HasValue)
        {
            if (double.TryParse((string)scoreValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                fraudScore = parsed;
            }
        }

        return PaymentResponse.FromPayment(payment, fraudScore);
    }
}