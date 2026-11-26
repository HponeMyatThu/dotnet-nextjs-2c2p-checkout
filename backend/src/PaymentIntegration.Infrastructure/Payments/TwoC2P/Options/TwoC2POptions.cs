namespace PaymentIntegration.Infrastructure.Payments.TwoC2P.Options;

public class TwoC2POptions
{
    public const string SectionName = "TwoC2P";

    public string BaseUrl { get; set; } = "https://sandbox-pgw.2c2p.com";
    public string MerchantId { get; set; } = "JT01";
    public string SecretKey { get; set; } = "7jYcp4FxFdf0";
    public string FrontendReturnUrl { get; set; } = "http://localhost:3000/result";
    public string BackendReturnUrl { get; set; } = "http://localhost:5000/api/payments/2c2p/notification";
    public int TimeoutSeconds { get; set; } = 30;
}
