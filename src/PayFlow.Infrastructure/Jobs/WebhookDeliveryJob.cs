using System.Text;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;
using PayFlow.Domain.ValueObjects;
using PayFlow.Infrastructure.Persistence.Context;
using PayFlow.Infrastructure.Signing;

namespace PayFlow.Infrastructure.Jobs;

public class WebhookDeliveryJob
{
    private readonly AdminDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebhookSigner _signer;
    private readonly ILogger<WebhookDeliveryJob> _logger;

    public WebhookDeliveryJob(
        AdminDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IWebhookSigner signer,
        ILogger<WebhookDeliveryJob> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _signer = signer;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(WebhookDeliveryId deliveryId, CancellationToken ct)
    {
        var delivery = await _dbContext.AllWebhookDeliveries
            .FirstOrDefaultAsync(w => w.Id == deliveryId, ct);

        if (delivery == null)
        {
            _logger.LogWarning("Webhook delivery {Id} not found", deliveryId);
            return;
        }

        if (delivery.Status == WebhookDeliveryStatus.Delivered)
        {
            _logger.LogInformation("Webhook delivery {Id} already delivered", deliveryId);
            return;
        }

        var (headers, body) = _signer.Sign(delivery.Payload, "tenant_secret_placeholder");

        using var client = _httpClientFactory.CreateClient("WebhookClient");
        client.Timeout = TimeSpan.FromSeconds(10);

        HttpResponseMessage? response = null;
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, delivery.EndpointUrl)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            foreach (var header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            response = await client.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deliver webhook {Id}", deliveryId);
            await RecordFailureAndRescheduleAsync(delivery, ex.Message, ct);
            return;
        }

        if (response.IsSuccessStatusCode)
        {
            delivery.MarkDelivered((int)response.StatusCode);
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("Webhook delivery {Id} succeeded", deliveryId);
            return;
        }

        await RecordFailureAndRescheduleAsync(delivery, $"HTTP {(int)response.StatusCode}", ct);
    }

    private async Task RecordFailureAndRescheduleAsync(WebhookDelivery delivery, string reason, CancellationToken ct)
    {
        delivery.RecordAttempt(failed: true, reason);

        if (delivery.AttemptCount >= 7)
        {
            delivery.MarkDead();
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogWarning("Webhook delivery {Id} exhausted retries", delivery.Id);
            return;
        }

        var delay = GetBackoffDelay(delivery.AttemptCount);
        delivery.ScheduleRetry(DateTimeOffset.UtcNow.Add(delay));

        await _dbContext.SaveChangesAsync(ct);

        BackgroundJob.Schedule<WebhookDeliveryJob>(
            j => j.ExecuteAsync(delivery.Id, CancellationToken.None),
            delay);

        _logger.LogInformation("Webhook delivery {Id} scheduled for retry in {Delay}", delivery.Id, delay);
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
