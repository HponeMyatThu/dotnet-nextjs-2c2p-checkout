using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using PaymentIntegration.API.Filters;
using PaymentIntegration.API.Middleware;
using PaymentIntegration.Application;
using PaymentIntegration.Infrastructure;
using PaymentIntegration.Infrastructure.Persistence.Context;
using PaymentIntegration.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Application and Infrastructure services (Clean Architecture)
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// 2. Add Controllers and JSON configuration
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// 3. Configure CORS for Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "http://127.0.0.1:3000",
                "http://frontend:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders("Idempotency-Key");
    });
});

// 4. Configure Swagger / OpenAPI documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Rezerv 2C2P Payment Integration API",
        Version = "v1",
        Description = "Full-stack payment integration API utilizing 2C2P Payment API v4.3 sandbox.\n\n" +
                      "Supports OneTime hosted checkout, Recurring hosted checkout with card tokenization, and Saved-Card direct checkout with Redis distributed locking."
    });

    options.OperationFilter<IdempotencyKeyHeaderFilter>();

    // Include XML comments if present
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// 5. Database Migration and Seeding on Startup
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

    try
    {
        logger.LogInformation("Initializing database schema and seed data...");
        await DbInitializer.InitializeAsync(dbContext, logger);
        logger.LogInformation("Database initialization completed successfully.");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not initialize database on startup (may be waiting for MySQL container). Retrying or continuing...");
    }
}

// 6. Middleware pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Rezerv 2C2P Payment API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowFrontend");

app.UseAuthorization();

app.MapControllers();

app.Run();

// Required for WebApplicationFactory in Integration/Concurrency tests
public partial class Program { }
