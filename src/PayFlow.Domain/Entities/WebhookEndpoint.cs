using PayFlow.Domain.ValueObjects;

namespace PayFlow.Domain.Entities;

public sealed class WebhookEndpoint : AggregateRoot
{
    public WebhookEndpointId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Url { get; private set; } = null!;
    public string Secret { get; private set; } = null!;
    public WebhookEndpointStatus Status { get; private set; }
    public string EventTypes { get; private set; } = null!; // Comma-separated list
    public DateTimeOffset? LastRotatedAt { get; private set; }

    private WebhookEndpoint() { }

    private WebhookEndpoint(WebhookEndpointId id, TenantId tenantId, string url, string secret, string eventTypes)
    {
        Id = id;
        TenantId = tenantId;
        Url = url;
        Secret = secret;
        Status = WebhookEndpointStatus.Active;
        EventTypes = eventTypes;
    }

    public static WebhookEndpoint Create(TenantId tenantId, string url, string secret, IEnumerable<string> eventTypes)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL is required", nameof(url));

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https"))
            throw new ArgumentException("URL must be HTTPS", nameof(url));

        if (string.IsNullOrWhiteSpace(secret))
            throw new ArgumentException("Secret is required", nameof(secret));

        var eventTypesString = string.Join(",", eventTypes.Select(e => e.Trim().ToLowerInvariant()));
        if (string.IsNullOrWhiteSpace(eventTypesString))
            throw new ArgumentException("At least one event type is required", nameof(eventTypes));

        return new WebhookEndpoint(
            new WebhookEndpointId(Guid.NewGuid()),
            tenantId,
            url,
            secret,
            eventTypesString);
    }

    public void UpdateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL is required", nameof(url));

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https"))
            throw new ArgumentException("URL must be HTTPS", nameof(url));

        Url = url;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateEventTypes(IEnumerable<string> eventTypes)
    {
        var eventTypesString = string.Join(",", eventTypes.Select(e => e.Trim().ToLowerInvariant()));
        if (string.IsNullOrWhiteSpace(eventTypesString))
            throw new ArgumentException("At least one event type is required", nameof(eventTypes));

        EventTypes = eventTypesString;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RotateSecret(string newSecret)
    {
        if (string.IsNullOrWhiteSpace(newSecret))
            throw new ArgumentException("Secret is required", nameof(newSecret));

        Secret = newSecret;
        LastRotatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Disable()
    {
        Status = WebhookEndpointStatus.Disabled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Enable()
    {
        Status = WebhookEndpointStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool IsActive => Status == WebhookEndpointStatus.Active;

    public bool SubscribesTo(string eventType)
    {
        return EventTypes.Split(',').Contains(eventType.ToLowerInvariant());
    }

    public IReadOnlyList<string> GetEventTypesList()
    {
        return EventTypes.Split(',').ToList().AsReadOnly();
    }
}

public enum WebhookEndpointStatus
{
    Active,
    Disabled
}