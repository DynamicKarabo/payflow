using Microsoft.EntityFrameworkCore;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Entities;
using PayFlow.Domain.ValueObjects;
using PayFlow.Infrastructure.Persistence.Context;

namespace PayFlow.Infrastructure.Persistence.Repositories;

public class WebhookEndpointRepository : IWebhookEndpointRepository
{
    private readonly PayFlowDbContext _context;

    public WebhookEndpointRepository(PayFlowDbContext context)
    {
        _context = context;
    }

    public async Task<WebhookEndpoint?> GetByIdAsync(WebhookEndpointId id, CancellationToken ct = default)
    {
        return await _context.WebhookEndpoints
            .FirstOrDefaultAsync(w => w.Id == id, ct);
    }

    public async Task<IReadOnlyList<WebhookEndpoint>> GetByTenantAsync(TenantId tenantId, CancellationToken ct = default)
    {
        var endpoints = await _context.WebhookEndpoints
            .Where(w => w.TenantId == tenantId)
            .ToListAsync(ct);
        return endpoints.AsReadOnly();
    }

    public async Task<IReadOnlyList<WebhookEndpoint>> GetActiveByTenantAndEventTypeAsync(TenantId tenantId, string eventType, CancellationToken ct = default)
    {
        var endpoints = await _context.WebhookEndpoints
            .Where(w => w.TenantId == tenantId && w.Status == WebhookEndpointStatus.Active)
            .ToListAsync(ct);

        return endpoints
            .Where(w => w.SubscribesTo(eventType))
            .ToList()
            .AsReadOnly();
    }

    public async Task AddAsync(WebhookEndpoint endpoint, CancellationToken ct = default)
    {
        await _context.WebhookEndpoints.AddAsync(endpoint, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(WebhookEndpoint endpoint, CancellationToken ct = default)
    {
        _context.WebhookEndpoints.Update(endpoint);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(WebhookEndpointId id, CancellationToken ct = default)
    {
        var endpoint = await _context.WebhookEndpoints.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (endpoint != null)
        {
            _context.WebhookEndpoints.Remove(endpoint);
            await _context.SaveChangesAsync(ct);
        }
    }
}