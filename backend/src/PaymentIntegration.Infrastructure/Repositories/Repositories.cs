using Microsoft.EntityFrameworkCore;
using PaymentIntegration.Domain.Entities;
using PaymentIntegration.Domain.Enums;
using PaymentIntegration.Domain.Interfaces;
using PaymentIntegration.Infrastructure.Persistence.Context;

namespace PaymentIntegration.Infrastructure.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly PaymentDbContext _context;

    public CustomerRepository(PaymentDbContext context)
    {
        _context = context;
    }

    public async Task<Customer?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.Customers
            .Include(c => c.Cards)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Customers
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .ToListAsync(ct);
    }
}

public class ProductRepository : IProductRepository
{
    private readonly PaymentDbContext _context;

    public ProductRepository(PaymentDbContext context)
    {
        _context = context;
    }

    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Products
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .ToListAsync(ct);
    }
}

public class CustomerCardRepository : ICustomerCardRepository
{
    private readonly PaymentDbContext _context;

    public CustomerCardRepository(PaymentDbContext context)
    {
        _context = context;
    }

    public async Task<CustomerCard?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.CustomerCards
            .FirstOrDefaultAsync(cc => cc.Id == id, ct);
    }

    public async Task<IReadOnlyList<CustomerCard>> GetByCustomerIdAsync(int customerId, CancellationToken ct = default)
    {
        return await _context.CustomerCards
            .AsNoTracking()
            .Where(cc => cc.CustomerId == customerId)
            .OrderByDescending(cc => cc.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<CustomerCard?> GetByCustomerAndTokenAsync(int customerId, string customerToken, CancellationToken ct = default)
    {
        return await _context.CustomerCards
            .FirstOrDefaultAsync(cc => cc.CustomerId == customerId && cc.CustomerToken == customerToken, ct);
    }

    public async Task AddAsync(CustomerCard card, CancellationToken ct = default)
    {
        await _context.CustomerCards.AddAsync(card, ct);
    }
}

public class TransactionRepository : ITransactionRepository
{
    private readonly PaymentDbContext _context;

    public TransactionRepository(PaymentDbContext context)
    {
        _context = context;
    }

    public async Task<Transaction?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.Transactions
            .Include(t => t.Customer)
            .Include(t => t.Product)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<Transaction?> GetByInvoiceNoAsync(string invoiceNo, CancellationToken ct = default)
    {
        return await _context.Transactions
            .Include(t => t.Customer)
            .Include(t => t.Product)
            .FirstOrDefaultAsync(t => t.InvoiceNo == invoiceNo, ct);
    }

    public async Task<Transaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        return await _context.Transactions
            .Include(t => t.Customer)
            .Include(t => t.Product)
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, ct);
    }

    public async Task AddAsync(Transaction transaction, CancellationToken ct = default)
    {
        await _context.Transactions.AddAsync(transaction, ct);
    }

    public Task UpdateAsync(Transaction transaction, CancellationToken ct = default)
    {
        _context.Transactions.Update(transaction);
        return Task.CompletedTask;
    }

    public async Task<int> UpdateStatusConditionalAsync(
        string invoiceNo, 
        TransactionStatus expectedCurrentStatus, 
        TransactionStatus newStatus, 
        string? respCode, 
        string? respDesc, 
        string? paymentChannel, 
        string? maskedCardNo, 
        string? providerRef, 
        DateTime? paidAt, 
        CancellationToken ct = default)
    {
        // Executes atomic SQL conditional UPDATE WHERE invoice_no = @invoiceNo AND status = @expectedCurrentStatus
        return await _context.Transactions
            .Where(t => t.InvoiceNo == invoiceNo && t.Status == expectedCurrentStatus)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, newStatus)
                .SetProperty(t => t.RespCode, respCode)
                .SetProperty(t => t.RespDesc, respDesc)
                .SetProperty(t => t.PaymentChannel, paymentChannel)
                .SetProperty(t => t.MaskedCardNo, maskedCardNo)
                .SetProperty(t => t.ProviderRef, providerRef)
                .SetProperty(t => t.PaidAt, paidAt),
                ct);
    }
}
