using PaymentIntegration.Domain.Entities;

namespace PaymentIntegration.Domain.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct = default);
}

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default);
}

public interface ICustomerCardRepository
{
    Task<CustomerCard?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerCard>> GetByCustomerIdAsync(int customerId, CancellationToken ct = default);
    Task<CustomerCard?> GetByCustomerAndTokenAsync(int customerId, string customerToken, CancellationToken ct = default);
    Task AddAsync(CustomerCard card, CancellationToken ct = default);
}

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Transaction?> GetByInvoiceNoAsync(string invoiceNo, CancellationToken ct = default);
    Task<Transaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task AddAsync(Transaction transaction, CancellationToken ct = default);
    Task UpdateAsync(Transaction transaction, CancellationToken ct = default);
    Task<int> UpdateStatusConditionalAsync(
        string invoiceNo, 
        Domain.Enums.TransactionStatus expectedCurrentStatus, 
        Domain.Enums.TransactionStatus newStatus, 
        string? respCode, 
        string? respDesc, 
        string? paymentChannel, 
        string? maskedCardNo, 
        string? providerRef, 
        DateTime? paidAt,
        CancellationToken ct = default);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
