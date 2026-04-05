using Microsoft.EntityFrameworkCore;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Entities;
using PayFlow.Domain.ValueObjects;
using PayFlow.Infrastructure.Persistence.Context;

namespace PayFlow.Infrastructure.Persistence.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly PayFlowDbContext _context;

    public PaymentRepository(PayFlowDbContext context)
    {
        _context = context;
    }

    public async Task<Payment?> GetByIdAsync(PaymentId id, CancellationToken ct = default)
    {
        return await _context.Payments
            .Include(p => p.Refunds)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<Payment?> GetByIdempotencyKeyAsync(TenantId tenantId, IdempotencyKey key, CancellationToken ct = default)
    {
        return await _context.Payments
            .Include(p => p.Refunds)
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.IdempotencyKey == key, ct);
    }

    public async Task AddAsync(Payment payment, CancellationToken ct = default)
    {
        await _context.Payments.AddAsync(payment, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Payment payment, CancellationToken ct = default)
    {
        _context.Payments.Update(payment);
        await _context.SaveChangesAsync(ct);
    }
}