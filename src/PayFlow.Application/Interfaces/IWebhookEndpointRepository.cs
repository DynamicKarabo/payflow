using PayFlow.Domain.Entities;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Application.Interfaces;

public interface IWebhookEndpointRepository
{
    Task<WebhookEndpoint?> GetByIdAsync(WebhookEndpointId id, CancellationToken ct = default);
    Task<IReadOnlyList<WebhookEndpoint>> GetByTenantAsync(TenantId tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<WebhookEndpoint>> GetActiveByTenantAndEventTypeAsync(TenantId tenantId, string eventType, CancellationToken ct = default);
    Task AddAsync(WebhookEndpoint endpoint, CancellationToken ct = default);
    Task UpdateAsync(WebhookEndpoint endpoint, CancellationToken ct = default);
    Task DeleteAsync(WebhookEndpointId id, CancellationToken ct = default);
}