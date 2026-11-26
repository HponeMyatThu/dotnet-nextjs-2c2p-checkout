using Microsoft.Extensions.Logging;
using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Domain.Entities;
using PaymentIntegration.Domain.Enums;
using PaymentIntegration.Domain.Interfaces;

namespace PaymentIntegration.Application.Services;

public class PaymentNotificationService : IPaymentNotificationService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICustomerCardRepository _customerCardRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITwoC2PPaymentService _twoC2PPaymentService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<PaymentNotificationService> _logger;

    public PaymentNotificationService(
        ITransactionRepository transactionRepository,
        ICustomerCardRepository customerCardRepository,
        IUnitOfWork unitOfWork,
        ITwoC2PPaymentService twoC2PPaymentService,
        ICacheService cacheService,
        ILogger<PaymentNotificationService> logger)
    {
        _transactionRepository = transactionRepository;
        _customerCardRepository = customerCardRepository;
        _unitOfWork = unitOfWork;
        _twoC2PPaymentService = twoC2PPaymentService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<PaymentNotificationResultDto> HandleNotificationAsync(
        string payload, 
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            _logger.LogWarning("Empty notification payload received.");
            return new PaymentNotificationResultDto(false, "Empty payload");
        }

        // 1. Verify and decode notification JWT using HMAC SHA-256
        var decoded = await _twoC2PPaymentService.VerifyAndDecodeNotificationAsync(payload, ct);
        if (!decoded.IsValid || string.IsNullOrEmpty(decoded.InvoiceNo))
        {
            _logger.LogWarning("Invalid notification signature or format: {Error}", decoded.Error);
            return new PaymentNotificationResultDto(false, $"Verification failed: {decoded.Error}");
        }

        _logger.LogInformation("Processing verified notification for Invoice {InvoiceNo}, RespCode {RespCode}", 
            decoded.InvoiceNo, decoded.RespCode);

        // 2. Locate transaction
        var transaction = await _transactionRepository.GetByInvoiceNoAsync(decoded.InvoiceNo, ct);
        if (transaction == null)
        {
            _logger.LogWarning("Transaction with invoice {InvoiceNo} not found for notification.", decoded.InvoiceNo);
            return new PaymentNotificationResultDto(false, "Transaction not found", decoded.InvoiceNo);
        }

        // 3. Idempotency guard: If transaction is already final (Succeeded or Failed), do not alter status
        if (transaction.IsFinalState)
        {
            _logger.LogInformation("Transaction {InvoiceNo} is already in final state {Status}. Skipping update.", 
                transaction.InvoiceNo, transaction.Status);

            // Even if duplicate notification arrives, verify if card token needs to be saved (safe idempotent check)
            if (transaction.Status == TransactionStatus.Succeeded && !string.IsNullOrWhiteSpace(decoded.CustomerToken))
            {
                await SaveCustomerCardSafelyAsync(transaction.CustomerId, decoded.CustomerToken, decoded.AccountNo, ct);
            }

            return new PaymentNotificationResultDto(true, "Transaction already finalized", transaction.InvoiceNo, transaction.Status.ToString());
        }

        // 4. Verify amount and currency
        if (decoded.Amount.HasValue && decoded.Amount.Value != transaction.Amount)
        {
            _logger.LogError("Amount mismatch for invoice {InvoiceNo}: expected {Expected}, got {Received}", 
                transaction.InvoiceNo, transaction.Amount, decoded.Amount.Value);
            await _transactionRepository.UpdateStatusConditionalAsync(
                transaction.InvoiceNo,
                TransactionStatus.Pending,
                TransactionStatus.Failed,
                "ERR_AMOUNT",
                "Notification amount does not match transaction amount.",
                decoded.ChannelCode ?? transaction.PaymentChannel,
                decoded.AccountNo ?? transaction.MaskedCardNo,
                decoded.TranRef ?? transaction.ProviderRef,
                null,
                ct);
            return new PaymentNotificationResultDto(false, "Amount mismatch", transaction.InvoiceNo, TransactionStatus.Failed.ToString());
        }

        if (!string.IsNullOrEmpty(decoded.CurrencyCode) && !decoded.CurrencyCode.Equals(transaction.Currency, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("Currency mismatch for invoice {InvoiceNo}: expected {Expected}, got {Received}", 
                transaction.InvoiceNo, transaction.Currency, decoded.CurrencyCode);
            await _transactionRepository.UpdateStatusConditionalAsync(
                transaction.InvoiceNo,
                TransactionStatus.Pending,
                TransactionStatus.Failed,
                "ERR_CURRENCY",
                "Notification currency does not match transaction currency.",
                decoded.ChannelCode ?? transaction.PaymentChannel,
                decoded.AccountNo ?? transaction.MaskedCardNo,
                decoded.TranRef ?? transaction.ProviderRef,
                null,
                ct);
            return new PaymentNotificationResultDto(false, "Currency mismatch", transaction.InvoiceNo, TransactionStatus.Failed.ToString());
        }

        // 5. Update transaction state atomically WHERE status = 'Pending'
        if (decoded.RespCode == "0000")
        {
            var paidAt = DateTime.UtcNow;
            var updatedRows = await _transactionRepository.UpdateStatusConditionalAsync(
                transaction.InvoiceNo,
                TransactionStatus.Pending,
                TransactionStatus.Succeeded,
                decoded.RespCode,
                decoded.RespDesc ?? "Success",
                decoded.ChannelCode ?? transaction.PaymentChannel,
                decoded.AccountNo ?? transaction.MaskedCardNo,
                decoded.TranRef ?? transaction.ProviderRef,
                paidAt,
                ct);

            if (updatedRows == 0)
            {
                _logger.LogInformation("Transaction {InvoiceNo} was already finalized concurrently.", transaction.InvoiceNo);
            }

            // 6. Save customer token if present (for recurring/save-card scenario)
            if (!string.IsNullOrWhiteSpace(decoded.CustomerToken))
            {
                await SaveCustomerCardSafelyAsync(transaction.CustomerId, decoded.CustomerToken, decoded.AccountNo, ct);
            }

            return new PaymentNotificationResultDto(true, "Notification processed successfully", transaction.InvoiceNo, TransactionStatus.Succeeded.ToString());
        }
        else
        {
            await _transactionRepository.UpdateStatusConditionalAsync(
                transaction.InvoiceNo,
                TransactionStatus.Pending,
                TransactionStatus.Failed,
                decoded.RespCode,
                decoded.RespDesc,
                decoded.ChannelCode ?? transaction.PaymentChannel,
                decoded.AccountNo ?? transaction.MaskedCardNo,
                decoded.TranRef ?? transaction.ProviderRef,
                null,
                ct);

            return new PaymentNotificationResultDto(true, "Notification processed successfully", transaction.InvoiceNo, TransactionStatus.Failed.ToString());
        }
    }

    private async Task SaveCustomerCardSafelyAsync(
        int customerId, 
        string customerToken, 
        string? maskedCardNo, 
        CancellationToken ct)
    {
        try
        {
            var existingCard = await _customerCardRepository.GetByCustomerAndTokenAsync(customerId, customerToken, ct);
            if (existingCard == null)
            {
                var newCard = new CustomerCard
                {
                    CustomerId = customerId,
                    CustomerToken = customerToken,
                    MaskedCardNo = maskedCardNo ?? "XXXX-XXXX-XXXX",
                    CreatedAt = DateTime.UtcNow
                };

                await _customerCardRepository.AddAsync(newCard, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                // Invalidate cached cards for this customer
                await _cacheService.RemoveAsync($"customers:{customerId}:cards", ct);
                _logger.LogInformation("Successfully saved customer card token for customer {CustomerId}", customerId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save customer card token or already exists for customer {CustomerId}", customerId);
        }
    }
}
