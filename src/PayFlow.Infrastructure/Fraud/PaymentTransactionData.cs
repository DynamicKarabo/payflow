namespace PayFlow.Infrastructure.Fraud;

public record PaymentTransactionData(
    string TransactionId,
    decimal Amount,
    string Currency,
    string Country,        // billing country or IP country; simplified to "SA" for now
    string DeviceId,
    string IpAddress,
    DateTimeOffset Timestamp);