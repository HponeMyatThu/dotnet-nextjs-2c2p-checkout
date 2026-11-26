using Microsoft.Extensions.Logging;
using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Domain.Entities;
using PaymentIntegration.Domain.Enums;
using PaymentIntegration.Domain.Exceptions;
using PaymentIntegration.Domain.Interfaces;

namespace PaymentIntegration.Application.Services;

public class CheckoutService : ICheckoutService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICustomerCardRepository _customerCardRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITwoC2PPaymentService _twoC2PPaymentService;
    private readonly IDistributedLockService _distributedLockService;
    private readonly ILogger<CheckoutService> _logger;

    public CheckoutService(
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        ICustomerCardRepository customerCardRepository,
        ITransactionRepository transactionRepository,
        IUnitOfWork unitOfWork,
        ITwoC2PPaymentService twoC2PPaymentService,
        IDistributedLockService distributedLockService,
        ILogger<CheckoutService> logger)
    {
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _customerCardRepository = customerCardRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
        _twoC2PPaymentService = twoC2PPaymentService;
        _distributedLockService = distributedLockService;
        _logger = logger;
    }

    public async Task<CheckoutResponseDto> ProcessCheckoutAsync(
        string idempotencyKey, 
        CheckoutRequestDto request, 
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || !Guid.TryParse(idempotencyKey, out _))
        {
            throw new ArgumentException("A valid UUID Idempotency-Key header is required.", nameof(idempotencyKey));
        }

        // 1. Fast path: Check if an existing transaction with this IdempotencyKey already exists
        var existingTransaction = await _transactionRepository.GetByIdempotencyKeyAsync(idempotencyKey, ct);
        if (existingTransaction != null)
        {
            _logger.LogInformation("Found existing transaction for IdempotencyKey {IdempotencyKey} with Invoice {InvoiceNo}", 
                idempotencyKey, existingTransaction.InvoiceNo);
            return new CheckoutResponseDto(
                existingTransaction.InvoiceNo,
                existingTransaction.Status.ToString(),
                existingTransaction.RedirectUrl);
        }

        // 2. Distributed Locking per IdempotencyKey with Redis
        var lockKey = $"lock:checkout:{idempotencyKey}";
        var lockTimeout = TimeSpan.FromSeconds(5);
        var lockExpiry = TimeSpan.FromSeconds(15);

        await using var lockHandle = await _distributedLockService.TryAcquireLockAsync(lockKey, lockTimeout, lockExpiry, ct);
        if (lockHandle == null)
        {
            // Lock was not acquired within timeout: Check again if transaction was created by concurrent winner
            existingTransaction = await _transactionRepository.GetByIdempotencyKeyAsync(idempotencyKey, ct);
            if (existingTransaction != null)
            {
                return new CheckoutResponseDto(
                    existingTransaction.InvoiceNo,
                    existingTransaction.Status.ToString(),
                    existingTransaction.RedirectUrl);
            }

            _logger.LogWarning("Concurrent request active for IdempotencyKey {IdempotencyKey}. Failing with 409 Conflict.", idempotencyKey);
            throw new IdempotencyConflictException(idempotencyKey);
        }

        // 3. Double-check under the lock
        existingTransaction = await _transactionRepository.GetByIdempotencyKeyAsync(idempotencyKey, ct);
        if (existingTransaction != null)
        {
            return new CheckoutResponseDto(
                existingTransaction.InvoiceNo,
                existingTransaction.Status.ToString(),
                existingTransaction.RedirectUrl);
        }

        // 4. Validate customer and product existence
        var customer = await _customerRepository.GetByIdAsync(request.CustomerId, ct)
            ?? throw new CustomerNotFoundException(request.CustomerId);

        var product = await _productRepository.GetByIdAsync(request.ProductId, ct)
            ?? throw new ProductNotFoundException(request.ProductId);

        CustomerCard? customerCard = null;
        if (request.CustomerCardId.HasValue)
        {
            customerCard = await _customerCardRepository.GetByIdAsync(request.CustomerCardId.Value, ct);
            if (customerCard == null || customerCard.CustomerId != customer.Id)
            {
                throw new CustomerCardNotFoundException(request.CustomerCardId.Value, customer.Id);
            }
        }

        // 5. Generate unique InvoiceNo
        var invoiceNo = GenerateInvoiceNo();

        // 6. Create Pending Transaction
        var transaction = new Transaction
        {
            CustomerId = customer.Id,
            ProductId = product.Id,
            InvoiceNo = invoiceNo,
            IdempotencyKey = idempotencyKey,
            Amount = product.Price,
            Currency = product.Currency,
            Status = TransactionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await _transactionRepository.AddAsync(transaction, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database conflict on inserting transaction with IdempotencyKey {IdempotencyKey}", idempotencyKey);
            // Fallback for database unique constraint collision
            var duplicate = await _transactionRepository.GetByIdempotencyKeyAsync(idempotencyKey, ct);
            if (duplicate != null)
            {
                return new CheckoutResponseDto(duplicate.InvoiceNo, duplicate.Status.ToString(), duplicate.RedirectUrl);
            }
            throw;
        }

        // 7. Execute 2C2P Payment Flow according to scenarios
        try
        {
            if (customerCard != null)
            {
                // Scenario C: Saved Card Payment (Direct API)
                return await ProcessSavedCardPaymentAsync(transaction, customerCard, product, ct);
            }
            else if (product.PaymentType == PaymentType.Recurring)
            {
                // Scenario B: Recurring Product + New Payment Method (Hosted Page with Card Tokenization)
                return await ProcessRecurringNewMethodPaymentAsync(transaction, product, ct);
            }
            else
            {
                // Scenario A: OneTime Product + New Payment Method (Hosted Page, all methods)
                return await ProcessOneTimeNewMethodPaymentAsync(transaction, product, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment for invoice {InvoiceNo}", invoiceNo);
            transaction.Status = TransactionStatus.Failed;
            transaction.RespDesc = ex.Message.Length > 250 ? ex.Message[..250] : ex.Message;
            await _transactionRepository.UpdateAsync(transaction, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            throw;
        }
    }

    private async Task<CheckoutResponseDto> ProcessOneTimeNewMethodPaymentAsync(
        Transaction transaction, 
        Product product, 
        CancellationToken ct)
    {
        var tokenArgs = new PaymentTokenRequestArgs(
            InvoiceNo: transaction.InvoiceNo,
            Amount: transaction.Amount,
            CurrencyCode: transaction.Currency,
            Description: product.Name,
            PaymentChannels: null, // Allow all payment methods
            Tokenize: false);

        var tokenResult = await _twoC2PPaymentService.RequestPaymentTokenAsync(tokenArgs, ct);
        if (!tokenResult.Success || string.IsNullOrEmpty(tokenResult.WebPaymentUrl))
        {
            transaction.Status = TransactionStatus.Failed;
            transaction.RespCode = tokenResult.RespCode;
            transaction.RespDesc = tokenResult.RespDesc ?? "Failed to obtain payment token from 2C2P.";
            await _transactionRepository.UpdateAsync(transaction, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return new CheckoutResponseDto(transaction.InvoiceNo, transaction.Status.ToString(), null);
        }

        transaction.RedirectUrl = tokenResult.WebPaymentUrl;
        transaction.RespCode = tokenResult.RespCode;
        transaction.RespDesc = tokenResult.RespDesc;
        await _transactionRepository.UpdateAsync(transaction, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new CheckoutResponseDto(transaction.InvoiceNo, transaction.Status.ToString(), tokenResult.WebPaymentUrl);
    }

    private async Task<CheckoutResponseDto> ProcessRecurringNewMethodPaymentAsync(
        Transaction transaction, 
        Product product, 
        CancellationToken ct)
    {
        var tokenArgs = new PaymentTokenRequestArgs(
            InvoiceNo: transaction.InvoiceNo,
            Amount: transaction.Amount,
            CurrencyCode: transaction.Currency,
            Description: product.Name,
            PaymentChannels: new[] { "CC" }, // Card only
            Tokenize: true); // Enable card tokenization

        var tokenResult = await _twoC2PPaymentService.RequestPaymentTokenAsync(tokenArgs, ct);
        if (!tokenResult.Success || string.IsNullOrEmpty(tokenResult.WebPaymentUrl))
        {
            transaction.Status = TransactionStatus.Failed;
            transaction.RespCode = tokenResult.RespCode;
            transaction.RespDesc = tokenResult.RespDesc ?? "Failed to obtain payment token for recurring product.";
            await _transactionRepository.UpdateAsync(transaction, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return new CheckoutResponseDto(transaction.InvoiceNo, transaction.Status.ToString(), null);
        }

        transaction.RedirectUrl = tokenResult.WebPaymentUrl;
        transaction.RespCode = tokenResult.RespCode;
        transaction.RespDesc = tokenResult.RespDesc;
        await _transactionRepository.UpdateAsync(transaction, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new CheckoutResponseDto(transaction.InvoiceNo, transaction.Status.ToString(), tokenResult.WebPaymentUrl);
    }

    private async Task<CheckoutResponseDto> ProcessSavedCardPaymentAsync(
        Transaction transaction, 
        CustomerCard card, 
        Product product, 
        CancellationToken ct)
    {
        // 1. Request Payment Token restricted to this customer token
        var tokenArgs = new PaymentTokenRequestArgs(
            InvoiceNo: transaction.InvoiceNo,
            Amount: transaction.Amount,
            CurrencyCode: transaction.Currency,
            Description: product.Name,
            PaymentChannels: new[] { "CC" },
            CustomerTokens: new[] { card.CustomerToken },
            CustomerTokenOnly: true,
            Request3DS: "N");

        var tokenResult = await _twoC2PPaymentService.RequestPaymentTokenAsync(tokenArgs, ct);
        if (!tokenResult.Success || string.IsNullOrEmpty(tokenResult.PaymentToken))
        {
            transaction.Status = TransactionStatus.Failed;
            transaction.RespCode = tokenResult.RespCode;
            transaction.RespDesc = tokenResult.RespDesc ?? "Failed to request payment token for saved card.";
            await _transactionRepository.UpdateAsync(transaction, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return new CheckoutResponseDto(transaction.InvoiceNo, transaction.Status.ToString(), null);
        }

        // 2. Call Do Payment (Direct API) with plain JSON
        var doPaymentResult = await _twoC2PPaymentService.DoPaymentAsync(
            tokenResult.PaymentToken, 
            card.CustomerToken, 
            ct);

        // 3. Handle Do Payment response codes:
        // If 1001: 3DS redirect required
        if (doPaymentResult.RespCode == "1001" && !string.IsNullOrEmpty(doPaymentResult.ThreeDSUrl))
        {
            transaction.RedirectUrl = doPaymentResult.ThreeDSUrl;
            transaction.RespCode = doPaymentResult.RespCode;
            transaction.RespDesc = doPaymentResult.RespDesc;
            transaction.PaymentChannel = doPaymentResult.ChannelCode ?? "CC";
            transaction.MaskedCardNo = card.MaskedCardNo;
            await _transactionRepository.UpdateAsync(transaction, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return new CheckoutResponseDto(transaction.InvoiceNo, transaction.Status.ToString(), doPaymentResult.ThreeDSUrl);
        }

        // On 2000 (transaction completed) or other non-3DS responses: Confirm with Payment Inquiry!
        // 2000 alone does not mean payment succeeded. Call Payment Inquiry.
        var inquiryResult = await _twoC2PPaymentService.PaymentInquiryAsync(transaction.InvoiceNo, ct);
        if (inquiryResult.Success && inquiryResult.RespCode == "0000")
        {
            // Verify amount & currency
            if (inquiryResult.Amount.HasValue && inquiryResult.Amount.Value == transaction.Amount &&
                (string.IsNullOrEmpty(inquiryResult.CurrencyCode) || inquiryResult.CurrencyCode.Equals(transaction.Currency, StringComparison.OrdinalIgnoreCase)))
            {
                transaction.Status = TransactionStatus.Succeeded;
                transaction.PaidAt = DateTime.UtcNow;
            }
            else
            {
                _logger.LogWarning("Payment Inquiry amount or currency mismatch for invoice {InvoiceNo}", transaction.InvoiceNo);
                transaction.Status = TransactionStatus.Failed;
            }
        }
        else
        {
            transaction.Status = TransactionStatus.Failed;
        }

        transaction.RespCode = inquiryResult.RespCode ?? doPaymentResult.RespCode;
        transaction.RespDesc = inquiryResult.RespDesc ?? doPaymentResult.RespDesc;
        transaction.PaymentChannel = inquiryResult.ChannelCode ?? doPaymentResult.ChannelCode ?? "CC";
        transaction.MaskedCardNo = inquiryResult.AccountNo ?? card.MaskedCardNo;
        transaction.ProviderRef = inquiryResult.TranRef;

        await _transactionRepository.UpdateAsync(transaction, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new CheckoutResponseDto(transaction.InvoiceNo, transaction.Status.ToString(), null);
    }

    private static string GenerateInvoiceNo()
    {
        // Format: INVyyyyMMddHHmmssfff + 3 random digits (Unique, readable, under 50 chars)
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var randomSuffix = Random.Shared.Next(100, 999);
        return $"INV{timestamp}{randomSuffix}";
    }
}
