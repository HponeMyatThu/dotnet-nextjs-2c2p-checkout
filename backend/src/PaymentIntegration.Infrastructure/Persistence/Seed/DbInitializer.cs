using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PaymentIntegration.Domain.Entities;
using PaymentIntegration.Domain.Enums;
using PaymentIntegration.Infrastructure.Persistence.Context;

namespace PaymentIntegration.Infrastructure.Persistence.Seed;

public static class DbInitializer
{
    public static async Task InitializeAsync(PaymentDbContext context, ILogger logger, CancellationToken ct = default)
    {
        logger.LogInformation("Applying database migrations...");
        await context.Database.MigrateAsync(ct);

        logger.LogInformation("Checking seed data...");

        // Seed Customers if empty
        if (!await context.Customers.AnyAsync(ct))
        {
            var customers = new List<Customer>
            {
                new() { FirstName = "John", LastName = "Doe", Email = "john.doe@example.com" },
                new() { FirstName = "Jane", LastName = "Smith", Email = "jane.smith@example.com" },
                new() { FirstName = "Michael", LastName = "Tan", Email = "michael.tan@example.sg" }
            };

            await context.Customers.AddRangeAsync(customers, ct);
            await context.SaveChangesAsync(ct);
            logger.LogInformation("Seeded 3 customers successfully.");
        }

        // Seed Products if empty
        if (!await context.Products.AnyAsync(ct))
        {
            var products = new List<Product>
            {
                new()
                {
                    Name = "Basic Gym Pass",
                    Description = "Single-entry pass to fitness studio equipment and locker facilities.",
                    Price = 25.00m,
                    Currency = "SGD",
                    PaymentType = PaymentType.OneTime
                },
                new()
                {
                    Name = "Full Day Co-Working Pass",
                    Description = "Access to high-speed internet, dedicated desk, and complimentary coffee for one day.",
                    Price = 50.00m,
                    Currency = "SGD",
                    PaymentType = PaymentType.OneTime
                },
                new()
                {
                    Name = "Monthly Fitness Subscription",
                    Description = "Unlimited access to all gym classes, pool, and facilities billed monthly.",
                    Price = 99.00m,
                    Currency = "SGD",
                    PaymentType = PaymentType.Recurring
                },
                new()
                {
                    Name = "Premium Workspace Membership",
                    Description = "24/7 access to all coworking locations with conference room credits.",
                    Price = 199.00m,
                    Currency = "SGD",
                    PaymentType = PaymentType.Recurring
                }
            };

            await context.Products.AddRangeAsync(products, ct);
            await context.SaveChangesAsync(ct);
            logger.LogInformation("Seeded 4 products (2 OneTime, 2 Recurring) successfully.");
        }
    }
}
