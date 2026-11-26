using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Infrastructure.Persistence.Context;
using PaymentIntegration.Infrastructure.Options;
using Testcontainers.MySql;
using Testcontainers.Redis;

namespace PaymentIntegration.ConcurrencyTests;

/// <summary>
/// MANDATORY: Concurrency tests proving that 10+ simultaneous checkout requests
/// with the SAME Idempotency-Key result in exactly ONE transaction and ONE payment API call.
/// </summary>
public class CheckoutConcurrencyTests : IAsyncLifetime
{
    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithDatabase("payment_concurrency_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder().Build();

    private WebApplicationFactory<Program> _factory = null!;
    private readonly Mock<ITwoC2PPaymentService> _mockPaymentService = new();
    private int _paymentTokenCallCount;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_mysql.StartAsync(), _redis.StartAsync());

        // Track payment token call count in a thread-safe manner
        _mockPaymentService
            .Setup(s => s.RequestPaymentTokenAsync(It.IsAny<PaymentTokenRequestArgs>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                Interlocked.Increment(ref _paymentTokenCallCount);
                return new TwoC2PPaymentTokenResult(true, "tok_test", "https://pay.2c2p.com/sandbox?token=abc", "0000", "Success");
            });

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.ConfigureServices(services =>
            {
                // Replace DB
                services.RemoveAll<DbContextOptions<PaymentDbContext>>();
                services.AddDbContext<PaymentDbContext>(opts =>
                    opts.UseMySql(
                        _mysql.GetConnectionString(),
                        ServerVersion.AutoDetect(_mysql.GetConnectionString()),
                        b => b.MigrationsAssembly("PaymentIntegration.Infrastructure")));

                // Replace Redis
                services.Configure<RedisOptions>(o =>
                    o.ConnectionString = _redis.GetConnectionString());

                // Replace payment service with our mock
                services.RemoveAll<ITwoC2PPaymentService>();
                services.AddSingleton(_mockPaymentService.Object);
            });
        });

        // Run migrations
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await Task.WhenAll(_mysql.StopAsync(), _redis.StopAsync());
    }

    [Fact]
    public async Task TenConcurrentCheckoutsWithSameIdempotencyKey_ProducesExactlyOneTransaction()
    {
        const int concurrentRequests = 10;
        const int customerId = 1;   // Seeded customer
        const int productId  = 1;   // Seeded OneTime product
        var idempotencyKey = Guid.NewGuid().ToString();

        // Reset counter
        Interlocked.Exchange(ref _paymentTokenCallCount, 0);

        // --- ACT: Fire 10 simultaneous requests with the same idempotency key ---
        var tasks = Enumerable.Range(0, concurrentRequests).Select(_ =>
        {
            var client = _factory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkout")
            {
                Content = JsonContent.Create(new { customerId, productId }),
            };
            request.Headers.Add("Idempotency-Key", idempotencyKey);
            return client.SendAsync(request);
        }).ToList();

        var responses = await Task.WhenAll(tasks);

        // --- ASSERT ---

        // All requests should succeed (200 OK or 409 Conflict — but the winning one must be 200)
        var successResponses = responses.Where(r => r.StatusCode == HttpStatusCode.OK).ToList();
        var conflictResponses = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToList();

        successResponses.Count.Should().BeGreaterThanOrEqualTo(1,
            because: "at least one request should succeed");

        (successResponses.Count + conflictResponses.Count).Should().Be(concurrentRequests,
            because: "all requests should return either 200 or 409");

        // All 200 responses should return the SAME invoiceNo
        var invoiceNos = new HashSet<string>();
        foreach (var r in successResponses)
        {
            var body = await r.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<JsonElement>(body);
            if (json.TryGetProperty("invoiceNo", out var inv))
                invoiceNos.Add(inv.GetString()!);
        }
        invoiceNos.Should().HaveCount(1, because: "all 200 responses must return the same invoiceNo");

        // Payment token API called EXACTLY ONCE
        _paymentTokenCallCount.Should().Be(1,
            because: "the payment gateway must only be called once regardless of concurrent requests");

        // DB should contain exactly ONE transaction with this idempotency key
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var txCount = await db.Transactions
            .Where(t => t.IdempotencyKey == idempotencyKey)
            .CountAsync();
        txCount.Should().Be(1, because: "exactly one transaction row must be created");
    }

    [Fact]
    public async Task FifteenConcurrentCheckoutsWithDifferentKeys_ProduceFifteenTransactions()
    {
        const int concurrentRequests = 15;
        const int customerId = 1;
        const int productId  = 1;

        Interlocked.Exchange(ref _paymentTokenCallCount, 0);

        var tasks = Enumerable.Range(0, concurrentRequests).Select(_ =>
        {
            var client = _factory.CreateClient();
            var uniqueKey = Guid.NewGuid().ToString(); // unique per request
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkout")
            {
                Content = JsonContent.Create(new { customerId, productId }),
            };
            request.Headers.Add("Idempotency-Key", uniqueKey);
            return client.SendAsync(request);
        }).ToList();

        var responses = await Task.WhenAll(tasks);

        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        successCount.Should().Be(concurrentRequests,
            because: "all 15 requests with unique keys should succeed");

        _paymentTokenCallCount.Should().Be(concurrentRequests,
            because: "each unique checkout should trigger exactly one payment token request");
    }
}
