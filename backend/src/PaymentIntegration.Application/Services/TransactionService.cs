using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Domain.Interfaces;

namespace PaymentIntegration.Application.Services;

public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _transactionRepository;

    public TransactionService(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
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
}
