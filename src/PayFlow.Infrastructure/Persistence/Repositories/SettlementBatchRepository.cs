using Microsoft.EntityFrameworkCore;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Entities;
using PayFlow.Domain.ValueObjects;
using PayFlow.Infrastructure.Persistence.Context;

namespace PayFlow.Infrastructure.Persistence.Repositories;

public class SettlementBatchRepository : ISettlementBatchRepository
{
    private readonly PayFlowDbContext _context;

    public SettlementBatchRepository(PayFlowDbContext context)
    {
        _context = context;
    }

    public async Task<SettlementBatch?> GetByIdAsync(SettlementBatchId id, CancellationToken ct = default)
    {
        return await _context.SettlementBatches
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<IReadOnlyList<SettlementBatch>> GetByTenantAsync(TenantId tenantId, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken ct = default)
    {
        var query = _context.SettlementBatches
            .Where(s => s.TenantId == tenantId);

        if (fromDate.HasValue)
        {
            query = query.Where(s => s.SettlementDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(s => s.SettlementDate <= toDate.Value);
        }

        var batches = await query
            .OrderByDescending(s => s.SettlementDate)
            .ToListAsync(ct);

        return batches.AsReadOnly();
    }

    public async Task AddAsync(SettlementBatch batch, CancellationToken ct = default)
    {
        await _context.SettlementBatches.AddAsync(batch, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(SettlementBatch batch, CancellationToken ct = default)
    {
        _context.SettlementBatches.Update(batch);
        await _context.SaveChangesAsync(ct);
    }
}