using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Application.Services;
using PaymentIntegration.Domain.Entities;
using PaymentIntegration.Domain.Enums;
using PaymentIntegration.Domain.Interfaces;

namespace PaymentIntegration.UnitTests;

public class PaymentNotificationServiceTests
{
    private readonly Mock<ITransactionRepository>  _transactionRepo = new();
    private readonly Mock<ICustomerCardRepository> _cardRepo        = new();
    private readonly Mock<IUnitOfWork>             _unitOfWork      = new();
    private readonly Mock<ITwoC2PPaymentService>   _paymentService  = new();
    private readonly Mock<ICacheService>           _cacheService    = new();
    private readonly PaymentNotificationService    _sut;

    public PaymentNotificationServiceTests()
    {
        _sut = new PaymentNotificationService(
            _transactionRepo.Object,
            _cardRepo.Object,
            _unitOfWork.Object,
            _paymentService.Object,
            _cacheService.Object,
            NullLogger<PaymentNotificationService>.Instance);
    }

    private static Transaction PendingTransaction(string invoiceNo = "INV001", decimal amount = 100m) => new()
    {
        Id = 1, InvoiceNo = invoiceNo, CustomerId = 1, ProductId = 1,
        Amount = amount, Currency = "SGD", Status = TransactionStatus.Pending,
        IdempotencyKey = Guid.NewGuid().ToString(), CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task HandleNotification_InvalidSignature_IsRejected()
    {
        _paymentService.Setup(s => s.VerifyAndDecodeNotificationAsync("bad-jwt", default))
            .ReturnsAsync(new TwoC2PDecodedNotification(
                IsValid: false, null, null, null, null, null, null, null, null, null,
                Error: "Invalid signature"));

        await _sut.HandleNotificationAsync("bad-jwt");

        // Transaction should NOT be touched
        _transactionRepo.Verify(r => r.GetByInvoiceNoAsync(It.IsAny<string>(), default), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task HandleNotification_ValidSuccess_UpdatesStatusConditionally()
    {
        var tx = PendingTransaction("INV001", 100m);
        _paymentService.Setup(s => s.VerifyAndDecodeNotificationAsync("valid-jwt", default))
            .ReturnsAsync(new TwoC2PDecodedNotification(
                IsValid: true, InvoiceNo: "INV001", Amount: 100m, CurrencyCode: "SGD",
                RespCode: "0000", RespDesc: "Success", ChannelCode: "CC",
                AccountNo: "411111******1111", TranRef: "REF001", CustomerToken: null, Error: null));
        _transactionRepo.Setup(r => r.GetByInvoiceNoAsync("INV001", default)).ReturnsAsync(tx);
        _transactionRepo.Setup(r => r.UpdateStatusConditionalAsync(
            "INV001", TransactionStatus.Pending, TransactionStatus.Succeeded,
            "0000", "Success", "CC", "411111******1111", "REF001", It.IsAny<DateTime?>(), default))
            .ReturnsAsync(1);

        var result = await _sut.HandleNotificationAsync("valid-jwt");

        result.Success.Should().BeTrue();
        result.Status.Should().Be("Succeeded");
        _transactionRepo.Verify(r => r.UpdateStatusConditionalAsync(
            "INV001", TransactionStatus.Pending, TransactionStatus.Succeeded,
            "0000", "Success", "CC", "411111******1111", "REF001", It.IsAny<DateTime?>(), default), Times.Once);
    }

    [Fact]
    public async Task HandleNotification_AmountMismatch_MarksTransactionFailed()
    {
        var tx = PendingTransaction("INV001", 100m);
        _paymentService.Setup(s => s.VerifyAndDecodeNotificationAsync("jwt", default))
            .ReturnsAsync(new TwoC2PDecodedNotification(
                IsValid: true, InvoiceNo: "INV001", Amount: 999m, // wrong amount
                CurrencyCode: "SGD", RespCode: "0000", RespDesc: "Success",
                ChannelCode: "CC", AccountNo: null, TranRef: null, CustomerToken: null, Error: null));
        _transactionRepo.Setup(r => r.GetByInvoiceNoAsync("INV001", default)).ReturnsAsync(tx);
        _transactionRepo.Setup(r => r.UpdateStatusConditionalAsync(
            "INV001", TransactionStatus.Pending, TransactionStatus.Failed,
            "ERR_AMOUNT", It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), null, default))
            .ReturnsAsync(1);

        var result = await _sut.HandleNotificationAsync("jwt");

        result.Success.Should().BeFalse();
        result.Status.Should().Be("Failed");
        _transactionRepo.Verify(r => r.UpdateStatusConditionalAsync(
            "INV001", TransactionStatus.Pending, TransactionStatus.Failed,
            "ERR_AMOUNT", It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), null, default), Times.Once);
    }

    [Fact]
    public async Task HandleNotification_CurrencyMismatch_MarksTransactionFailed()
    {
        var tx = PendingTransaction("INV001", 100m);
        _paymentService.Setup(s => s.VerifyAndDecodeNotificationAsync("jwt", default))
            .ReturnsAsync(new TwoC2PDecodedNotification(
                IsValid: true, InvoiceNo: "INV001", Amount: 100m,
                CurrencyCode: "USD", // wrong currency
                RespCode: "0000", RespDesc: "Success",
                ChannelCode: "CC", AccountNo: null, TranRef: null, CustomerToken: null, Error: null));
        _transactionRepo.Setup(r => r.GetByInvoiceNoAsync("INV001", default)).ReturnsAsync(tx);
        _transactionRepo.Setup(r => r.UpdateStatusConditionalAsync(
            "INV001", TransactionStatus.Pending, TransactionStatus.Failed,
            "ERR_CURRENCY", It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), null, default))
            .ReturnsAsync(1);

        var result = await _sut.HandleNotificationAsync("jwt");

        result.Success.Should().BeFalse();
        result.Status.Should().Be("Failed");
        _transactionRepo.Verify(r => r.UpdateStatusConditionalAsync(
            "INV001", TransactionStatus.Pending, TransactionStatus.Failed,
            "ERR_CURRENCY", It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), null, default), Times.Once);
    }

    [Fact]
    public async Task HandleNotification_AlreadyFinalState_IsIgnored()
    {
        var tx = PendingTransaction("INV001", 100m);
        tx.Status = TransactionStatus.Succeeded; // already final
        _paymentService.Setup(s => s.VerifyAndDecodeNotificationAsync("jwt", default))
            .ReturnsAsync(new TwoC2PDecodedNotification(
                IsValid: true, InvoiceNo: "INV001", Amount: 100m, CurrencyCode: "SGD",
                RespCode: "0000", RespDesc: "Success", ChannelCode: "CC",
                AccountNo: null, TranRef: null, CustomerToken: null, Error: null));
        _transactionRepo.Setup(r => r.GetByInvoiceNoAsync("INV001", default)).ReturnsAsync(tx);

        await _sut.HandleNotificationAsync("jwt");

        // Should not call conditional update since already in final state
        _transactionRepo.Verify(r => r.UpdateStatusConditionalAsync(
            It.IsAny<string>(), It.IsAny<TransactionStatus>(), It.IsAny<TransactionStatus>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleNotification_WithCustomerToken_SavesCard()
    {
        var tx = PendingTransaction("INV001", 100m);
        _paymentService.Setup(s => s.VerifyAndDecodeNotificationAsync("jwt", default))
            .ReturnsAsync(new TwoC2PDecodedNotification(
                IsValid: true, InvoiceNo: "INV001", Amount: 100m, CurrencyCode: "SGD",
                RespCode: "0000", RespDesc: "Success", ChannelCode: "CC",
                AccountNo: "411111******1111", TranRef: "REF001",
                CustomerToken: "CUSTOMER_TOKEN_ABC", Error: null));
        _transactionRepo.Setup(r => r.GetByInvoiceNoAsync("INV001", default)).ReturnsAsync(tx);
        _transactionRepo.Setup(r => r.UpdateStatusConditionalAsync(
            "INV001", TransactionStatus.Pending, TransactionStatus.Succeeded,
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), default))
            .ReturnsAsync(1);
        // Card does NOT exist yet
        _cardRepo.Setup(r => r.GetByCustomerAndTokenAsync(1, "CUSTOMER_TOKEN_ABC", default))
            .ReturnsAsync((CustomerCard?)null);
        _cardRepo.Setup(r => r.AddAsync(It.IsAny<CustomerCard>(), default)).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        await _sut.HandleNotificationAsync("jwt");

        _cardRepo.Verify(r => r.AddAsync(It.Is<CustomerCard>(c =>
            c.CustomerId == 1 &&
            c.CustomerToken == "CUSTOMER_TOKEN_ABC" &&
            c.MaskedCardNo == "411111******1111"
        ), default), Times.Once);
    }

    [Fact]
    public async Task HandleNotification_DuplicateCard_NotSavedTwice()
    {
        var tx = PendingTransaction("INV001", 100m);
        _paymentService.Setup(s => s.VerifyAndDecodeNotificationAsync("jwt", default))
            .ReturnsAsync(new TwoC2PDecodedNotification(
                IsValid: true, InvoiceNo: "INV001", Amount: 100m, CurrencyCode: "SGD",
                RespCode: "0000", RespDesc: "Success", ChannelCode: "CC",
                AccountNo: "411111******1111", TranRef: null,
                CustomerToken: "EXISTING_TOKEN", Error: null));
        _transactionRepo.Setup(r => r.GetByInvoiceNoAsync("INV001", default)).ReturnsAsync(tx);
        _transactionRepo.Setup(r => r.UpdateStatusConditionalAsync(
            "INV001", TransactionStatus.Pending, TransactionStatus.Succeeded,
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), default))
            .ReturnsAsync(1);
        // Card ALREADY exists
        _cardRepo.Setup(r => r.GetByCustomerAndTokenAsync(1, "EXISTING_TOKEN", default))
            .ReturnsAsync(new CustomerCard { CustomerId = 1, CustomerToken = "EXISTING_TOKEN", MaskedCardNo = "411111******1111" });
        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        await _sut.HandleNotificationAsync("jwt");

        _cardRepo.Verify(r => r.AddAsync(It.IsAny<CustomerCard>(), default), Times.Never);
    }
}
