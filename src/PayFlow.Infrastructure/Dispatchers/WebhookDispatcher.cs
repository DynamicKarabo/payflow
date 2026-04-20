using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Events;
using PayFlow.Domain.ValueObjects;
using PayFlow.Infrastructure.Jobs;
using PayFlow.Infrastructure.Persistence.Context;

namespace PayFlow.Infrastructure.Dispatchers;

public sealed class WebhookDispatcher : IWebhookDispatcher
{
    private readonly AdminDbContext _dbContext;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<WebhookDispatcher> _logger;

    public WebhookDispatcher(
        AdminDbContext dbContext,
        IBackgroundJobClient backgroundJobClient,
        ILogger<WebhookDispatcher> logger)
    {
        _dbContext = dbContext;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public async Task DispatchAsync(IDomainEvent domainEvent, TenantId tenantId, CancellationToken ct)
    {
        var eventType = domainEvent.GetType().Name;

        // Find active webhook endpoints that subscribe to this event type
        var endpoints = await _dbContext.AllWebhookEndpoints
            .Where(e => e.TenantId == tenantId && e.IsActive)
            .ToListAsync(ct);

        var matchingEndpoints = endpoints
            .Where(e => e.SubscribesTo(eventType))
            .ToList();

        if (matchingEndpoints.Count == 0)
        {
            _logger.LogDebug("No webhook endpoints subscribed to {EventType} for tenant {TenantId}", 
                eventType, tenantId.Value);
            return;
        }

        // Serialize the event payload
        var payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType());
        var eventId = Guid.NewGuid().ToString();

        // Create webhook deliveries for each matching endpoint
        foreach (var endpoint in matchingEndpoints)
        {
            var delivery = new WebhookDelivery(
                tenantId,
                eventId,
                eventType,
                endpoint.Url,
                payload);

            _dbContext.WebhookDeliveries.Add(delivery);
        }

        await _dbContext.SaveChangesAsync(ct);

        // Schedule delivery jobs via Hangfire
        var deliveries = await _dbContext.AllWebhookDeliveries
            .Where(d => d.EventId == eventId && d.TenantId == tenantId)
            .ToListAsync(ct);

        foreach (var delivery in deliveries)
        {
            _backgroundJobClient.Enqueue<WebhookDeliveryJob>(
                job => job.ExecuteAsync(delivery.Id, CancellationToken.None));

            _logger.LogInformation("Scheduled webhook delivery {DeliveryId} for event {EventType} to {EndpointUrl}",
                delivery.Id.Value, eventType, delivery.EndpointUrl);
        }

        _logger.LogInformation("Dispatched {Count} webhook deliveries for event {EventType}", 
            deliveries.Count, eventType);
    }
}