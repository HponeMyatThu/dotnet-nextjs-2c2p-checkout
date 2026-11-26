using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Infrastructure.Payments.TwoC2P.Clients;
using PaymentIntegration.Infrastructure.Payments.TwoC2P.Jwt;
using PaymentIntegration.Infrastructure.Payments.TwoC2P.Models;
using PaymentIntegration.Infrastructure.Payments.TwoC2P.Options;

namespace PaymentIntegration.Infrastructure.Payments.TwoC2P.Services;

public class TwoC2PPaymentService : ITwoC2PPaymentService
{
    private readonly TwoC2PClient _client;
    private readonly ITwoC2PJwtService _jwtService;
    private readonly TwoC2POptions _options;
    private readonly ILogger<TwoC2PPaymentService> _logger;

    public TwoC2PPaymentService(
        TwoC2PClient client,
        ITwoC2PJwtService jwtService,
        IOptions<TwoC2POptions> options,
        ILogger<TwoC2PPaymentService> logger)
    {
        _client = client;
        _jwtService = jwtService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TwoC2PPaymentTokenResult> RequestPaymentTokenAsync(
        PaymentTokenRequestArgs args, 
        CancellationToken ct = default)
    {
        var returnUrlSeparator = _options.FrontendReturnUrl.Contains('?') ? "&" : "?";
        var frontendReturnUrl = $"{_options.FrontendReturnUrl}{returnUrlSeparator}invoice={Uri.EscapeDataString(args.InvoiceNo)}";

        // 1. Construct payment token payload
        var payload = new TwoC2PPaymentTokenRequestPayload
        {
            MerchantID = _options.MerchantId,
            InvoiceNo = args.InvoiceNo,
            Description = args.Description,
            Amount = args.Amount,
            CurrencyCode = args.CurrencyCode,
            PaymentChannel = args.PaymentChannels,
            Tokenize = args.Tokenize,
            CustomerToken = args.CustomerTokens,
            CustomerTokenOnly = args.CustomerTokenOnly,
            Request3DS = args.Request3DS,
            FrontendReturnUrl = frontendReturnUrl,
            BackendReturnUrl = _options.BackendReturnUrl
        };

        // 2. Sign JWT with HMAC SHA-256
        var signedJwt = _jwtService.SignPayload(payload);

        // 3. Send HTTP request
        var wrapper = await _client.RequestPaymentTokenAsync(signedJwt, ct);
        if (wrapper == null)
        {
            _logger.LogError("Failed to get payment token response from 2C2P for invoice {InvoiceNo}", args.InvoiceNo);
            return new TwoC2PPaymentTokenResult(false, null, null, "ERR_HTTP", "Failed to communicate with 2C2P payment token API.");
        }

        if (!string.IsNullOrEmpty(wrapper.RespCode) && wrapper.RespCode != "0000")
        {
            _logger.LogWarning("2C2P returned direct error code {RespCode}: {RespDesc}", wrapper.RespCode, wrapper.RespDesc);
            return new TwoC2PPaymentTokenResult(false, null, null, wrapper.RespCode, wrapper.RespDesc ?? "2C2P returned an error.");
        }

        if (string.IsNullOrWhiteSpace(wrapper.Payload))
        {
            _logger.LogError("Empty payment token payload for invoice {InvoiceNo}", args.InvoiceNo);
            return new TwoC2PPaymentTokenResult(false, null, null, wrapper.RespCode ?? "ERR_PAYLOAD", wrapper.RespDesc ?? "Empty response payload.");
        }

        // 4. Verify and decode response JWT
        if (!_jwtService.TryVerifyAndDecode<TwoC2PPaymentTokenResponsePayload>(wrapper.Payload, out var responsePayload, out var error)
            || responsePayload == null)
        {
            _logger.LogError("Failed to verify/decode payment token response JWT: {Error}", error);
            return new TwoC2PPaymentTokenResult(false, null, null, "ERR_JWT", error ?? "Invalid response signature.");
        }

        var isSuccess = responsePayload.RespCode == "0000";
        return new TwoC2PPaymentTokenResult(
            Success: isSuccess,
            PaymentToken: responsePayload.PaymentToken,
            WebPaymentUrl: responsePayload.WebPaymentUrl,
            RespCode: responsePayload.RespCode,
            RespDesc: responsePayload.RespDesc);
    }

    public async Task<TwoC2PDoPaymentResult> DoPaymentAsync(
        string paymentToken, 
        string customerToken, 
        CancellationToken ct = default)
    {
        // Do Payment takes plain JSON, not JWT
        var request = new TwoC2PDoPaymentRequest
        {
            PaymentToken = paymentToken,
            Payment = new TwoC2PPaymentDetails
            {
                Code = new TwoC2PPaymentCode { ChannelCode = "CC" },
                Data = new TwoC2PPaymentData
                {
                    CustomerToken = customerToken,
                    Token = customerToken
                }
            }
        };

        var response = await _client.DoPaymentAsync(request, ct);
        if (response == null)
        {
            _logger.LogError("Do Payment returned null response.");
            return new TwoC2PDoPaymentResult(false, "ERR_HTTP", "No response from Do Payment endpoint", null, null, null);
        }

        // Check if 3DS redirection URL is present (code 1001)
        string? threeDSUrl = null;
        if (response.RespCode == "1001")
        {
            // 2C2P returns the 3DS URL in rawData or payload
            threeDSUrl = !string.IsNullOrEmpty(response.RawData) ? response.RawData : response.Payload;
        }

        return new TwoC2PDoPaymentResult(
            Success: response.RespCode is "2000" or "0000" or "1001",
            RespCode: response.RespCode,
            RespDesc: response.RespDesc,
            ChannelCode: response.ChannelCode,
            RawData: response.RawData,
            ThreeDSUrl: threeDSUrl);
    }

    public async Task<TwoC2PInquiryResult> PaymentInquiryAsync(
        string invoiceNo, 
        CancellationToken ct = default)
    {
        var payload = new TwoC2PInquiryRequestPayload
        {
            MerchantID = _options.MerchantId,
            InvoiceNo = invoiceNo,
            Locale = "en"
        };

        var signedJwt = _jwtService.SignPayload(payload);
        var wrapper = await _client.PaymentInquiryAsync(signedJwt, ct);
        if (wrapper == null)
        {
            _logger.LogError("Payment inquiry returned null response for invoice {InvoiceNo}", invoiceNo);
            return new TwoC2PInquiryResult(false, invoiceNo, null, null, "ERR_HTTP", "Failed to query 2C2P payment inquiry API", null, null, null, null);
        }

        if (!string.IsNullOrEmpty(wrapper.RespCode) && wrapper.RespCode != "0000")
        {
            _logger.LogInformation("2C2P payment inquiry returned direct code {RespCode}: {RespDesc} for invoice {InvoiceNo}",
                wrapper.RespCode, wrapper.RespDesc, invoiceNo);
            return new TwoC2PInquiryResult(false, invoiceNo, null, null, wrapper.RespCode, wrapper.RespDesc, null, null, null, null);
        }

        if (string.IsNullOrWhiteSpace(wrapper.Payload))
        {
            _logger.LogError("Payment inquiry returned empty payload for invoice {InvoiceNo}", invoiceNo);
            return new TwoC2PInquiryResult(false, invoiceNo, null, null, "ERR_HTTP", "Empty payment inquiry response payload", null, null, null, null);
        }

        // Verify and decode response JWT
        if (!_jwtService.TryVerifyAndDecode<TwoC2PInquiryResponsePayload>(wrapper.Payload, out var responsePayload, out var error)
            || responsePayload == null)
        {
            _logger.LogError("Failed to verify/decode payment inquiry response JWT: {Error}", error);
            return new TwoC2PInquiryResult(false, invoiceNo, null, null, "ERR_JWT", error ?? "Invalid response signature.", null, null, null, null);
        }

        return new TwoC2PInquiryResult(
            Success: true,
            InvoiceNo: responsePayload.InvoiceNo,
            Amount: responsePayload.Amount,
            CurrencyCode: responsePayload.CurrencyCode,
            RespCode: responsePayload.RespCode,
            RespDesc: responsePayload.RespDesc,
            ChannelCode: responsePayload.ChannelCode,
            AccountNo: responsePayload.AccountNo,
            TranRef: responsePayload.TranRef,
            CustomerToken: responsePayload.CustomerToken);
    }

    public Task<TwoC2PDecodedNotification> VerifyAndDecodeNotificationAsync(
        string jwtPayload, 
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(jwtPayload))
        {
            return Task.FromResult(new TwoC2PDecodedNotification(false, null, null, null, null, null, null, null, null, null, "Empty JWT"));
        }

        if (!_jwtService.TryVerifyAndDecode<TwoC2PNotificationPayload>(jwtPayload, out var payload, out var error)
            || payload == null)
        {
            return Task.FromResult(new TwoC2PDecodedNotification(false, null, null, null, null, null, null, null, null, null, error ?? "Invalid signature"));
        }

        return Task.FromResult(new TwoC2PDecodedNotification(
            IsValid: true,
            InvoiceNo: payload.InvoiceNo,
            Amount: payload.Amount,
            CurrencyCode: payload.CurrencyCode,
            RespCode: payload.RespCode,
            RespDesc: payload.RespDesc,
            ChannelCode: payload.ChannelCode,
            AccountNo: payload.AccountNo,
            TranRef: payload.TranRef,
            CustomerToken: payload.CustomerToken,
            Error: null));
    }
}
