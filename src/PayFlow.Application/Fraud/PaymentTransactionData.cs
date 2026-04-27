namespace PayFlow.Application.Fraud;

public record PaymentTransactionData(
    string TransactionId,
    decimal Amount,
    string Currency,
    string Country,
    string DeviceId,
    string IpAddress,
    System.DateTimeOffset Timestamp);