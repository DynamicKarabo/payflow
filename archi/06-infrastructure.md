# PayFlow — Infrastructure

---

## Redis

### Purpose

Redis serves two functions in PayFlow: idempotency key storage and distributed locking.

### Configuration

```csharp
// appsettings.json
{
  "Redis": {
    "ConnectionString": "localhost:6379,abortConnect=false,connectTimeout=5000",
    "InstanceName": "payflow:"
  }
}

// Program.cs
builder.Services.AddStackExchangeRedisCache(options => {
    options.Configuration = config["Redis:ConnectionString"];
    options.InstanceName = config["Redis:InstanceName"];
});
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(config["Redis:ConnectionString"]));
```

### Key Namespacing

All keys are prefixed with `payflow:` (set via `InstanceName`) to avoid collisions when sharing a Redis instance.

| Key Pattern | Purpose | TTL |
|---|---|---|
| `payflow:idempotency:{tenantId}:{key}` | Idempotency cache | 24h |
| `payflow:lock:settle:{tenantId}:{date}` | Settlement job mutex | 5m |
| `payflow:tenant:{tenantId}:config` | Tenant config cache | 5m |

### Resilience

Redis is treated as **non-critical infrastructure**. If Redis is unavailable:
- Idempotency checks are skipped (logged at Warning level) — this trades safety for availability under degraded conditions; this behaviour is configurable via `Redis:FallbackOnUnavailable`.
- Tenant config falls back to direct SQL query.

The `IConnectionMultiplexer` connection pool handles reconnection automatically. `abortConnect=false` prevents startup failure if Redis is temporarily unreachable.

---

## Azure Service Bus

### Purpose

Service Bus decouples domain event publishing from downstream consumers (webhook dispatcher, audit logger, analytics sink).

### Topology

```
Topic: payflow.payment.events
  ├── Subscription: webhook-delivery
  │     Filter: ALL events
  ├── Subscription: audit-log
  │     Filter: ALL events
  └── Subscription: analytics
        Filter: payment.captured, payment.settled, refund.succeeded
```

### Message Schema

```json
{
  "messageId": "evt_01HX...",
  "correlationId": "pay_...",
  "subject": "payment.captured",
  "body": {
    "eventType": "payment.captured",
    "tenantId": "ten_...",
    "occurredAt": "2026-03-28T10:00:00Z",
    "payload": { ... }
  }
}
```

`correlationId` is set to the `PaymentId` for easy message chain tracing in Application Insights.

### Publisher

```csharp
public sealed class ServiceBusDomainEventPublisher(
    ServiceBusSender sender,
    ILogger<ServiceBusDomainEventPublisher> logger) : IDomainEventPublisher
{
    public async Task PublishAsync(
        IEnumerable<IDomainEvent> events, CancellationToken ct)
    {
        var messages = events.Select(e => new ServiceBusMessage(
            BinaryData.FromObjectAsJson(new EventEnvelope(e)))
        {
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = GetCorrelationId(e),
            Subject = GetEventType(e)
        });

        await sender.SendMessagesAsync(messages, ct);
    }
}
```

### Consumer (Webhook Dispatcher)

```csharp
// Registered as a hosted service
public sealed class WebhookDispatcherConsumer(
    ServiceBusProcessor processor,
    IBackgroundJobClient jobs) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        processor.ProcessMessageAsync += async args =>
        {
            var envelope = args.Message.Body.ToObjectFromJson<EventEnvelope>();
            jobs.Enqueue<WebhookDeliveryJob>(
                j => j.ExecuteAsync(envelope.TenantId, envelope.EventType,
                                    envelope.Payload, CancellationToken.None));
            await args.CompleteMessageAsync(args.Message);
        };

        processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(args.Exception, "Service Bus error");
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(ct);
        await Task.Delay(Timeout.Infinite, ct);
        await processor.StopProcessingAsync();
    }
}
```

### Dead-Letter Queue

Messages that fail processing after 10 delivery attempts are moved to the dead-letter sub-queue. A separate Hangfire recurring job checks the DLQ every 30 minutes and creates an alert if unprocessed messages accumulate.

---

## Hangfire

### Purpose

