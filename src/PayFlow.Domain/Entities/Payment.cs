using PayFlow.Domain.Enums;
using PayFlow.Domain.Events;
using PayFlow.Domain.Exceptions;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Domain.Entities;

public sealed class Payment : AggregateRoot
{
    public PaymentId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public IdempotencyKey IdempotencyKey { get; private set; }
    public decimal Amount { get; private set; }
    public Currency Currency { get; private set; }
    public PaymentStatus Status { get; private set; }
    public PaymentMode Mode { get; private set; }
    public CustomerId CustomerId { get; private set; }
    public PaymentMethodSnapshot PaymentMethod { get; private set; }
    public string? GatewayReference { get; private set; }
    public string? FailureReason { get; private set; }

    private readonly List<Refund> _refunds = new();
    public IReadOnlyList<Refund> Refunds => _refunds.AsReadOnly();

    private Payment() { }

    private Payment(PaymentId id, TenantId tenantId, IdempotencyKey idempotencyKey, 
        decimal amount, Currency currency, PaymentMode mode, CustomerId customerId,
        PaymentMethodSnapshot paymentMethod)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));

        Id = id;
        TenantId = tenantId;
        IdempotencyKey = idempotencyKey;
        Amount = amount;
        Currency = currency;
        Mode = mode;
        Status = PaymentStatus.Created;
        CustomerId = customerId;
        PaymentMethod = paymentMethod;
    }

    public static Payment Create(TenantId tenantId, IdempotencyKey idempotencyKey,
        decimal amount, Currency currency, PaymentMode mode, CustomerId customerId,
        PaymentMethodSnapshot paymentMethod)
    {
        var payment = new Payment(
            new PaymentId(Guid.NewGuid()),
            tenantId,
            idempotencyKey,
            amount,
            currency,
            mode,
            customerId,
            paymentMethod);

        payment.RaiseDomainEvent(new PaymentCreated(payment.Id, tenantId, new Money(amount, currency), mode));

        return payment;
    }

    public decimal RefundableAmount
    {
        get
        {
            var refunded = _refunds
                .Where(r => r.Status != RefundStatus.Failed)
                .Sum(r => r.Amount);
            return Amount - refunded;
        }
    }

    public void Authorise(string gatewayReference)
    {
        if (Status != PaymentStatus.Created)
            throw new InvalidPaymentTransitionException(Status, "Authorise");

        GatewayReference = gatewayReference ?? throw new ArgumentNullException(nameof(gatewayReference));
        Status = PaymentStatus.Authorised;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new PaymentAuthorised(Id, TenantId, gatewayReference));
    }

    public void Capture()
    {
        if (Status != PaymentStatus.Authorised)
            throw new InvalidPaymentTransitionException(Status, "Capture");

        Status = PaymentStatus.Captured;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new PaymentCaptured(Id, TenantId));
    }

    public void Settle()
    {
        if (Status != PaymentStatus.Captured)
            throw new InvalidPaymentTransitionException(Status, "Settle");

        Status = PaymentStatus.Settled;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new PaymentSettled(Id, TenantId, new Money(Amount, Currency)));
    }

    public void Fail(string reason)
    {
        if (Status is PaymentStatus.Settled or PaymentStatus.Cancelled or PaymentStatus.Failed)
            throw new InvalidPaymentTransitionException(Status, "Fail");

        FailureReason = reason ?? "Unknown failure";
        Status = PaymentStatus.Failed;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new PaymentFailed(Id, TenantId, reason));
    }

    public void Cancel()
    {
        if (Status is PaymentStatus.Captured or PaymentStatus.Settled)
            throw new InvalidPaymentTransitionException(Status, "Cancel");

        Status = PaymentStatus.Cancelled;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new PaymentCancelled(Id, TenantId));
    }

    public Refund Refund(decimal amount, string reason)
    {
        if (Status != PaymentStatus.Settled)
            throw new InvalidPaymentTransitionException(Status, "Refund");

        if (amount > RefundableAmount)
            throw new InsufficientRefundableAmountException(
                new Money(amount, Currency),
                new Money(RefundableAmount, Currency));

        var refund = new Refund(
            new RefundId(Guid.NewGuid()),
            Id,
            TenantId,
            new Money(amount, Currency),
            reason);

        _refunds.Add(refund);
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new RefundCreated(refund.Id, Id, TenantId, new Money(amount, Currency)));

        return refund;
    }

    public void ApplyDomainEvents()
    {
        ClearDomainEvents();
    }
}
