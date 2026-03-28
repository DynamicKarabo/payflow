# PayFlow — Webhook Delivery

## Overview

Webhooks notify tenant systems of payment lifecycle events in near-real-time. The delivery system is decoupled from the API request path via Azure Service Bus, uses HMAC-SHA256 to sign every payload, and retries failed deliveries with exponential backoff via Hangfire.

---

## Architecture

```
Payment State Change
        │
        ▼
Domain Event dispatched (post-commit)
        │
        ▼
Service Bus Publisher
  Topic: payment.events
  Message: {EventType, TenantId, Payload}
        │
        ▼
Webhook Dispatcher (Service Bus Consumer — Hangfire Worker)
  Subscription: webhook-delivery
        │
        ▼
  1. Load tenant WebhookConfig
  2. Check EventSubscriptions — skip if not subscribed
  3. Enqueue WebhookDeliveryJob (Hangfire)
        │
        ▼
WebhookDeliveryJob (Hangfire)
  1. Build canonical payload
  2. Sign with HMAC-SHA256
  3. POST to tenant endpoint
  4. Record delivery attempt
        │
  ┌─────┴──────┐
  │ Success    │ Failure
  │ (2xx)      │ (non-2xx / timeout)
  │            │
  ▼            ▼
Mark Delivered  Schedule retry (exponential backoff)
                Max 7 attempts → Mark Dead
```

---

## Event Types

| Event Type | Trigger |
|---|---|
| `payment.created` | Payment persisted in `Created` state |
| `payment.authorised` | Gateway authorisation succeeded |
| `payment.captured` | Payment captured |
| `payment.settled` | Payment moved to `Settled` by batch job |
| `payment.failed` | Payment failed at any stage |
| `payment.cancelled` | Payment cancelled |
| `refund.created` | Refund record created |
| `refund.succeeded` | Gateway refund confirmed |
| `refund.failed` | Gateway refund failed |

---

## Payload Schema

```json
{
  "id": "evt_01HX...",
  "type": "payment.captured",
  "api_version": "2026-03-01",
  "created": 1743158400,
  "livemode": true,
  "data": {
    "object": {
      "id": "pay_...",
      "status": "captured",
      "amount": 4999,
      "currency": "GBP",
      "customer_id": "cus_...",
      "gateway_reference": "ch_...",
      "mode": "live",
      "created_at": "2026-03-28T10:00:00Z",
      "metadata": { "order_id": "ord_8823" }
    }
  }
}
```

All numeric amounts are in minor units. `livemode` mirrors the payment's `Mode`.

---

## HMAC-SHA256 Signing

Every delivery is signed so the receiving endpoint can verify authenticity.

### Signature Construction

```
timestamp = Unix epoch seconds at delivery time (string)
payload   = raw JSON body bytes (UTF-8)

signed_content = timestamp + "." + payload

signature = HMAC-SHA256(key=tenant.WebhookConfig.HmacSecret, data=signed_content)
            → hex-encoded lowercase
```

### Request Headers

```
POST https://tenant-endpoint.example.com/webhooks
Content-Type: application/json
PayFlow-Signature: t=1743158400,v1=3d9f2a...
PayFlow-Event-ID: evt_01HX...
```

`PayFlow-Signature` format: `t={timestamp},v1={hex_signature}`

The `t=` value is included in the signed content to prevent replay attacks. Tenants should reject events where `|now - t| > 300` seconds.

### Verification Example (Receiver Side)

```csharp
public static bool VerifySignature(
    string rawBody,
    string signatureHeader,
    string secret,
    int toleranceSeconds = 300)
{
    var parts = signatureHeader.Split(',');
    var timestamp = parts.First(p => p.StartsWith("t="))[2..];
    var v1 = parts.First(p => p.StartsWith("v1="))[3..];

    if (!long.TryParse(timestamp, out var ts) ||
        Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts) > toleranceSeconds)
        return false;

    var signedContent = $"{timestamp}.{rawBody}";
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var expected = Convert.ToHexString(
        hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent))).ToLower();

    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(expected),
        Encoding.UTF8.GetBytes(v1));
}
```

`CryptographicOperations.FixedTimeEquals` prevents timing attacks on the comparison.

---

## Retry Policy

Hangfire manages retries. The backoff schedule is fixed-delay exponential with jitter:

