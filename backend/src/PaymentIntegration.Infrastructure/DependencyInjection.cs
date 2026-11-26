using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Domain.Interfaces;
using PaymentIntegration.Infrastructure.Caching;
using PaymentIntegration.Infrastructure.Locking;
using PaymentIntegration.Infrastructure.Options;
using PaymentIntegration.Infrastructure.Payments.TwoC2P.Clients;
using PaymentIntegration.Infrastructure.Payments.TwoC2P.Jwt;
using PaymentIntegration.Infrastructure.Payments.TwoC2P.Options;
using PaymentIntegration.Infrastructure.Payments.TwoC2P.Services;
using PaymentIntegration.Infrastructure.Persistence.Context;
using PaymentIntegration.Infrastructure.Repositories;
using StackExchange.Redis;

namespace PaymentIntegration.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // 1. MySQL EF Core configuration
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Server=localhost;Port=3306;Database=payment_db;User=payment_user;Password=payment_password;";

        services.AddDbContext<PaymentDbContext>(options =>
        {
            options.UseMySql(
                connectionString, 
                new MySqlServerVersion(new Version(8, 0, 36)),
                mySqlOptions =>
                {
                    mySqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3, 
                        maxRetryDelay: TimeSpan.FromSeconds(5), 
                        errorNumbersToAdd: null);
                });
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PaymentDbContext>());
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICustomerCardRepository, CustomerCardRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();

        // 2. Redis Configuration
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var redisOptions = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
            var config = ConfigurationOptions.Parse(redisOptions.ConnectionString);
            config.AbortOnConnectFail = false; // Do not crash host if Redis is temporarily connecting
            config.ConnectTimeout = 5000;
            config.SyncTimeout = 5000;
            return ConnectionMultiplexer.Connect(config);
        });

        services.AddSingleton<IDistributedLockService, RedisDistributedLockService>();
        services.AddSingleton<ICacheService, RedisCacheService>();

        // 3. 2C2P Payments Configuration
        services.Configure<TwoC2POptions>(configuration.GetSection(TwoC2POptions.SectionName));
        services.AddSingleton<ITwoC2PJwtService, TwoC2PJwtService>();

        services.AddHttpClient<TwoC2PClient>((sp, client) =>
        {
            var twoC2POptions = sp.GetRequiredService<IOptions<TwoC2POptions>>().Value;
            client.BaseAddress = new Uri(twoC2POptions.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(twoC2POptions.TimeoutSeconds);
        });

        services.AddScoped<ITwoC2PPaymentService, TwoC2PPaymentService>();

        return services;
    }
}
