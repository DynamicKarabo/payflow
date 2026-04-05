using PayFlow.Domain.Entities;

namespace PayFlow.Application.DTOs;

public record SettlementBatchResponse(
    string Id,
    string SettlementDate,
    decimal GrossAmount,
    decimal FeeAmount,
    decimal NetAmount,
    string Currency,
    int PaymentCount,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt)
{
    public static SettlementBatchResponse FromSettlementBatch(SettlementBatch batch) => new(
        Id: $"set_{batch.Id.Value:N}",
        SettlementDate: batch.SettlementDate.ToString("yyyy-MM-dd"),
        GrossAmount: batch.GrossAmountValue,
        FeeAmount: batch.FeeAmountValue,
        NetAmount: batch.NetAmountValue,
        Currency: batch.GrossCurrencyCode,
        PaymentCount: batch.PaymentCount,
        Status: batch.Status.ToString().ToLowerInvariant(),
        CreatedAt: batch.CreatedAt,
        CompletedAt: batch.CompletedAt);
}