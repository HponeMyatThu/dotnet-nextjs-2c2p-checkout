using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Application.Services;
using PaymentIntegration.Domain.Entities;
using PaymentIntegration.Domain.Enums;
using PaymentIntegration.Domain.Exceptions;
using PaymentIntegration.Domain.Interfaces;

namespace PaymentIntegration.UnitTests;

public class CheckoutServiceTests
{
    private readonly Mock<ICustomerRepository>     _customerRepo    = new();
    private readonly Mock<IProductRepository>      _productRepo     = new();
    private readonly Mock<ICustomerCardRepository> _cardRepo        = new();
    private readonly Mock<ITransactionRepository>  _transactionRepo = new();
    private readonly Mock<IUnitOfWork>             _unitOfWork      = new();
    private readonly Mock<ITwoC2PPaymentService>   _paymentService  = new();
    private readonly Mock<IDistributedLockService> _lockService     = new();
    private readonly CheckoutService               _sut;

    public CheckoutServiceTests()
    {
        _sut = new CheckoutService(
            _customerRepo.Object,
            _productRepo.Object,
            _cardRepo.Object,
            _transactionRepo.Object,
            _unitOfWork.Object,
            _paymentService.Object,
            _lockService.Object,
            NullLogger<CheckoutService>.Instance);
    }

    private static Customer MakeCustomer(int id = 1) => new()
    {
        Id = id, FirstName = "Alice", LastName = "Smith", Email = "alice@example.com"
    };

    private static Product MakeProduct(PaymentType type = PaymentType.OneTime) => new()
    {
        Id = 1, Name = "Test Product", Price = 50m, Currency = "SGD", PaymentType = type
    };

