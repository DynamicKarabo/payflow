using PayFlow.Domain.ValueObjects;

namespace PayFlow.Application.Interfaces;

public interface IIdempotencyService
{
    Task<IdempotencyResult> CheckOrReserveAsync(TenantId tenantId, IdempotencyKey key, CancellationToken ct = default);
    Task CommitAsync(string redisKey, object response, CancellationToken ct = default);
}

public abstract class IdempotencyResult
{
    public bool IsNewRequest { get; protected set; }
    public bool IsDuplicate { get; protected set; }
    public bool IsInFlight { get; protected set; }
    public object? CachedResponse { get; protected set; }
    public string? RedisKey { get; protected set; }

    private IdempotencyResult() { }

    public static IdempotencyResult NewRequest(string redisKey) => new NewRequestResult { RedisKey = redisKey };
    public static IdempotencyResult Duplicate(object? response) => new DuplicateResult { CachedResponse = response };
    public static IdempotencyResult InFlight() => new InFlightResult();

    private sealed class NewRequestResult : IdempotencyResult
    {
        public NewRequestResult() { IsNewRequest = true; }
    }

    private sealed class DuplicateResult : IdempotencyResult
    {
        public DuplicateResult() { IsDuplicate = true; }
    }

    private sealed class InFlightResult : IdempotencyResult
    {
        public InFlightResult() { IsInFlight = true; }
    }
}
