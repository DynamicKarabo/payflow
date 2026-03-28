namespace PayFlow.Domain.Enums;

public enum PaymentStatus
{
    Created,
    Authorised,
    Captured,
    Settled,
    Failed,
    Cancelled
}

public enum PaymentMode
{
    Test,
    Live
}

public enum RefundStatus
{
    Pending,
    Succeeded,
    Failed
}

public enum TenantStatus
{
    Active,
    Suspended,
    Closed
}

public enum ApiKeyStatus
{
    Active,
    Revoked
}
