using System.Text.RegularExpressions;

namespace PayFlow.Domain.ValueObjects;

public readonly record struct IdempotencyKey
{
    private static readonly Regex ValidKeyPattern = new(@"^[a-zA-Z0-9\-]{1,64}$", RegexOptions.Compiled);

    public string Value { get; }

    public IdempotencyKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Idempotency key cannot be empty", nameof(value));
        
        if (!ValidKeyPattern.IsMatch(value))
            throw new ArgumentException("Idempotency key must be alphanumeric with hyphens, max 64 chars", nameof(value));

        Value = value;
    }

    public static implicit operator string(IdempotencyKey key) => key.Value;
    public static explicit operator IdempotencyKey(string value) => new(value);
}
