using System.Text.Json.Serialization;

namespace PaymentIntegration.Infrastructure.Payments.TwoC2P.Models;

public class TwoC2PPayloadWrapper
{
    [JsonPropertyName("payload")]
    public string? Payload { get; set; }

    [JsonPropertyName("respCode")]
    public string? RespCode { get; set; }

    [JsonPropertyName("respDesc")]
    public string? RespDesc { get; set; }
}

public class TwoC2PPaymentTokenRequestPayload
{
    [JsonPropertyName("merchantID")]
    public string MerchantID { get; set; } = string.Empty;

    [JsonPropertyName("invoiceNo")]
    public string InvoiceNo { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; set; } = "SGD";

    [JsonPropertyName("paymentChannel")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? PaymentChannel { get; set; }

    [JsonPropertyName("tokenize")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Tokenize { get; set; }

    [JsonPropertyName("customerToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? CustomerToken { get; set; }

    [JsonPropertyName("customerTokenOnly")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool CustomerTokenOnly { get; set; }

    [JsonPropertyName("request3DS")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Request3DS { get; set; }

    [JsonPropertyName("frontendReturnUrl")]
    public string FrontendReturnUrl { get; set; } = string.Empty;

    [JsonPropertyName("backendReturnUrl")]
    public string BackendReturnUrl { get; set; } = string.Empty;
}

public class TwoC2PPaymentTokenResponsePayload
{
    [JsonPropertyName("webPaymentUrl")]
    public string? WebPaymentUrl { get; set; }

    [JsonPropertyName("paymentToken")]
    public string? PaymentToken { get; set; }

    [JsonPropertyName("respCode")]
    public string? RespCode { get; set; }

    [JsonPropertyName("respDesc")]
    public string? RespDesc { get; set; }
}

public class TwoC2PDoPaymentRequest
{
    [JsonPropertyName("paymentToken")]
    public string PaymentToken { get; set; } = string.Empty;

    [JsonPropertyName("payment")]
    public TwoC2PPaymentDetails Payment { get; set; } = new();
}

public class TwoC2PPaymentDetails
{
    [JsonPropertyName("code")]
    public TwoC2PPaymentCode Code { get; set; } = new();

    [JsonPropertyName("data")]
    public TwoC2PPaymentData Data { get; set; } = new();
}

public class TwoC2PPaymentCode
{
    [JsonPropertyName("channelCode")]
    public string ChannelCode { get; set; } = "CC";
}

public class TwoC2PPaymentData
{
    [JsonPropertyName("customerToken")]
    public string CustomerToken { get; set; } = string.Empty;

    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;
}

public class TwoC2PDoPaymentResponse
{
    [JsonPropertyName("respCode")]
    public string? RespCode { get; set; }

    [JsonPropertyName("respDesc")]
    public string? RespDesc { get; set; }

    [JsonPropertyName("channelCode")]
    public string? ChannelCode { get; set; }

    [JsonPropertyName("rawData")]
    public string? RawData { get; set; }

    [JsonPropertyName("payload")]
    public string? Payload { get; set; }
}

public class TwoC2PInquiryRequestPayload
{
    [JsonPropertyName("merchantID")]
    public string MerchantID { get; set; } = string.Empty;

    [JsonPropertyName("invoiceNo")]
    public string InvoiceNo { get; set; } = string.Empty;

    [JsonPropertyName("locale")]
    public string Locale { get; set; } = "en";
}

public class TwoC2PInquiryResponsePayload
{
    [JsonPropertyName("invoiceNo")]
    public string? InvoiceNo { get; set; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("currencyCode")]
    public string? CurrencyCode { get; set; }

    [JsonPropertyName("respCode")]
    public string? RespCode { get; set; }

    [JsonPropertyName("respDesc")]
    public string? RespDesc { get; set; }

    [JsonPropertyName("channelCode")]
    public string? ChannelCode { get; set; }

    [JsonPropertyName("accountNo")]
    public string? AccountNo { get; set; }

    [JsonPropertyName("tranRef")]
    public string? TranRef { get; set; }

    [JsonPropertyName("customerToken")]
    public string? CustomerToken { get; set; }
}

public class TwoC2PNotificationPayload
{
    [JsonPropertyName("merchantID")]
    public string? MerchantID { get; set; }

    [JsonPropertyName("invoiceNo")]
    public string? InvoiceNo { get; set; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("currencyCode")]
    public string? CurrencyCode { get; set; }

    [JsonPropertyName("respCode")]
    public string? RespCode { get; set; }

    [JsonPropertyName("respDesc")]
    public string? RespDesc { get; set; }

    [JsonPropertyName("channelCode")]
    public string? ChannelCode { get; set; }

    [JsonPropertyName("accountNo")]
    public string? AccountNo { get; set; }

    [JsonPropertyName("tranRef")]
    public string? TranRef { get; set; }

    [JsonPropertyName("customerToken")]
    public string? CustomerToken { get; set; }
}
