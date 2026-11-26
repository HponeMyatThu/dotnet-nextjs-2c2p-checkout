using System.Text.Json.Nodes;

namespace PaymentIntegration.Infrastructure.Payments.TwoC2P.Jwt;

public interface ITwoC2PJwtService
{
    /// <summary>
    /// Signs a payload dictionary/object into a JWT using HMAC SHA-256 with the merchant secret key.
    /// </summary>
    string SignPayload<T>(T payload);

    /// <summary>
    /// Verifies the HMAC SHA-256 signature and decodes the JWT payload into the requested type.
    /// Returns false if the signature is invalid or payload is corrupted.
    /// </summary>
    bool TryVerifyAndDecode<T>(string token, out T? result, out string? error);

    /// <summary>
    /// Verifies the HMAC SHA-256 signature and decodes the JWT payload into a JsonNode.
    /// </summary>
    bool TryVerifyAndDecode(string token, out JsonNode? jsonNode, out string? error);
}