| Attempt | Delay | Cumulative |
|---|---|---|
| 1 (initial) | immediate | 0s |
| 2 | 30s | 30s |
| 3 | 5m | ~5.5m |
| 4 | 30m | ~36m |
| 5 | 2h | ~2.6h |
| 6 | 5h | ~7.6h |
| 7 | 24h | ~31.6h |

After attempt 7, the delivery is marked `Dead`. Dead events are retained for 7 days and can be manually re-enqueued via the admin API.

### Hangfire Job

```csharp
public sealed class WebhookDeliveryJob(
    IWebhookDeliveryRepository repo,
    IHttpClientFactory httpFactory,
    IWebhookSigner signer,
    ILogger<WebhookDeliveryJob> logger)
{
    [AutomaticRetry(Attempts = 0)]  // Retry schedule managed manually below
    public async Task ExecuteAsync(
        WebhookDeliveryId deliveryId,
        CancellationToken ct)
    {
        var delivery = await repo.GetAsync(deliveryId, ct);
        if (delivery.Status == WebhookDeliveryStatus.Delivered) return;

        var (headers, body) = signer.Sign(delivery.Payload);

        using var client = httpFactory.CreateClient("WebhookClient");
        client.Timeout = TimeSpan.FromSeconds(10);

        HttpResponseMessage? response = null;
        try
        {
            response = await client.PostAsync(
                delivery.EndpointUrl, 
                new StringContent(body, Encoding.UTF8, "application/json"),
                ct);
        }
        catch (Exception ex)
        {
            await RecordFailureAndRescheduleAsync(delivery, ex.Message, ct);
            return;
        }

        if (response.IsSuccessStatusCode)
        {
            await repo.MarkDeliveredAsync(deliveryId, (int)response.StatusCode, ct);
            return;
        }

        await RecordFailureAndRescheduleAsync(
            delivery, $"HTTP {(int)response.StatusCode}", ct);
    }

    private async Task RecordFailureAndRescheduleAsync(
        WebhookDelivery delivery, string reason, CancellationToken ct)
    {
        delivery.RecordAttempt(failed: true, reason);

        if (delivery.AttemptCount >= 7)
        {
            await repo.MarkDeadAsync(delivery.Id, ct);
            logger.LogWarning("Webhook delivery {Id} exhausted retries", delivery.Id);
            return;
        }

        var delay = GetBackoffDelay(delivery.AttemptCount);
        BackgroundJob.Schedule<WebhookDeliveryJob>(
            j => j.ExecuteAsync(delivery.Id, CancellationToken.None),
            delay);

        await repo.SaveAsync(delivery, ct);
    }

    private static TimeSpan GetBackoffDelay(int attemptCount) => attemptCount switch
    {
        1 => TimeSpan.FromSeconds(30),
        2 => TimeSpan.FromMinutes(5),
        3 => TimeSpan.FromMinutes(30),
        4 => TimeSpan.FromHours(2),
        5 => TimeSpan.FromHours(5),
        _ => TimeSpan.FromHours(24)
    };
}
```

---

## Webhook Delivery Record

```csharp
public sealed class WebhookDelivery : Entity
{
    public WebhookDeliveryId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string EventId { get; private set; }
    public string EventType { get; private set; }
    public string EndpointUrl { get; private set; }
    public string Payload { get; private set; }         // serialised JSON
    public WebhookDeliveryStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public int? LastHttpStatus { get; private set; }
    public string? LastFailureReason { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

public enum WebhookDeliveryStatus { Pending, Delivered, Dead }
```

---

## Tenant Webhook Configuration

Tenants configure webhooks via:

```
POST /v1/webhook-endpoints
{
  "url": "https://example.com/payflow-webhook",
  "events": ["payment.captured", "refund.created"]
}

→ 201 Created
{
  "id": "we_...",
  "url": "https://example.com/payflow-webhook",
  "secret": "whsec_...",    ← shown once; store immediately
  "events": ["payment.captured", "refund.created"],
  "status": "enabled"
}
```

The `secret` is the HMAC signing key shown once at creation. PayFlow stores only the encrypted version. If lost, the tenant rotates it via `POST /v1/webhook-endpoints/{id}/rotate-secret`.

---

## Security Considerations

- HMAC secret is encrypted at rest using AES-256 (envelope encryption via Azure Key Vault).
- Webhook delivery is always over HTTPS — plaintext HTTP endpoints are rejected at registration.
- TLS certificate validation is enforced; self-signed certificates are not permitted in live mode.
- The delivery job runs in an isolated Hangfire worker, not in the API process.
- Event payloads contain no full PAN or CVV data — only tokenised/masked payment method info.
