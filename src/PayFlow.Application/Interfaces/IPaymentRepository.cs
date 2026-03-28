using PayFlow.Domain.Entities;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Application.Interfaces;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(PaymentId id, CancellationToken ct = default);
    Task<Payment?> GetByIdempotencyKeyAsync(TenantId tenantId, IdempotencyKey key, CancellationToken ct = default);
    Task AddAsync(Payment payment, CancellationToken ct = default);
    Task UpdateAsync(Payment payment, CancellationToken ct = default);
}
