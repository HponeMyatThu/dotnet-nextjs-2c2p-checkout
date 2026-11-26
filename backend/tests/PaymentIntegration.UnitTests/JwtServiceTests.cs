using System.IdentityModel.Tokens.Jwt;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PaymentIntegration.Infrastructure.Payments.TwoC2P.Jwt;
using PaymentIntegration.Infrastructure.Payments.TwoC2P.Options;

namespace PaymentIntegration.UnitTests;

public class JwtServiceTests
{
    private readonly TwoC2PJwtService _sut;
    private const string SecretKey = "test-secret-key-for-unit-tests-min32";

    public JwtServiceTests()
    {
        var options = Options.Create(new TwoC2POptions
        {
            MerchantId = "JT01",
            SecretKey = SecretKey,
            BaseUrl = "https://sandbox-pgw.2c2p.com",
            FrontendReturnUrl = "http://localhost:3000/result",
            BackendReturnUrl = "http://localhost:5000/api/payments/2c2p/notification",
            TimeoutSeconds = 30
        });
        _sut = new TwoC2PJwtService(options, NullLogger<TwoC2PJwtService>.Instance);
    }

    [Fact]
    public void SignPayload_ShouldReturnValidJwt()
    {
        var payload = new { merchantID = "JT01", invoiceNo = "INV001", amount = 100.00 };
        var token = _sut.SignPayload(payload);

        token.Should().NotBeNullOrWhiteSpace();
        token.Split('.').Should().HaveCount(3, "a JWT must have three parts");
    }

    [Fact]
    public void SignAndVerify_RoundTrip_ShouldSucceed()
    {
        var payload = new { merchantID = "JT01", invoiceNo = "INV001", respCode = "0000" };
        var token = _sut.SignPayload(payload);

        var success = _sut.TryVerifyAndDecode(token, out var jsonNode, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        jsonNode.Should().NotBeNull();
        jsonNode!["merchantID"]!.GetValue<string>().Should().Be("JT01");
        jsonNode!["invoiceNo"]!.GetValue<string>().Should().Be("INV001");
    }

    [Fact]
    public void Verify_TamperedSignature_ShouldFail()
    {
        var payload = new { invoiceNo = "INV001", respCode = "0000" };
        var validToken = _sut.SignPayload(payload);

        // Tamper the signature part
        var parts = validToken.Split('.');
        parts[2] = parts[2][..^4] + "XXXX";
        var tamperedToken = string.Join('.', parts);

        var success = _sut.TryVerifyAndDecode(tamperedToken, out _, out var error);

        success.Should().BeFalse();
        error.Should().Contain("Signature");
    }

    [Fact]
    public void Verify_TokenSignedWithWrongKey_ShouldFail()
    {
        // Sign with a DIFFERENT secret
        var differentOptions = Options.Create(new TwoC2POptions
        {
            MerchantId = "JT01",
            SecretKey = "completely-different-secret-key-!!",
            BaseUrl = "https://sandbox-pgw.2c2p.com",
            FrontendReturnUrl = "http://localhost:3000/result",
            BackendReturnUrl = "http://localhost:5000/api/payments/2c2p/notification",
            TimeoutSeconds = 30
        });
        var wrongService = new TwoC2PJwtService(differentOptions, NullLogger<TwoC2PJwtService>.Instance);
        var token = wrongService.SignPayload(new { invoiceNo = "INV001" });

        // Verify with our service (different key)
        var success = _sut.TryVerifyAndDecode(token, out _, out var error);

        success.Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void Verify_NoneAlgorithmToken_ShouldFail()
    {
        // Build a "none" algorithm token manually
        var headerJson = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var payloadJson = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"invoiceNo\":\"INV001\"}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var noneToken = $"{headerJson}.{payloadJson}.";

        var success = _sut.TryVerifyAndDecode(noneToken, out _, out var error);

        success.Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void Verify_EmptyToken_ShouldFail()
    {
        var success = _sut.TryVerifyAndDecode("", out _, out var error);

        success.Should().BeFalse();
        error.Should().Contain("null or empty");
    }

    [Fact]
    public void Verify_MalformedToken_ShouldFail()
    {
        var success = _sut.TryVerifyAndDecode("not-a-jwt", out _, out var error);

        success.Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void SignPayload_UsesHmacSha256Algorithm()
    {
        var token = _sut.SignPayload(new { test = "value" });
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Header.Alg.Should().Be("HS256");
    }
}
