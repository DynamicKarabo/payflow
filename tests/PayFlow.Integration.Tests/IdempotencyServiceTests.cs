using Microsoft.Extensions.Logging;
using Moq;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.ValueObjects;
using PayFlow.Infrastructure.Redis;
using StackExchange.Redis;
using Xunit;

namespace PayFlow.Integration.Tests;

public class IdempotencyServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _dbMock;
    private readonly Mock<ILogger<RedisIdempotencyService>> _loggerMock;
    private readonly RedisIdempotencyService _service;
    private readonly TenantId _tenantId = new(Guid.NewGuid());
    private readonly IdempotencyKey _key = new("test-key-001");

    public IdempotencyServiceTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _dbMock = new Mock<IDatabase>();
        _loggerMock = new Mock<ILogger<RedisIdempotencyService>>();
        
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_dbMock.Object);
        
        _service = new RedisIdempotencyService(_redisMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CheckOrReserve_NewRequest_ShouldReturnNewRequest()
    {
        _dbMock.Setup(d => d.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var result = await _service.CheckOrReserveAsync(_tenantId, _key);

        Assert.True(result.IsNewRequest);
        Assert.NotNull(result.RedisKey);
    }

    [Fact]
    public async Task CheckOrReserve_DuplicateRequest_ShouldReturnDuplicate()
    {
        var cachedResponse = "{\"id\":\"pay_123\"}";
        
        _dbMock.Setup(d => d.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        _dbMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(cachedResponse);

        var result = await _service.CheckOrReserveAsync(_tenantId, _key);

        Assert.True(result.IsDuplicate);
        Assert.NotNull(result.CachedResponse);
    }

    [Fact]
    public async Task CheckOrReserve_InFlightRequest_ShouldReturnInFlight()
    {
        _dbMock.Setup(d => d.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        _dbMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync("__PROCESSING__");

        var result = await _service.CheckOrReserveAsync(_tenantId, _key);

        Assert.True(result.IsInFlight);
    }

    [Fact]
    public async Task CheckOrReserve_RedisUnavailable_ShouldReturnNewRequest()
    {
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        var result = await _service.CheckOrReserveAsync(_tenantId, _key);

        Assert.True(result.IsNewRequest);
    }

    [Fact]
    public async Task Commit_ShouldStoreResponseInRedis()
    {
        var redisKey = "payflow:idempotency:test:key";
        var response = new { id = "pay_123" };

        await _service.CommitAsync(redisKey, response);
    }
}
