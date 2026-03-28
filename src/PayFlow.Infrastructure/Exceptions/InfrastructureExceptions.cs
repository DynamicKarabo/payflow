using PayFlow.Domain.Exceptions;

namespace PayFlow.Infrastructure.Exceptions;

public sealed class PaymentConcurrencyConflictException : DomainException
{
    public PaymentConcurrencyConflictException()
        : base("The payment was modified by another request. Please retry.") { }
}

public sealed class RedisUnavailableException : DomainException
{
    public RedisUnavailableException(string message) : base(message) { }
}
