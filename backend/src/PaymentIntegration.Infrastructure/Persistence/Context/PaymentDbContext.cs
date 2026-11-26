using Microsoft.EntityFrameworkCore;
using PaymentIntegration.Domain.Entities;
using PaymentIntegration.Domain.Interfaces;

namespace PaymentIntegration.Infrastructure.Persistence.Context;

public class PaymentDbContext : DbContext, IUnitOfWork
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<CustomerCard> CustomerCards => Set<CustomerCard>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);
    }
}
