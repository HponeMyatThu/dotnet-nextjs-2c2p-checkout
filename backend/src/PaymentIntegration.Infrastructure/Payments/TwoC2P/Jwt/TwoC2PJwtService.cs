using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentIntegration.Infrastructure.Payments.TwoC2P.Options;

namespace PaymentIntegration.Infrastructure.Payments.TwoC2P.Jwt;

/// <summary>
/// RFC 7519 / RFC 7515 compliant JWT generator and verifier using standard System.Security.Cryptography.HMACSHA256.
/// This allows 2C2P Sandbox merchant keys of any length (such as the standard 12-char key "7jYcp4FxFdf0")
/// without being blocked by Microsoft.IdentityModel's arbitrary >= 128-bit key restriction.
/// </summary>
public class TwoC2PJwtService : ITwoC2PJwtService
{
    private readonly TwoC2POptions _options;
    private readonly ILogger<TwoC2PJwtService> _logger;
    private readonly byte[] _secretKeyBytes;

    public TwoC2PJwtService(IOptions<TwoC2POptions> options, ILogger<TwoC2PJwtService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _secretKeyBytes = Encoding.UTF8.GetBytes(_options.SecretKey);
    }

    public string SignPayload<T>(T payload)
    {
        var header = new { alg = "HS256", typ = "JWT" };
        var headerJson = JsonSerializer.Serialize(header);
        var headerEncoded = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));

        var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null // Preserve exact casing required by 2C2P
        });
        var payloadEncoded = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

        var stringToSign = $"{headerEncoded}.{payloadEncoded}";
        using var hmac = new HMACSHA256(_secretKeyBytes);
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
        var signatureEncoded = Base64UrlEncode(signatureBytes);

        return $"{stringToSign}.{signatureEncoded}";
    }

    public bool TryVerifyAndDecode<T>(string token, out T? result, out string? error)
    {
        result = default;
        if (!TryVerifyAndDecode(token, out var jsonNode, out error) || jsonNode == null)
        {
            return false;
        }

        try
        {
            result = jsonNode.Deserialize<T>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return result != null;
        }
        catch (Exception ex)
        {
            error = $"Failed to deserialize JWT claims to target type: {ex.Message}";
            _logger.LogError(ex, "Failed to deserialize JWT token payload.");
            return false;
        }
    }

    public bool TryVerifyAndDecode(string token, out JsonNode? jsonNode, out string? error)
    {
        jsonNode = null;
        error = null;

        if (string.IsNullOrWhiteSpace(token))
        {
            error = "JWT token is null or empty.";
            return false;
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            error = "JWT must contain header, payload, and signature separated by dots.";
            return false;
        }

        try
        {
            // 1. Verify header algorithm
            var headerBytes = Base64UrlDecode(parts[0]);
            var headerJson = Encoding.UTF8.GetString(headerBytes);
            var headerNode = JsonNode.Parse(headerJson);
            var alg = headerNode?["alg"]?.GetValue<string>();

            if (!string.Equals(alg, "HS256", StringComparison.OrdinalIgnoreCase))
            {
                error = $"Unsupported or invalid algorithm: {alg}. Only HS256 is accepted.";
                return false;
            }

            // 2. Verify HMAC SHA-256 signature
            var stringToSign = $"{parts[0]}.{parts[1]}";
            using var hmac = new HMACSHA256(_secretKeyBytes);
            var expectedSignatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
            var actualSignatureBytes = Base64UrlDecode(parts[2]);

            if (!CryptographicOperations.FixedTimeEquals(expectedSignatureBytes, actualSignatureBytes))
            {
                error = "Signature validation failed: HMAC signature does not match.";
                _logger.LogWarning("Invalid 2C2P JWT signature.");
                return false;
            }

            // 3. Decode payload
            var payloadBytes = Base64UrlDecode(parts[1]);
            var payloadJson = Encoding.UTF8.GetString(payloadBytes);
            jsonNode = JsonNode.Parse(payloadJson);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Malformed JWT token: {ex.Message}";
            _logger.LogError(ex, "Error validating 2C2P JWT token.");
            return false;
        }
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var output = input.Replace('-', '+').Replace('_', '/');
        switch (output.Length % 4)
        {
            case 2: output += "=="; break;
            case 3: output += "="; break;
        }
        return Convert.FromBase64String(output);
    }
}
