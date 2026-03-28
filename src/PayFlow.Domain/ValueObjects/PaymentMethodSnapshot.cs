namespace PayFlow.Domain.ValueObjects;

public sealed record PaymentMethodSnapshot
{
    public string Type { get; init; }
    public string? Last4 { get; init; }
    public string? Brand { get; init; }
    public string? ExpiryMonth { get; init; }
    public string? ExpiryYear { get; init; }
    public string? BankName { get; init; }

    public PaymentMethodSnapshot(string type, string? last4 = null, string? brand = null, 
        string? expiryMonth = null, string? expiryYear = null, string? bankName = null)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Last4 = last4;
        Brand = brand;
        ExpiryMonth = expiryMonth;
        ExpiryYear = expiryYear;
        BankName = bankName;
    }
}
