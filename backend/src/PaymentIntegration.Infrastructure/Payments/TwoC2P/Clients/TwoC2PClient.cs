using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentIntegration.Infrastructure.Payments.TwoC2P.Models;
using PaymentIntegration.Infrastructure.Payments.TwoC2P.Options;

namespace PaymentIntegration.Infrastructure.Payments.TwoC2P.Clients;

public class TwoC2PClient
{
    private readonly HttpClient _httpClient;
    private readonly TwoC2POptions _options;
    private readonly ILogger<TwoC2PClient> _logger;

    public TwoC2PClient(
        HttpClient httpClient, 
        IOptions<TwoC2POptions> options, 
        ILogger<TwoC2PClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        }
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    /// <summary>
    /// POST /payment/4.3/paymenttoken with JWT payload
    /// </summary>
    public async Task<TwoC2PPayloadWrapper?> RequestPaymentTokenAsync(string signedJwt, CancellationToken ct = default)
    {
        var request = new TwoC2PPayloadWrapper { Payload = signedJwt };
        var response = await _httpClient.PostAsJsonAsync("payment/4.3/paymenttoken", request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("2C2P paymenttoken API failed with status {StatusCode}: {ErrorBody}", 
                response.StatusCode, errorBody);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<TwoC2PPayloadWrapper>(cancellationToken: ct);
    }

    /// <summary>
    /// POST /payment/4.3/payment (Do Payment) with plain JSON
    /// </summary>
    public async Task<TwoC2PDoPaymentResponse?> DoPaymentAsync(TwoC2PDoPaymentRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("payment/4.3/payment", request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("2C2P payment API failed with status {StatusCode}: {ErrorBody}", 
                response.StatusCode, errorBody);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<TwoC2PDoPaymentResponse>(cancellationToken: ct);
    }

    /// <summary>
    /// POST /payment/4.3/paymentinquiry with JWT payload
    /// </summary>
    public async Task<TwoC2PPayloadWrapper?> PaymentInquiryAsync(string signedJwt, CancellationToken ct = default)
    {
        var request = new TwoC2PPayloadWrapper { Payload = signedJwt };
        var response = await _httpClient.PostAsJsonAsync("payment/4.3/paymentinquiry", request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("2C2P paymentinquiry API failed with status {StatusCode}: {ErrorBody}", 
                response.StatusCode, errorBody);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<TwoC2PPayloadWrapper>(cancellationToken: ct);
    }
}
