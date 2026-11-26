namespace PaymentIntegration.Application.DTOs;

public record CustomerDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    string FullName);

public record ProductDto(
    int Id,
    string Name,
    string Description,
    decimal Price,
    string Currency,
    string PaymentType);

public record CustomerCardDto(
    int Id,
    int CustomerId,
    string MaskedCardNo,
    DateTime CreatedAt);

public record CheckoutRequestDto(
    int CustomerId,
    int ProductId,
    int? CustomerCardId);

public record CheckoutResponseDto(
    string InvoiceNo,
    string Status,
    string? RedirectUrl);

public record TransactionDto(
    long Id,
    int CustomerId,
    string CustomerName,
    int ProductId,
    string ProductName,
    string InvoiceNo,
    string IdempotencyKey,
    decimal Amount,
    string Currency,
    string Status,
    string? PaymentChannel,
    string? MaskedCardNo,
    string? ProviderRef,
    string? RespCode,
    string? RespDesc,
    string? RedirectUrl,
    DateTime CreatedAt,
    DateTime? PaidAt);

public record PaymentNotificationRequestDto(
    string Payload);

public record PaymentNotificationResultDto(
    bool Success,
    string Message,
    string? InvoiceNo = null,
    string? Status = null);
