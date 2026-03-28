using PayFlow.Domain.Enums;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Domain.Entities;

public sealed class WebhookDelivery : Entity
{
    public WebhookDeliveryId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string EventId { get; private set; }
    public string EventType { get; private set; }
    public string EndpointUrl { get; private set; }
    public string Payload { get; private set; }
    public WebhookDeliveryStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public int? LastHttpStatus { get; private set; }
    public string? LastFailureReason { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }

    private WebhookDelivery() { }

    public WebhookDelivery(TenantId tenantId, string eventId, string eventType, string endpointUrl, string payload)
    {
        Id = new WebhookDeliveryId(Guid.NewGuid());
        TenantId = tenantId;
        EventId = eventId;
        EventType = eventType;
        EndpointUrl = endpointUrl;
        Payload = payload;
        Status = WebhookDeliveryStatus.Pending;
        AttemptCount = 0;
        NextAttemptAt = DateTimeOffset.UtcNow;
    }

    public void RecordAttempt(bool failed, string? reason)
    {
        AttemptCount++;
        if (failed)
        {
            LastFailureReason = reason;
            Status = WebhookDeliveryStatus.Pending;
        }
    }

    public void MarkDelivered(int httpStatus)
    {
        Status = WebhookDeliveryStatus.Delivered;
        LastHttpStatus = httpStatus;
        DeliveredAt = DateTimeOffset.UtcNow;
    }

    public void MarkDead()
    {
        Status = WebhookDeliveryStatus.Dead;
    }

    public void ScheduleRetry(DateTimeOffset nextAttempt)
    {
        NextAttemptAt = nextAttempt;
    }
}

public enum WebhookDeliveryStatus
{
    Pending,
    Delivered,
    Dead
}
