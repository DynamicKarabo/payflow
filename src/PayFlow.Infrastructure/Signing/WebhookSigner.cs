using System.Security.Cryptography;
using System.Text;

namespace PayFlow.Infrastructure.Signing;

public interface IWebhookSigner
{
    (IDictionary<string, string> Headers, string Body) Sign(string payload, string secret);
    bool Verify(string payload, string signature, string secret, int toleranceSeconds = 300);
}

public sealed class HmacWebhookSigner : IWebhookSigner
{
    public (IDictionary<string, string> Headers, string Body) Sign(string payload, string secret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedContent = $"{timestamp}.{payload}";
        
        var signature = ComputeHmac(signedContent, secret);
        
        var headers = new Dictionary<string, string>
        {
            ["PayFlow-Signature"] = $"t={timestamp},v1={signature}",
            ["Content-Type"] = "application/json"
        };

        return (headers, payload);
    }

    public bool Verify(string payload, string signature, string secret, int toleranceSeconds = 300)
    {
        try
        {
            var parts = signature.Split(',');
            var timestampPart = parts.FirstOrDefault(p => p.StartsWith("t="));
            var signaturePart = parts.FirstOrDefault(p => p.StartsWith("v1="));

            if (timestampPart == null || signaturePart == null)
                return false;

            var timestamp = long.Parse(timestampPart[2..]);
            var providedSignature = signaturePart[3..];

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(now - timestamp) > toleranceSeconds)
                return false;

            var signedContent = $"{timestamp}.{payload}";
            var expectedSignature = ComputeHmac(signedContent, secret);

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSignature),
                Encoding.UTF8.GetBytes(providedSignature));
        }
        catch
        {
            return false;
        }
    }

    private static string ComputeHmac(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
