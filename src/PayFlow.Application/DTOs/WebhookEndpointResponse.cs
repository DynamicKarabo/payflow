using PayFlow.Domain.Entities;

namespace PayFlow.Application.DTOs;

public record WebhookEndpointResponse(
    string Id,
    string Url,
    string Status,
    IReadOnlyList<string> EventTypes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastRotatedAt)
{
    public static WebhookEndpointResponse FromWebhookEndpoint(WebhookEndpoint endpoint) => new(
        Id: $"wh_{endpoint.Id.Value:N}",
        Url: endpoint.Url,
        Status: endpoint.Status.ToString().ToLowerInvariant(),
        EventTypes: endpoint.GetEventTypesList(),
        CreatedAt: endpoint.CreatedAt,
        LastRotatedAt: endpoint.LastRotatedAt);
}

public record WebhookDeliveryResponse(
    string Id,
    string EventType,
    string EndpointUrl,
    string Status,
    int AttemptCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeliveredAt)
{
    public static WebhookDeliveryResponse FromWebhookDelivery(WebhookDelivery delivery) => new(
        Id: $"whd_{delivery.Id.Value:N}",
        EventType: delivery.EventType,
        EndpointUrl: delivery.EndpointUrl,
        Status: delivery.Status.ToString().ToLowerInvariant(),
        AttemptCount: delivery.AttemptCount,
        CreatedAt: delivery.CreatedAt,
        DeliveredAt: delivery.DeliveredAt);
}