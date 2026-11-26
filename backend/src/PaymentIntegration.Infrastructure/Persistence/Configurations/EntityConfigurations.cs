using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentIntegration.Domain.Entities;

namespace PaymentIntegration.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customer");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(c => c.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Email)
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();

        builder.HasMany(c => c.Cards)
            .WithOne(cc => cc.Customer)
            .HasForeignKey(cc => cc.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Transactions)
            .WithOne(t => t.Customer)
            .HasForeignKey(t => t.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("product");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.Price)
            .HasColumnName("price")
            .HasColumnType("DECIMAL(12,2)")
            .IsRequired();

        builder.Property(p => p.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsFixedLength()
            .HasDefaultValue("SGD")
            .IsRequired();

        builder.Property(p => p.PaymentType)
            .HasColumnName("payment_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasMany(p => p.Transactions)
            .WithOne(t => t.Product)
            .HasForeignKey(t => t.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CustomerCardConfiguration : IEntityTypeConfiguration<CustomerCard>
{
    public void Configure(EntityTypeBuilder<CustomerCard> builder)
    {
        builder.ToTable("customer_card");

        builder.HasKey(cc => cc.Id);
        builder.Property(cc => cc.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(cc => cc.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.Property(cc => cc.CustomerToken)
            .HasColumnName("customer_token")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(cc => cc.MaskedCardNo)
            .HasColumnName("masked_card_no")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(cc => cc.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("DATETIME")
            .IsRequired();

        // Unique composite index on (customer_id, customer_token)
        builder.HasIndex(cc => new { cc.CustomerId, cc.CustomerToken })
            .IsUnique()
            .HasDatabaseName("ix_customer_card_customer_id_token");
    }
}

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transaction");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(t => t.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.Property(t => t.ProductId)
            .HasColumnName("product_id")
            .IsRequired();

        builder.Property(t => t.InvoiceNo)
            .HasColumnName("invoice_no")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.Amount)
            .HasColumnName("amount")
            .HasColumnType("DECIMAL(12,2)")
            .IsRequired();

        builder.Property(t => t.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsFixedLength()
            .HasDefaultValue("SGD")
            .IsRequired();

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.PaymentChannel)
            .HasColumnName("payment_channel")
            .HasMaxLength(20)
            .IsRequired(false);

        builder.Property(t => t.MaskedCardNo)
            .HasColumnName("masked_card_no")
            .HasMaxLength(30)
            .IsRequired(false);

        builder.Property(t => t.ProviderRef)
            .HasColumnName("provider_ref")
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(t => t.RespCode)
            .HasColumnName("resp_code")
            .HasMaxLength(10)
            .IsRequired(false);

        builder.Property(t => t.RespDesc)
            .HasColumnName("resp_desc")
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(t => t.RedirectUrl)
            .HasColumnName("redirect_url")
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("DATETIME")
            .IsRequired();

        builder.Property(t => t.PaidAt)
            .HasColumnName("paid_at")
            .HasColumnType("DATETIME")
            .IsRequired(false);

        // Required Unique Constraints
        builder.HasIndex(t => t.InvoiceNo)
            .IsUnique()
            .HasDatabaseName("ix_transaction_invoice_no");

        builder.HasIndex(t => t.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ix_transaction_idempotency_key");

        builder.HasIndex(t => t.Status)
            .HasDatabaseName("ix_transaction_status");
    }
}
