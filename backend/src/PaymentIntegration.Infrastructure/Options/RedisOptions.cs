namespace PaymentIntegration.Infrastructure.Options;

public class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379";
    public int DefaultCacheTtlMinutes { get; set; } = 10;
    public int DefaultLockTtlSeconds { get; set; } = 15;
}
