using PaymentIntegration.Application.DTOs;

namespace PaymentIntegration.Application.Interfaces;

public interface ICheckoutService
{
    Task<CheckoutResponseDto> ProcessCheckoutAsync(string idempotencyKey, CheckoutRequestDto request, CancellationToken ct = default);
}

public interface ITransactionService
{
    Task<TransactionDto?> GetByInvoiceNoAsync(string invoiceNo, CancellationToken ct = default);
}

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerDto>> GetAllCustomersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CustomerCardDto>> GetCardsByCustomerIdAsync(int customerId, CancellationToken ct = default);
}

public interface IProductService
{
    Task<IReadOnlyList<ProductDto>> GetAllProductsAsync(CancellationToken ct = default);
}

public interface IPaymentNotificationService
{
    Task<PaymentNotificationResultDto> HandleNotificationAsync(string payload, CancellationToken ct = default);
}
