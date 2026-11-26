namespace PaymentIntegration.Application.Interfaces;

public record PaymentTokenRequestArgs(
    string InvoiceNo,
    decimal Amount,
    string CurrencyCode,
    string Description,
    IReadOnlyList<string>? PaymentChannels = null,
    bool Tokenize = false,
    IReadOnlyList<string>? CustomerTokens = null,
    bool CustomerTokenOnly = false,
    string Request3DS = "N");

public record TwoC2PPaymentTokenResult(
    bool Success,
    string? PaymentToken,
    string? WebPaymentUrl,
    string? RespCode,
    string? RespDesc);

public record TwoC2PDoPaymentResult(
    bool Success,
    string? RespCode,
    string? RespDesc,
    string? ChannelCode,
    string? RawData,
    string? ThreeDSUrl);

public record TwoC2PInquiryResult(
    bool Success,
    string? InvoiceNo,
    decimal? Amount,
    string? CurrencyCode,
    string? RespCode,
    string? RespDesc,
    string? ChannelCode,
    string? AccountNo,
    string? TranRef,
    string? CustomerToken);

public record TwoC2PDecodedNotification(
    bool IsValid,
    string? InvoiceNo,
    decimal? Amount,
    string? CurrencyCode,
    string? RespCode,
    string? RespDesc,
    string? ChannelCode,
    string? AccountNo,
    string? TranRef,
    string? CustomerToken,
    string? Error);

public interface ITwoC2PPaymentService
{
    Task<TwoC2PPaymentTokenResult> RequestPaymentTokenAsync(PaymentTokenRequestArgs args, CancellationToken ct = default);
    Task<TwoC2PDoPaymentResult> DoPaymentAsync(string paymentToken, string customerToken, CancellationToken ct = default);
    Task<TwoC2PInquiryResult> PaymentInquiryAsync(string invoiceNo, CancellationToken ct = default);
    Task<TwoC2PDecodedNotification> VerifyAndDecodeNotificationAsync(string jwtPayload, CancellationToken ct = default);
}
