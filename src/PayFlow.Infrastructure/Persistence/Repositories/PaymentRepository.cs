using Microsoft.EntityFrameworkCore;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Entities;
using PayFlow.Domain.ValueObjects;
using PayFlow.Infrastructure.Persistence.Context;
using PayFlow.Infrastructure.ServiceBus;

namespace PayFlow.Infrastructure.Persistence.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly PayFlowDbContext _context;
    private readonly IDomainEventPublisher _eventPublisher;

    public PaymentRepository(PayFlowDbContext context, IDomainEventPublisher eventPublisher)
    {
        _context = context;
        _eventPublisher = eventPublisher;
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
        
        // Publish domain events after save
        var events = payment.DomainEvents.ToList();
        if (events.Any())
        {
            payment.ClearDomainEvents();
            await _eventPublisher.PublishAsync(events, ct);
        }
    }

    public async Task UpdateAsync(Payment payment, CancellationToken ct = default)
    {
        _context.Payments.Update(payment);
        await _context.SaveChangesAsync(ct);
        
        // Publish domain events after save
        var events = payment.DomainEvents.ToList();
        if (events.Any())
        {
            payment.ClearDomainEvents();
            await _eventPublisher.PublishAsync(events, ct);
        }
    }
}