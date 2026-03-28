using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using PayFlow.Domain.Events;
using PayFlow.Domain.ValueObjects;

namespace PayFlow.Infrastructure.ServiceBus;

public interface IDomainEventPublisher
{
    Task PublishAsync(IEnumerable<IDomainEvent> events, CancellationToken ct);
}

public sealed class ServiceBusDomainEventPublisher : IDomainEventPublisher
{
    private readonly ServiceBusSender _sender;
    private readonly ILogger<ServiceBusDomainEventPublisher> _logger;

    public ServiceBusDomainEventPublisher(
        ServiceBusSender sender,
        ILogger<ServiceBusDomainEventPublisher> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task PublishAsync(IEnumerable<IDomainEvent> events, CancellationToken ct)
    {
        var eventList = events.ToList();
        if (!eventList.Any()) return;

        var messages = eventList.Select(e => CreateMessage(e)).ToList();

        try
        {
            await _sender.SendMessagesAsync(messages, ct);
            _logger.LogDebug("Published {Count} domain events", messages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish domain events");
            throw;
        }
    }

    private static ServiceBusMessage CreateMessage(IDomainEvent domainEvent)
    {
        var envelope = new EventEnvelope
        {
            EventType = domainEvent.GetType().Name,
            OccurredAt = domainEvent.OccurredAt,
            Payload = JsonSerializer.SerializeToElement(domainEvent)
        };

        var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(envelope))
        {
            MessageId = Guid.NewGuid().ToString(),
            Subject = envelope.EventType
        };

        if (domainEvent is IPaymentEvent paymentEvent)
        {
            message.CorrelationId = paymentEvent.PaymentId.Value.ToString();
        }

        return message;
    }

    private record EventEnvelope
    {
        public string EventType { get; init; } = string.Empty;
        public DateTimeOffset OccurredAt { get; init; }
        public JsonElement Payload { get; init; }
    }
}

public interface IPaymentEvent
{
    PaymentId PaymentId { get; }
}
