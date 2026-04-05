namespace PayFlow.Domain.ValueObjects;

public readonly record struct PaymentId(Guid Value);
public readonly record struct TenantId(Guid Value);
public readonly record struct RefundId(Guid Value);
public readonly record struct CustomerId(Guid Value);
public readonly record struct ApiKeyId(Guid Value);
public readonly record struct SettlementBatchId(Guid Value);
public readonly record struct WebhookDeliveryId(Guid Value);
public readonly record struct WebhookEndpointId(Guid Value);