Hangfire manages:
- Webhook delivery jobs (enqueued on demand)
- Refund gateway jobs (enqueued on demand)
- Nightly settlement batch job (recurring)
- DLQ inspection job (recurring)

### Storage

Hangfire uses SQL Server as its backing store (same server, separate `HangfireSchema` schema). This avoids an extra infrastructure dependency in development.

```csharp
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        SchemaName = "HangfireSchema",
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));

builder.Services.AddHangfireServer(options =>
{
    options.Queues = new[] { "webhook", "refund", "settlement", "default" };
    options.WorkerCount = 10;
});
```

### Queue Priority

| Queue | Jobs | Workers |
|---|---|---|
| `webhook` | `WebhookDeliveryJob` | 6 |
| `refund` | `RefundGatewayJob` | 2 |
| `settlement` | `SettlementBatchJob` | 1 |
| `default` | Everything else | 1 |

### Dashboard

Hangfire Dashboard is mounted at `/admin/hangfire` with `LocalRequestsOnlyAuthorizationFilter` in development and `HmacAuthorizationFilter` in production.

### Recurring Jobs Registration

```csharp
// Registered in IHostedService.StartAsync
app.Services.GetRequiredService<IRecurringJobManager>()
    .AddOrUpdate<SettlementBatchJob>(
        "settlement-nightly",
        j => j.ExecuteAsync(CancellationToken.None),
        "30 0 * * *",           // 00:30 UTC daily
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
```

---

## Docker & Compose

### Services

```yaml
# docker-compose.yml
services:
  api:
    build: ./src/PayFlow.Api
    ports: ["8080:8080"]
    depends_on: [sqlserver, redis, servicebus-emulator]
    environment:
      - ASPNETCORE_ENVIRONMENT=Development

  worker:
    build: ./src/PayFlow.Worker     # Hangfire server process (separate from API)
    depends_on: [sqlserver, redis, servicebus-emulator]

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    ports: ["1433:1433"]
    environment:
      SA_PASSWORD: "Dev_Password1!"
      ACCEPT_EULA: "Y"
    volumes:
      - sqlserver-data:/var/opt/mssql

  redis:
    image: redis:7-alpine
    ports: ["6379:6379"]
    command: redis-server --save "" --appendonly no   # dev: no persistence

  servicebus-emulator:
    image: mcr.microsoft.com/azure-messaging/servicebus-emulator:latest
    ports: ["5672:5672", "5300:5300"]
    environment:
      ACCEPT_EULA: "Y"
    volumes:
      - ./infra/servicebus-config.json:/ServiceBus_Emulator/ConfigFiles/Config.json

volumes:
  sqlserver-data:
```

### Override for Local Dev

```yaml
# docker-compose.override.yml
services:
  api:
    volumes:
      - ~/.aspnet/https:/https:ro     # dev cert
    environment:
      - ASPNETCORE_URLS=https://+:8443;http://+:8080
      - ASPNETCORE_Kestrel__Certificates__Default__Path=/https/aspnetapp.pfx
```

### Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "sqlserver")
    .AddRedis(redisConnectionString, name: "redis")
    .AddAzureServiceBusTopic(connectionString, "payflow.payment.events", name: "servicebus")
    .AddHangfire(options => options.MinimumAvailableServers = 1, name: "hangfire");

app.MapHealthChecks("/health/ready",
    new HealthCheckOptions { Predicate = _ => true });
app.MapHealthChecks("/health/live",
    new HealthCheckOptions { Predicate = _ => false });   // liveness: just 200
```

---

## Configuration & Secrets

| Setting | Source (Dev) | Source (Prod) |
|---|---|---|
| SQL Server connection string | `appsettings.Development.json` | Azure Key Vault |
| Redis connection string | `appsettings.Development.json` | Azure Key Vault |
| Service Bus connection string | `appsettings.Development.json` | Managed Identity |
| Webhook HMAC secrets | Encrypted in DB (AES-256) | AES key from Key Vault |
| API key hash salt | `appsettings.Development.json` | Azure Key Vault |

In production, `DefaultAzureCredential` (Managed Identity) is used for all Azure resource authentication — no connection strings with passwords in environment variables.
