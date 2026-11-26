using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PaymentIntegration.Infrastructure.Persistence.Context;

namespace PaymentIntegration.Infrastructure.Persistence;

public class PaymentDbContextFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    public PaymentDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PaymentDbContext>();
        var connectionString = "Server=localhost;Port=3306;Database=payment_db;User=payment_user;Password=payment_password;";
        optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)));

        return new PaymentDbContext(optionsBuilder.Options);
    }
}
