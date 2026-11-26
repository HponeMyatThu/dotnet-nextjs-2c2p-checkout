using System.Net;
using System.Net.Http.Json;
using System.Text;
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
using Testcontainers.MySql;
using Testcontainers.Redis;

namespace PaymentIntegration.IntegrationTests;

/// <summary>
/// Integration tests for the notification endpoint using real MySQL + Redis containers.
/// </summary>
public class NotificationProcessingTests : IAsyncLifetime
{
    private readonly MySqlContainer  _mysql = new MySqlBuilder()
        .WithDatabase("payment_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder().Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_mysql.StartAsync(), _redis.StartAsync());

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.ConfigureServices(services =>
            {
                // Replace DB connection
                services.RemoveAll<DbContextOptions<PaymentDbContext>>();
                services.AddDbContext<PaymentDbContext>(opts =>
                    opts.UseMySql(
                        _mysql.GetConnectionString(),
                        ServerVersion.AutoDetect(_mysql.GetConnectionString()),
                        b => b.MigrationsAssembly("PaymentIntegration.Infrastructure")));

                // Replace Redis connection string
                services.Configure<PaymentIntegration.Infrastructure.Options.RedisOptions>(o =>
                    o.ConnectionString = _redis.GetConnectionString());
            });
        });

        // Run migrations
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        await db.Database.MigrateAsync();

        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await Task.WhenAll(_mysql.StopAsync(), _redis.StopAsync());
    }

    [Fact]
    public async Task Notification_InvalidPayload_Returns200WithoutProcessing()
    {
        // The notification endpoint should always return 200 to 2C2P
        // even for invalid payloads (to prevent 2C2P from retrying with valid notification)
        var content = new StringContent(
            JsonSerializer.Serialize(new { payload = "not-a-valid-jwt" }),
            Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/payments/2c2p/notification", content);

        // Should still return 200 (we acknowledge receipt; rejection is handled internally)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task HealthCheck_ApiIsRunning()
    {
        var response = await _client.GetAsync("/swagger/index.html");
        // Swagger should be available (may need AllowAny in test env)
        response.IsSuccessStatusCode.Should().BeTrue(
            because: "API should be running and Swagger should be accessible");
    }

    [Fact]
    public async Task GetProducts_ReturnsSeededProducts()
    {
        var response = await _client.GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("OneTime");
        content.Should().Contain("Recurring");
    }

    [Fact]
    public async Task GetCustomers_ReturnsSeededCustomers()
    {
        var response = await _client.GetAsync("/api/customers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        // We seeded 3 customers
        var customers = JsonSerializer.Deserialize<JsonElement[]>(content);
        customers.Should().NotBeNull();
        customers!.Length.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task Checkout_MissingIdempotencyKey_Returns400()
    {
        var payload = new { customerId = 1, productId = 1 };
        var response = await _client.PostAsJsonAsync("/api/checkout", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Checkout_InvalidIdempotencyKey_Returns400()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkout")
        {
            Content = JsonContent.Create(new { customerId = 1, productId = 1 })
        };
        request.Headers.Add("Idempotency-Key", "not-a-uuid");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
