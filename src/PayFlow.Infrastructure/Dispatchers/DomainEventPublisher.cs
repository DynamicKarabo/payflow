using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;
using PayFlow.Domain.Events;
using PayFlow.Domain.ValueObjects;
using PayFlow.Infrastructure.ServiceBus;

namespace PayFlow.Infrastructure.Dispatchers;

/// <summary>
/// In-process implementation of IDomainEventPublisher that dispatches events to webhooks.
/// This replaces ServiceBusDomainEventPublisher for local/dev use when Azure Service Bus is not available.
/// </summary>
public sealed class DomainEventPublisher : IDomainEventPublisher
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> _tenantIdPropertyCache = new();

    private readonly IWebhookDispatcher _webhookDispatcher;
    private readonly ILogger<DomainEventPublisher> _logger;

    public DomainEventPublisher(
        IWebhookDispatcher webhookDispatcher,
        ILogger<DomainEventPublisher> logger)
    {
        _webhookDispatcher = webhookDispatcher;
        _logger = logger;
    }

    public async Task PublishAsync(IEnumerable<IDomainEvent> events, CancellationToken ct)
    {
        var eventList = events.ToList();
        if (!eventList.Any()) return;

        _logger.LogDebug("Publishing {Count} domain events", eventList.Count);

        foreach (var domainEvent in eventList)
        {
            // Extract tenant ID from the event
            TenantId? tenantId = ExtractTenantId(domainEvent);
            
            if (tenantId == null)
            {
                _logger.LogError("Could not extract tenant ID from event {EventType}. Webhook dispatch skipped.",
                    domainEvent.GetType().Name);
                continue;
            }

            try
            {
                await _webhookDispatcher.DispatchAsync(domainEvent, tenantId.Value, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch webhook for event {EventType}", 
                    domainEvent.GetType().Name);
                // Don't throw - we want to continue processing other events
            }
        }
    }

    private static TenantId? ExtractTenantId(IDomainEvent domainEvent)
    {
        var eventType = domainEvent.GetType();
        var property = _tenantIdPropertyCache.GetOrAdd(eventType,
            t => t.GetProperty("TenantId", BindingFlags.Public | BindingFlags.Instance));

        if (property?.GetValue(domainEvent) is TenantId tenantId)
        {
            return tenantId;
        }

        return null;
    }
}