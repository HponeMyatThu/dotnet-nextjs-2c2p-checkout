namespace PaymentIntegration.Application.Interfaces;

public interface IDistributedLockService
{
    /// <summary>
    /// Attempts to acquire a distributed lock on the specified resource key.
    /// Returns an IAsyncDisposable that releases the lock if acquired, or null if lock acquisition failed/timed out.
    /// </summary>
    Task<IAsyncDisposable?> TryAcquireLockAsync(
        string lockKey, 
        TimeSpan timeout, 
        TimeSpan lockExpiry, 
        CancellationToken ct = default);
}

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}
