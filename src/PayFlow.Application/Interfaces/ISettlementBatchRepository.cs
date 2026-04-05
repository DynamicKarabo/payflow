using PayFlow.Domain.Entities;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Application.Interfaces;

public interface ISettlementBatchRepository
{
    Task<SettlementBatch?> GetByIdAsync(SettlementBatchId id, CancellationToken ct = default);
    Task<IReadOnlyList<SettlementBatch>> GetByTenantAsync(TenantId tenantId, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken ct = default);
    Task AddAsync(SettlementBatch batch, CancellationToken ct = default);
    Task UpdateAsync(SettlementBatch batch, CancellationToken ct = default);
}