    private Mock<IAsyncDisposable> SetupLock()
    {
        var lockHandle = new Mock<IAsyncDisposable>();
        lockHandle.Setup(l => l.DisposeAsync()).Returns(ValueTask.CompletedTask);
        _lockService.Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockHandle.Object);
        return lockHandle;
    }

    [Fact]
    public async Task ProcessCheckout_ExistingIdempotencyKey_ReturnsExisting()
    {
        var key = Guid.NewGuid().ToString();
        var existingTx = new Transaction
        {
            InvoiceNo = "INV001", Status = TransactionStatus.Succeeded, IdempotencyKey = key
        };
        _transactionRepo.Setup(r => r.GetByIdempotencyKeyAsync(key, default)).ReturnsAsync(existingTx);

        var result = await _sut.ProcessCheckoutAsync(key, new CheckoutRequestDto(1, 1, null));

        result.InvoiceNo.Should().Be("INV001");
        result.Status.Should().Be("Succeeded");
        // Payment service should NOT be called
        _paymentService.Verify(s => s.RequestPaymentTokenAsync(It.IsAny<PaymentTokenRequestArgs>(), default), Times.Never);
    }

    [Fact]
    public async Task ProcessCheckout_CustomerNotFound_Throws()
    {
        var key = Guid.NewGuid().ToString();
        _transactionRepo.Setup(r => r.GetByIdempotencyKeyAsync(key, default)).ReturnsAsync((Transaction?)null);
        SetupLock();
        // Double-check under lock returns null too
        _transactionRepo.SetupSequence(r => r.GetByIdempotencyKeyAsync(key, default))
            .ReturnsAsync((Transaction?)null)
            .ReturnsAsync((Transaction?)null);
        _customerRepo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Customer?)null);

        await _sut.Invoking(s => s.ProcessCheckoutAsync(key, new CheckoutRequestDto(99, 1, null)))
            .Should().ThrowAsync<CustomerNotFoundException>();
    }

    [Fact]
    public async Task ProcessCheckout_ProductNotFound_Throws()
    {
        var key = Guid.NewGuid().ToString();
        _transactionRepo.Setup(r => r.GetByIdempotencyKeyAsync(key, default)).ReturnsAsync((Transaction?)null);
        SetupLock();
        _transactionRepo.SetupSequence(r => r.GetByIdempotencyKeyAsync(key, default))
            .ReturnsAsync((Transaction?)null)
            .ReturnsAsync((Transaction?)null);
        _customerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(MakeCustomer());
        _productRepo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Product?)null);

        await _sut.Invoking(s => s.ProcessCheckoutAsync(key, new CheckoutRequestDto(1, 99, null)))
            .Should().ThrowAsync<ProductNotFoundException>();
    }

    [Fact]
    public async Task ProcessCheckout_InvalidIdempotencyKey_Throws()
    {
        await _sut.Invoking(s => s.ProcessCheckoutAsync("not-a-uuid", new CheckoutRequestDto(1, 1, null)))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ProcessCheckout_CardBelongsToDifferentCustomer_ThrowsCardNotFoundException()
    {
        var key = Guid.NewGuid().ToString();
        _transactionRepo.Setup(r => r.GetByIdempotencyKeyAsync(key, default)).ReturnsAsync((Transaction?)null);
        SetupLock();
        _transactionRepo.SetupSequence(r => r.GetByIdempotencyKeyAsync(key, default))
            .ReturnsAsync((Transaction?)null)
            .ReturnsAsync((Transaction?)null);
        _customerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(MakeCustomer(1));
        _productRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(MakeProduct(PaymentType.Recurring));
        // Card belongs to customer 2, not 1
        var card = new CustomerCard { Id = 5, CustomerId = 2, CustomerToken = "tok", MaskedCardNo = "411111******1111" };
        _cardRepo.Setup(r => r.GetByIdAsync(5, default)).ReturnsAsync(card);

        await _sut.Invoking(s => s.ProcessCheckoutAsync(key, new CheckoutRequestDto(1, 1, 5)))
            .Should().ThrowAsync<CustomerCardNotFoundException>();
    }

    [Fact]
    public async Task ProcessCheckout_OneTimeProduct_CallsPaymentTokenAndReturnsRedirectUrl()
    {
        var key = Guid.NewGuid().ToString();
        _transactionRepo.Setup(r => r.GetByIdempotencyKeyAsync(key, default)).ReturnsAsync((Transaction?)null);
        SetupLock();
        _transactionRepo.SetupSequence(r => r.GetByIdempotencyKeyAsync(key, default))
            .ReturnsAsync((Transaction?)null)
            .ReturnsAsync((Transaction?)null);
        _customerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(MakeCustomer());
        _productRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(MakeProduct(PaymentType.OneTime));
        _transactionRepo.Setup(r => r.AddAsync(It.IsAny<Transaction>(), default)).Returns(Task.CompletedTask);
        _transactionRepo.Setup(r => r.UpdateAsync(It.IsAny<Transaction>(), default)).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);
        _paymentService.Setup(s => s.RequestPaymentTokenAsync(It.IsAny<PaymentTokenRequestArgs>(), default))
            .ReturnsAsync(new TwoC2PPaymentTokenResult(true, "tok123", "https://pay.2c2p.com/sandbox?token=abc", "0000", "Success"));

        var result = await _sut.ProcessCheckoutAsync(key, new CheckoutRequestDto(1, 1, null));

        result.RedirectUrl.Should().StartWith("https://pay.2c2p.com");
        result.Status.Should().Be("Pending");
        _paymentService.Verify(s => s.RequestPaymentTokenAsync(It.Is<PaymentTokenRequestArgs>(a => a.Tokenize == false), default), Times.Once);
    }

    [Fact]
    public async Task ProcessCheckout_RecurringProduct_TokenizeEnabled()
    {
        var key = Guid.NewGuid().ToString();
        _transactionRepo.Setup(r => r.GetByIdempotencyKeyAsync(key, default)).ReturnsAsync((Transaction?)null);
        SetupLock();
        _transactionRepo.SetupSequence(r => r.GetByIdempotencyKeyAsync(key, default))
            .ReturnsAsync((Transaction?)null)
            .ReturnsAsync((Transaction?)null);
        _customerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(MakeCustomer());
        _productRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(MakeProduct(PaymentType.Recurring));
        _transactionRepo.Setup(r => r.AddAsync(It.IsAny<Transaction>(), default)).Returns(Task.CompletedTask);
        _transactionRepo.Setup(r => r.UpdateAsync(It.IsAny<Transaction>(), default)).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);
        _paymentService.Setup(s => s.RequestPaymentTokenAsync(It.IsAny<PaymentTokenRequestArgs>(), default))
            .ReturnsAsync(new TwoC2PPaymentTokenResult(true, "tok456", "https://pay.2c2p.com/sandbox?token=xyz", "0000", "Success"));

        await _sut.ProcessCheckoutAsync(key, new CheckoutRequestDto(1, 1, null));

        _paymentService.Verify(s => s.RequestPaymentTokenAsync(
            It.Is<PaymentTokenRequestArgs>(a => a.Tokenize == true && a.PaymentChannels != null && a.PaymentChannels.Contains("CC")),
            default), Times.Once);
    }
}
