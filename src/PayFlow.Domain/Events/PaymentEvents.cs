using PayFlow.Domain.Enums;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Domain.Events;

public sealed record PaymentCreated(
    PaymentId PaymentId,
    TenantId TenantId,
    Money Amount,
    PaymentMode Mode,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public PaymentCreated(PaymentId paymentId, TenantId tenantId, Money amount, PaymentMode mode) 
        : this(paymentId, tenantId, amount, mode, DateTimeOffset.UtcNow) { }
}

public sealed record PaymentAuthorised(
    PaymentId PaymentId,
    TenantId TenantId,
    string GatewayReference,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public PaymentAuthorised(PaymentId paymentId, TenantId tenantId, string gatewayReference)
        : this(paymentId, tenantId, gatewayReference, DateTimeOffset.UtcNow) { }
}

public sealed record PaymentCaptured(
    PaymentId PaymentId,
    TenantId TenantId,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public PaymentCaptured(PaymentId paymentId, TenantId tenantId)
        : this(paymentId, tenantId, DateTimeOffset.UtcNow) { }
}

public sealed record PaymentSettled(
    PaymentId PaymentId,
    TenantId TenantId,
    Money Amount,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public PaymentSettled(PaymentId paymentId, TenantId tenantId, Money amount)
        : this(paymentId, tenantId, amount, DateTimeOffset.UtcNow) { }
}

public sealed record PaymentFailed(
    PaymentId PaymentId,
    TenantId TenantId,
    string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public PaymentFailed(PaymentId paymentId, TenantId tenantId, string reason)
        : this(paymentId, tenantId, reason, DateTimeOffset.UtcNow) { }
}

public sealed record PaymentCancelled(
    PaymentId PaymentId,
    TenantId TenantId,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public PaymentCancelled(PaymentId paymentId, TenantId tenantId)
        : this(paymentId, tenantId, DateTimeOffset.UtcNow) { }
}

public sealed record RefundCreated(
    RefundId RefundId,
    PaymentId PaymentId,
    TenantId TenantId,
    Money Amount,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public RefundCreated(RefundId refundId, PaymentId paymentId, TenantId tenantId, Money amount)
        : this(refundId, paymentId, tenantId, amount, DateTimeOffset.UtcNow) { }
}
