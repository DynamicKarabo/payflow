using FluentValidation;
using MediatR;
using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;
using PayFlow.Domain.Exceptions;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Application.Commands;

public sealed record CreatePaymentCommand(
    decimal Amount,
    string Currency,
    string CustomerId,
    PaymentMethodRequest PaymentMethod,
    bool AutoCapture,
    Dictionary<string, string>? Metadata) : IRequest<PaymentResponse>;

public sealed record PaymentMethodRequest(
    string Type,
    string? Token,
    string? Last4,
    string? Brand,
    string? ExpiryMonth,
    string? ExpiryYear);

public sealed class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than zero");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3)
            .WithMessage("Currency must be a 3-letter ISO code");

        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("Customer ID is required");

        RuleFor(x => x.PaymentMethod)
            .NotNull()
            .WithMessage("Payment method is required");

        RuleFor(x => x.PaymentMethod.Type)
            .NotEmpty()
            .WithMessage("Payment method type is required");
    }
}

public sealed class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, PaymentResponse>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentGatewayAdapter _gatewayAdapter;
    private readonly IIdempotencyService _idempotencyService;
    private readonly ITenantContext _tenantContext;

    public CreatePaymentCommandHandler(
        IPaymentRepository paymentRepository,
        IPaymentGatewayAdapter gatewayAdapter,
        IIdempotencyService idempotencyService,
        ITenantContext tenantContext)
    {
        _paymentRepository = paymentRepository;
        _gatewayAdapter = gatewayAdapter;
        _idempotencyService = idempotencyService;
        _tenantContext = tenantContext;
    }

    public async Task<PaymentResponse> Handle(CreatePaymentCommand request, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId;
        var mode = _tenantContext.Mode;

        var currency = Currency.FromCode(request.Currency);
        var amount = new Money(request.Amount, currency);
        var customerId = new CustomerId(Guid.Parse(request.CustomerId));
        
        var idempotencyKey = new IdempotencyKey(request.Metadata?.GetValueOrDefault("idempotency_key") ?? Guid.NewGuid().ToString());

        var idempotencyResult = await _idempotencyService.CheckOrReserveAsync(tenantId, idempotencyKey, ct);

        if (idempotencyResult.IsInFlight)
        {
            throw new PaymentInFlightException();
        }

        if (idempotencyResult.IsDuplicate)
        {
            throw new IdempotencyConflictException();
        }

        var existingPayment = await _paymentRepository.GetByIdempotencyKeyAsync(tenantId, idempotencyKey, ct);
        if (existingPayment != null)
        {
            throw new IdempotencyConflictException();
        }

        var paymentMethod = new PaymentMethodSnapshot(
            request.PaymentMethod.Type,
            request.PaymentMethod.Last4,
            request.PaymentMethod.Brand,
            request.PaymentMethod.ExpiryMonth,
            request.PaymentMethod.ExpiryYear);

        var payment = Payment.Create(
            tenantId,
            idempotencyKey,
            amount,
            currency,
            mode,
            customerId,
            paymentMethod);

        var authoriseRequest = new AuthoriseRequest(
            amount,
            currency,
            request.PaymentMethod.Token ?? string.Empty,
            customerId);

        var authoriseResult = await _gatewayAdapter.AuthoriseAsync(authoriseRequest, ct);

        if (authoriseResult.Succeeded)
        {
            payment.Authorise(authoriseResult.GatewayReference!);

            if (request.AutoCapture)
            {
                payment.Capture();
            }
        }
        else
        {
            payment.Fail(authoriseResult.FailureReason ?? "Unknown failure");
        }

        await _paymentRepository.AddAsync(payment, ct);

        if (idempotencyResult.RedisKey != null)
        {
            var response = PaymentResponse.FromPayment(payment);
            await _idempotencyService.CommitAsync(idempotencyResult.RedisKey, response, ct);
        }

        return PaymentResponse.FromPayment(payment);
    }
}

public class PaymentInFlightException : DomainException
{
    public PaymentInFlightException() : base("Payment is already being processed") { }
}

public class IdempotencyConflictException : DomainException
{
    public IdempotencyConflictException() : base("Idempotency key conflict") { }
}
