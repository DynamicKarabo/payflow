using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

using PayFlow.Application.Fraud;

namespace PayFlow.Infrastructure.Fraud;

public class FraudScoringService : IFraudScoringService
{
    private readonly HttpClient _httpClient;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<FraudScoringService> _logger;
    private readonly IDatabase _redisDb;

    public FraudScoringService(HttpClient httpClient, IConnectionMultiplexer redis, ILogger<FraudScoringService> logger)
    {
        _httpClient = httpClient;
        _redis = redis;
        _logger = logger;
        _redisDb = redis.GetDatabase();
    }

    public async Task<double> GetFraudScoreAsync(PaymentTransactionData transaction, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(transaction);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/score", content, ct);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("fraud_probability", out var probElem))
            {
                var score = probElem.GetDouble();
                // Cache in Redis for 30 days
                await _redisDb.StringSetAsync($"fraud:payment:{transaction.TransactionId}", score.ToString(), expiry: TimeSpan.FromDays(30));
                return score;
            }
            _logger.LogWarning("Fraud service response missing fraud_probability: {Response}", responseJson);
            return 0.0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to score fraud for transaction {TxId}", transaction.TransactionId);
            // Fail open: no fraud suspicion
            return 0.0;
        }
    }
}
