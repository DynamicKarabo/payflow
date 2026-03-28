namespace PayFlow.Domain.Events;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
