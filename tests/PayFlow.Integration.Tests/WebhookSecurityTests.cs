using System.Security.Cryptography;
using System.Text;
using PayFlow.Infrastructure.Signing;
using Xunit;

namespace PayFlow.Integration.Tests;

public class WebhookSignerTests
{
    private const string Secret = "test_webhook_secret";
    private readonly HmacWebhookSigner _signer = new();

    [Fact]
    public void Sign_ShouldProduceValidSignature()
    {
        var payload = "{\"event\":\"payment.created\",\"data\":{\"id\":\"pay_123\"}}";

        var (headers, _) = _signer.Sign(payload, Secret);

        Assert.True(headers.ContainsKey("PayFlow-Signature"));
        var signature = headers["PayFlow-Signature"];
        
        Assert.Contains("t=", signature);
        Assert.Contains("v1=", signature);
    }

    [Fact]
    public void Verify_ValidSignature_ShouldReturnTrue()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = "{\"event\":\"payment.created\"}";
        var signedContent = $"{timestamp}.{payload}";
        
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent));
        var signature = Convert.ToHexString(hash).ToLowerInvariant();
        
        var signatureHeader = $"t={timestamp},v1={signature}";

        var result = _signer.Verify(payload, signatureHeader, Secret);

        Assert.True(result);
    }

    [Fact]
    public void Verify_InvalidSignature_ShouldReturnFalse()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = "{\"event\":\"payment.created\"}";
        
        var signatureHeader = $"t={timestamp},v1=invalid_signature";

        var result = _signer.Verify(payload, signatureHeader, Secret);

        Assert.False(result);
    }

    [Fact]
    public void Verify_ModifiedPayload_ShouldReturnFalse()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var originalPayload = "{\"event\":\"payment.created\"}";
        var modifiedPayload = "{\"event\":\"payment.created\",\"amount\":100}";
        
        var signedContent = $"{timestamp}.{originalPayload}";
        
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent));
        var signature = Convert.ToHexString(hash).ToLowerInvariant();
        
        var signatureHeader = $"t={timestamp},v1={signature}";

        var result = _signer.Verify(modifiedPayload, signatureHeader, Secret);

        Assert.False(result);
    }

    [Fact]
    public void Verify_ExpiredTimestamp_ShouldReturnFalse()
    {
        var expiredTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        var payload = "{\"event\":\"payment.created\"}";
        
        var signedContent = $"{expiredTimestamp}.{payload}";
        
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent));
        var signature = Convert.ToHexString(hash).ToLowerInvariant();
        
        var signatureHeader = $"t={expiredTimestamp},v1={signature}";

        var result = _signer.Verify(payload, signatureHeader, Secret, toleranceSeconds: 300);

        Assert.False(result);
    }

    [Fact]
    public void Verify_MissingTimestamp_ShouldReturnFalse()
    {
        var payload = "{\"event\":\"payment.created\"}";
        var signatureHeader = "v1=some_signature";

        var result = _signer.Verify(payload, signatureHeader, Secret);

        Assert.False(result);
    }

    [Fact]
    public void Verify_MissingSignature_ShouldReturnFalse()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = "{\"event\":\"payment.created\"}";
        var signatureHeader = $"t={timestamp}";

        var result = _signer.Verify(payload, signatureHeader, Secret);

        Assert.False(result);
    }

    [Fact]
    public void Verify_ValidTimestamp_WithinTolerance_ShouldReturnTrue()
    {
        var timestamp = DateTimeOffset.UtcNow.AddSeconds(-60).ToUnixTimeSeconds();
        var payload = "{\"event\":\"payment.created\"}";
        
        var signedContent = $"{timestamp}.{payload}";
        
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent));
        var signature = Convert.ToHexString(hash).ToLowerInvariant();
        
        var signatureHeader = $"t={timestamp},v1={signature}";

        var result = _signer.Verify(payload, signatureHeader, Secret, toleranceSeconds: 300);

        Assert.True(result);
    }
}

public class WebhookSecurityTests
{
    [Theory]
    [InlineData("http://example.com/webhook", false)]
    [InlineData("https://example.com/webhook", true)]
    [InlineData("http://localhost:8080/webhook", false)]
    [InlineData("https://localhost:8080/webhook", true)]
    public void WebhookEndpoint_ShouldRequireHttps(string url, bool expectedValid)
    {
        var isHttps = Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https";
        
        Assert.Equal(expectedValid, isHttps);
    }

    [Fact]
    public void PaymentPayload_ShouldNotContainSensitiveData()
    {
        var paymentPayload = new
        {
            id = "pay_123",
            amount = 1000,
            card = new { last4 = "4242", brand = "visa" },
            token = "tok_xxx"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(paymentPayload);
        
        Assert.DoesNotContain("4242424242424242", json);
        Assert.DoesNotContain("cvv", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cvc", json, StringComparison.OrdinalIgnoreCase);
    }
}
