using PayFlow.Domain.Enums;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public sealed class InvalidPaymentTransitionException : DomainException
{
    public PaymentStatus From { get; }
    public string AttemptedEvent { get; }

    public InvalidPaymentTransitionException(PaymentStatus from, string attemptedEvent)
        : base($"Invalid transition from '{from}' via '{attemptedEvent}'")
    {
        From = from;
        AttemptedEvent = attemptedEvent;
    }
}

public sealed class InsufficientRefundableAmountException : DomainException
{
    public Money Requested { get; }
    public Money Available { get; }

    public InsufficientRefundableAmountException(Money requested, Money available)
        : base($"Refund amount {requested.Amount} {requested.Currency.Code} exceeds refundable amount {available.Amount} {available.Currency.Code}")
    {
        Requested = requested;
        Available = available;
    }
}

public sealed class TenantSuspendedException : DomainException
{
    public TenantSuspendedException() : base("Tenant is suspended") { }
}

public sealed class PaymentModeMismatchException : DomainException
{
    public PaymentMode ApiKeyMode { get; }
    public PaymentMode PaymentMode { get; }

    public PaymentModeMismatchException(PaymentMode apiKeyMode, PaymentMode paymentMode)
        : base($"API key mode '{apiKeyMode}' does not match payment mode '{paymentMode}'")
    {
        ApiKeyMode = apiKeyMode;
        PaymentMode = paymentMode;
    }
}

public sealed class PaymentNotFoundException : DomainException
{
    public PaymentNotFoundException() : base("Payment not found") { }
}

public sealed class RefundNotFoundException : DomainException
{
    public RefundNotFoundException() : base("Refund not found") { }
}

public sealed class InvalidPaymentStateException : DomainException
{
    public InvalidPaymentStateException(string message) : base(message) { }
}
