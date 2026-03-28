using PayFlow.Domain.Enums;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Domain.Entities;

public sealed class ApiKey : Entity
{
    public ApiKeyId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string KeyPrefix { get; private set; }
    public string HashedSecret { get; private set; }
    public PaymentMode Mode { get; private set; }
    public ApiKeyStatus Status { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }

    private ApiKey() { }

    public ApiKey(ApiKeyId id, TenantId tenantId, string keyPrefix, string hashedSecret, PaymentMode mode, DateTimeOffset? expiresAt = null)
    {
        Id = id;
        TenantId = tenantId;
        KeyPrefix = keyPrefix;
        HashedSecret = hashedSecret;
        Mode = mode;
        Status = ApiKeyStatus.Active;
        ExpiresAt = expiresAt;
    }

    public bool IsExpired()
    {
        return ExpiresAt.HasValue && ExpiresAt.Value < DateTimeOffset.UtcNow;
    }

    public void Revoke()
    {
        Status = ApiKeyStatus.Revoked;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool IsValid()
    {
        return Status == ApiKeyStatus.Active && !IsExpired();
    }
}
