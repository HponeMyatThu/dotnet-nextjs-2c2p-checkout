using Microsoft.Extensions.Logging;
using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Domain.Entities;
using PaymentIntegration.Domain.Enums;
using PaymentIntegration.Domain.Interfaces;

namespace PaymentIntegration.Application.Services;

public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICustomerCardRepository _customerCardRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITwoC2PPaymentService _twoC2PPaymentService;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(
        ITransactionRepository transactionRepository,
        ICustomerCardRepository customerCardRepository,
        IUnitOfWork unitOfWork,
        ITwoC2PPaymentService twoC2PPaymentService,
        ILogger<TransactionService> logger)
    {
        _transactionRepository = transactionRepository;
        _customerCardRepository = customerCardRepository;
        _unitOfWork = unitOfWork;
        _twoC2PPaymentService = twoC2PPaymentService;
        _logger = logger;
    }

    public async Task<TransactionDto?> GetByInvoiceNoAsync(string invoiceNo, CancellationToken ct = default)
    {
        // NOTE: Per assessment requirements, NEVER cache transaction status.
        // It must always be read fresh from the database.
        var transaction = await _transactionRepository.GetByInvoiceNoAsync(invoiceNo, ct);
        if (transaction == null)
        {
            return null;
        }

        // Fallback to 2C2P Payment Inquiry if transaction is still Pending
        // (e.g., when backendReturnUrl webhook hasn't arrived or isn't publicly reachable locally).
        if (transaction.Status == TransactionStatus.Pending)
        {
            await TrySyncFromPaymentInquiryAsync(transaction, ct);
            // Re-read fresh from DB in case status was updated
            transaction = await _transactionRepository.GetByInvoiceNoAsync(invoiceNo, ct) ?? transaction;
        }

        return new TransactionDto(
            transaction.Id,
            transaction.CustomerId,
            transaction.Customer != null ? $"{transaction.Customer.FirstName} {transaction.Customer.LastName}".Trim() : string.Empty,
            transaction.ProductId,
            transaction.Product?.Name ?? string.Empty,
            transaction.InvoiceNo,
            transaction.IdempotencyKey,
            transaction.Amount,
            transaction.Currency,
            transaction.Status.ToString(),
            transaction.PaymentChannel,
            transaction.MaskedCardNo,
            transaction.ProviderRef,
            transaction.RespCode,
            transaction.RespDesc,
            transaction.RedirectUrl,
            transaction.CreatedAt,
            transaction.PaidAt);
    }

    private async Task TrySyncFromPaymentInquiryAsync(Transaction transaction, CancellationToken ct)
    {
        try
        {
            var inquiry = await _twoC2PPaymentService.PaymentInquiryAsync(transaction.InvoiceNo, ct);
            if (!inquiry.Success || string.IsNullOrEmpty(inquiry.RespCode))
            {
                return;
            }

            // 0001 = Payment still pending / in progress; 9015 = Not yet found
            if (inquiry.RespCode is "0001" or "9015")
            {
                return;
            }

            // Verify amount and currency match stored transaction
            if (inquiry.Amount.HasValue && inquiry.Amount.Value != transaction.Amount)
            {
                _logger.LogWarning("Payment Inquiry amount mismatch for {InvoiceNo}: expected {Expected}, got {Actual}",
                    transaction.InvoiceNo, transaction.Amount, inquiry.Amount.Value);
                await _transactionRepository.UpdateStatusConditionalAsync(
                    transaction.InvoiceNo,
                    TransactionStatus.Pending,
                    TransactionStatus.Failed,
                    "ERR_AMOUNT",
                    "Inquiry amount does not match transaction amount.",
                    inquiry.ChannelCode ?? transaction.PaymentChannel,
                    inquiry.AccountNo ?? transaction.MaskedCardNo,
                    inquiry.TranRef ?? transaction.ProviderRef,
                    null,
                    ct);
                return;
            }

            if (!string.IsNullOrEmpty(inquiry.CurrencyCode) &&
                !inquiry.CurrencyCode.Equals(transaction.Currency, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Payment Inquiry currency mismatch for {InvoiceNo}: expected {Expected}, got {Actual}",
                    transaction.InvoiceNo, transaction.Currency, inquiry.CurrencyCode);
                await _transactionRepository.UpdateStatusConditionalAsync(
                    transaction.InvoiceNo,
                    TransactionStatus.Pending,
                    TransactionStatus.Failed,
                    "ERR_CURRENCY",
                    "Inquiry currency does not match transaction currency.",
                    inquiry.ChannelCode ?? transaction.PaymentChannel,
                    inquiry.AccountNo ?? transaction.MaskedCardNo,
                    inquiry.TranRef ?? transaction.ProviderRef,
                    null,
                    ct);
                return;
            }

            if (inquiry.RespCode == "0000")
            {
                await _transactionRepository.UpdateStatusConditionalAsync(
                    transaction.InvoiceNo,
                    TransactionStatus.Pending,
                    TransactionStatus.Succeeded,
                    inquiry.RespCode,
                    inquiry.RespDesc ?? "Payment succeeded",
                    inquiry.ChannelCode ?? transaction.PaymentChannel,
                    inquiry.AccountNo ?? transaction.MaskedCardNo,
                    inquiry.TranRef ?? transaction.ProviderRef,
                    DateTime.UtcNow,
                    ct);

                if (!string.IsNullOrWhiteSpace(inquiry.CustomerToken))
                {
                    var existing = await _customerCardRepository.GetByCustomerAndTokenAsync(
                        transaction.CustomerId, inquiry.CustomerToken, ct);
                    if (existing == null)
                    {
                        await _customerCardRepository.AddAsync(new CustomerCard
                        {
                            CustomerId = transaction.CustomerId,
                            CustomerToken = inquiry.CustomerToken,
                            MaskedCardNo = inquiry.AccountNo ?? "****",
                            CreatedAt = DateTime.UtcNow
                        }, ct);
                        await _unitOfWork.SaveChangesAsync(ct);
                    }
                }
            }
            else
            {
                await _transactionRepository.UpdateStatusConditionalAsync(
                    transaction.InvoiceNo,
                    TransactionStatus.Pending,
                    TransactionStatus.Failed,
                    inquiry.RespCode,
                    inquiry.RespDesc,
                    inquiry.ChannelCode ?? transaction.PaymentChannel,
                    inquiry.AccountNo ?? transaction.MaskedCardNo,
                    inquiry.TranRef ?? transaction.ProviderRef,
                    null,
                    ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Payment Inquiry fallback failed for invoice {InvoiceNo}", transaction.InvoiceNo);
        }
    }
}
