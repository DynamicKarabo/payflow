using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Enums;
using PayFlow.Domain.ValueObjects;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace PayFlow.Infrastructure.Gateways;

public sealed class RealPaymentGatewayAdapter : IPaymentGatewayAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RealPaymentGatewayAdapter> _logger;
    private readonly ResiliencePipeline<HttpResponseMessage> _resiliencePipeline;

    public RealPaymentGatewayAdapter(
        HttpClient httpClient,
        ILogger<RealPaymentGatewayAdapter> logger,
        string gatewayBaseUrl)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(gatewayBaseUrl);
        _logger = logger;
        _resiliencePipeline = BuildResiliencePipeline();
    }

    private static ResiliencePipeline<HttpResponseMessage> BuildResiliencePipeline()
    {
        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    return default;
                }
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(10)
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(30),
                OnOpened = args =>
                {
                    return default;
                },
                OnClosed = args =>
                {
                    return default;
                }
            })
            .Build();
    }

    public async Task<GatewayAuthoriseResult> AuthoriseAsync(AuthoriseRequest request, CancellationToken ct)
    {
        try
        {
            var response = await _resiliencePipeline.ExecuteAsync(
                async token => await _httpClient.PostAsJsonAsync("/authorise", request, token),
                ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GatewayResponse>(ct);
                return new GatewayAuthoriseResult(
                    Succeeded: true,
                    GatewayReference: result?.Reference,
                    FailureReason: null,
                    FailureCode: null);
            }

            var error = await response.Content.ReadFromJsonAsync<GatewayErrorResponse>(ct);
            return new GatewayAuthoriseResult(
                Succeeded: false,
                GatewayReference: null,
                FailureReason: error?.Message ?? "Unknown error",
                FailureCode: error?.Code);
        }
        catch (TimeoutRejectedException)
        {
            _logger.LogWarning("Gateway timeout during authorise");
            return new GatewayAuthoriseResult(
                Succeeded: false,
                GatewayReference: null,
                FailureReason: "Gateway timeout",
                FailureCode: "timeout");
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("Circuit breaker open - gateway unavailable");
            return new GatewayAuthoriseResult(
                Succeeded: false,
                GatewayReference: null,
                FailureReason: "Gateway unavailable",
                FailureCode: "circuit_open");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during gateway authorise");
            return new GatewayAuthoriseResult(
                Succeeded: false,
                GatewayReference: null,
                FailureReason: "Internal error",
                FailureCode: "internal_error");
        }
    }

    public async Task<GatewayCaptureResult> CaptureAsync(string gatewayReference, Money amount, CancellationToken ct)
    {
        try
        {
            var response = await _resiliencePipeline.ExecuteAsync(
                async token => await _httpClient.PostAsJsonAsync("/capture", new { Reference = gatewayReference, Amount = amount.Amount }, token),
                ct);

            if (response.IsSuccessStatusCode)
            {
                return new GatewayCaptureResult(Succeeded: true, FailureReason: null, FailureCode: null);
            }

            var error = await response.Content.ReadFromJsonAsync<GatewayErrorResponse>(ct);
            return new GatewayCaptureResult(
                Succeeded: false,
                FailureReason: error?.Message ?? "Unknown error",
                FailureCode: error?.Code);
        }
        catch (TimeoutRejectedException)
        {
            return new GatewayCaptureResult(Succeeded: false, FailureReason: "Gateway timeout", FailureCode: "timeout");
        }
        catch (BrokenCircuitException)
        {
            return new GatewayCaptureResult(Succeeded: false, FailureReason: "Gateway unavailable", FailureCode: "circuit_open");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during gateway capture");
            return new GatewayCaptureResult(Succeeded: false, FailureReason: "Internal error", FailureCode: "internal_error");
        }
    }

    public async Task<GatewayRefundResult> RefundAsync(string gatewayReference, Money amount, CancellationToken ct)
    {
        try
        {
            var response = await _resiliencePipeline.ExecuteAsync(
                async token => await _httpClient.PostAsJsonAsync("/refund", new { Reference = gatewayReference, Amount = amount.Amount }, token),
                ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GatewayResponse>(ct);
                return new GatewayRefundResult(
                    Succeeded: true,
                    GatewayReference: result?.Reference,
                    FailureReason: null,
                    FailureCode: null);
            }

            var error = await response.Content.ReadFromJsonAsync<GatewayErrorResponse>(ct);
            return new GatewayRefundResult(
                Succeeded: false,
                GatewayReference: null,
                FailureReason: error?.Message ?? "Unknown error",
                FailureCode: error?.Code);
        }
        catch (TimeoutRejectedException)
        {
            return new GatewayRefundResult(Succeeded: false, GatewayReference: null, FailureReason: "Gateway timeout", FailureCode: "timeout");
        }
        catch (BrokenCircuitException)
        {
            return new GatewayRefundResult(Succeeded: false, GatewayReference: null, FailureReason: "Gateway unavailable", FailureCode: "circuit_open");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during gateway refund");
            return new GatewayRefundResult(Succeeded: false, GatewayReference: null, FailureReason: "Internal error", FailureCode: "internal_error");
        }
    }

    private record GatewayResponse(string? Reference);
    private record GatewayErrorResponse(string? Code, string? Message);
}

public sealed class StubPaymentGatewayAdapter : IPaymentGatewayAdapter
{
    private readonly ILogger<StubPaymentGatewayAdapter> _logger;
    private readonly bool _simulateSuccess;

    public StubPaymentGatewayAdapter(ILogger<StubPaymentGatewayAdapter> logger, bool simulateSuccess = true)
    {
        _logger = logger;
        _simulateSuccess = simulateSuccess;
    }

    public Task<GatewayAuthoriseResult> AuthoriseAsync(AuthoriseRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Stub gateway: Authorise {Amount} {Currency}", request.Amount.Amount, request.Currency.Code);
        
        if (_simulateSuccess)
        {
            return Task.FromResult(new GatewayAuthoriseResult(
                Succeeded: true,
                GatewayReference: $"stub_{Guid.NewGuid():N}",
                FailureReason: null,
                FailureCode: null));
        }

        return Task.FromResult(new GatewayAuthoriseResult(
            Succeeded: false,
            GatewayReference: null,
            FailureReason: "Card declined (stub)",
            FailureCode: "card_declined"));
    }

    public Task<GatewayCaptureResult> CaptureAsync(string gatewayReference, Money amount, CancellationToken ct)
    {
        _logger.LogInformation("Stub gateway: Capture {Reference}", gatewayReference);
        
        if (_simulateSuccess)
        {
            return Task.FromResult(new GatewayCaptureResult(Succeeded: true, FailureReason: null, FailureCode: null));
        }

        return Task.FromResult(new GatewayCaptureResult(Succeeded: false, FailureReason: "Capture failed (stub)", FailureCode: "capture_failed"));
    }

    public Task<GatewayRefundResult> RefundAsync(string gatewayReference, Money amount, CancellationToken ct)
    {
        _logger.LogInformation("Stub gateway: Refund {Reference} {Amount}", gatewayReference, amount.Amount);
        
        if (_simulateSuccess)
        {
            return Task.FromResult(new GatewayRefundResult(
                Succeeded: true,
                GatewayReference: $"refund_{Guid.NewGuid():N}",
                FailureReason: null,
                FailureCode: null));
        }

        return Task.FromResult(new GatewayRefundResult(
            Succeeded: false,
            GatewayReference: null,
            FailureReason: "Refund failed (stub)",
            FailureCode: "refund_failed"));
    }
}
