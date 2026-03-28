using System.Text.Json;
using Microsoft.Extensions.Logging;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.ValueObjects;
using StackExchange.Redis;

namespace PayFlow.Infrastructure.Redis;

public class RedisIdempotencyService : IIdempotencyService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisIdempotencyService> _logger;
    private readonly string _keyPrefix;
    private readonly TimeSpan _defaultTtl = TimeSpan.FromHours(24);

    private const string ProcessingSentinel = "__PROCESSING__";

    public RedisIdempotencyService(
        IConnectionMultiplexer redis,
        ILogger<RedisIdempotencyService> logger,
        string keyPrefix = "payflow:idempotency:")
    {
        _redis = redis;
        _logger = logger;
        _keyPrefix = keyPrefix;
    }

    public async Task<IdempotencyResult> CheckOrReserveAsync(
        TenantId tenantId, 
        IdempotencyKey key, 
        CancellationToken ct = default)
    {
        var redisKey = $"{_keyPrefix}{tenantId.Value}:{key.Value}";

        try
        {
            var db = _redis.GetDatabase();

            var claimed = await db.StringSetAsync(
                redisKey,
                ProcessingSentinel,
                expiry: _defaultTtl,
                when: When.NotExists);

            if (!claimed)
            {
                var existingValue = await db.StringGetAsync(redisKey);
                
                if (existingValue == ProcessingSentinel)
                {
                    return IdempotencyResult.InFlight();
                }

                if (!existingValue.IsNullOrEmpty)
                {
                    try
                    {
                        var cached = JsonSerializer.Deserialize<object>(existingValue!);
                        return IdempotencyResult.Duplicate(cached);
                    }
                    catch (JsonException)
                    {
                        _logger.LogWarning("Failed to deserialize cached response for key {Key}", redisKey);
                        return IdempotencyResult.NewRequest(redisKey);
                    }
                }

                return IdempotencyResult.NewRequest(redisKey);
            }

            return IdempotencyResult.NewRequest(redisKey);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable - proceeding without idempotency check");
            return IdempotencyResult.NewRequest(redisKey);
        }
    }

    public async Task CommitAsync(string redisKey, object response, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();

        try
        {
            var serialized = JsonSerializer.Serialize(response);
            await db.StringSetAsync(
                redisKey,
                serialized,
                expiry: _defaultTtl);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable - failed to cache response for key {Key}", redisKey);
        }
    }
}
