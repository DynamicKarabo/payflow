using PayFlow.Domain.Events;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Infrastructure.Dispatchers;

public interface IWebhookDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, TenantId tenantId, CancellationToken ct);
}