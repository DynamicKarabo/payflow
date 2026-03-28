namespace PayFlow.Domain.ValueObjects;

public readonly record struct Currency(string Code)
{
    public static readonly Currency GBP = new("GBP");
    public static readonly Currency USD = new("USD");
    public static readonly Currency EUR = new("EUR");

    public int DecimalPlaces => Code switch
    {
        "JPY" or "KRW" => 0,
        _ => 2
    };

    public static Currency FromCode(string code) => new(code);
}

public readonly record struct Money
{
    public decimal Amount { get; }
    public Currency Currency { get; }

    public Money(decimal amount, Currency currency)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative", nameof(amount));

        Amount = amount;
        Currency = currency;
    }

    public static Money Zero(Currency currency) => new(0, currency);

    public bool IsPositive => Amount > 0;

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot add money with different currencies");
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot subtract money with different currencies");
        if (other.Amount > Amount)
            throw new InvalidOperationException("Result would be negative");
        return new Money(Amount - other.Amount, Currency);
    }
}
