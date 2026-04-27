using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PayFlow.Domain.Events;
using PayFlow.Domain.ValueObjects;
using PayFlow.Application.Interfaces;

namespace PayFlow.Infrastructure.Dispatchers;

/// <summary>
/// No-op implementation for test environments where Hangfire is unavailable.
/// </summary>
public sealed class NoOpWebhookDispatcher : IWebhookDispatcher
{
    private readonly ILogger<NoOpWebhookDispatcher> _logger;

    public NoOpWebhookDispatcher(ILogger<NoOpWebhookDispatcher> logger)
    {
        _logger = logger;
    }

    public Task DispatchAsync(IDomainEvent domainEvent, TenantId tenantId, CancellationToken ct)
    {
        _logger.LogDebug(
            "NoOpWebhookDispatcher: skipping dispatch for {EventType} (test environment)", 
            domainEvent.GetType().Name);
        
        return Task.CompletedTask;
    }
}
