using Microsoft.Extensions.Logging;
using PaymentIntegration.Application.Interfaces;
using StackExchange.Redis;

namespace PaymentIntegration.Infrastructure.Locking;

public class RedisDistributedLockService : IDistributedLockService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisDistributedLockService> _logger;

    // Lua script: compare-and-delete to ensure only the lock owner can release the lock
    private const string ReleaseLockScript = @"
        if redis.call('get', KEYS[1]) == ARGV[1] then
            return redis.call('del', KEYS[1])
        else
            return 0
        end";

    public RedisDistributedLockService(
        IConnectionMultiplexer redis, 
        ILogger<RedisDistributedLockService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<IAsyncDisposable?> TryAcquireLockAsync(
        string lockKey, 
        TimeSpan timeout, 
        TimeSpan lockExpiry, 
        CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var lockValue = Guid.NewGuid().ToString("N"); // Unique owner token per acquisition
        var startTime = DateTime.UtcNow;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // SET key value NX PX <lockExpiry>
                bool acquired = await db.StringSetAsync(
                    lockKey, 
                    lockValue, 
                    lockExpiry, 
                    When.NotExists);

                if (acquired)
                {
                    _logger.LogDebug("Acquired distributed lock {LockKey} with token {LockValue}", lockKey, lockValue);
                    return new RedisLockHandle(db, lockKey, lockValue, _logger);
                }
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Redis connection error while acquiring lock {LockKey}", lockKey);
                // Return null to let the caller handle Redis unavailability or fallback
                return null;
            }

            if (DateTime.UtcNow - startTime >= timeout)
            {
                _logger.LogWarning("Timed out waiting to acquire distributed lock {LockKey} after {TimeoutMs}ms", 
                    lockKey, timeout.TotalMilliseconds);
                break;
            }

            // Exponential / randomized jitter wait before retry (25ms - 75ms)
            var delayMs = Random.Shared.Next(25, 75);
            await Task.Delay(delayMs, ct);
        }

        return null;
    }

    private sealed class RedisLockHandle : IAsyncDisposable
    {
        private readonly IDatabase _db;
        private readonly string _lockKey;
        private readonly string _lockValue;
        private readonly ILogger _logger;
        private int _isDisposed;

        public RedisLockHandle(IDatabase db, string lockKey, string lockValue, ILogger logger)
        {
            _db = db;
            _lockKey = lockKey;
            _lockValue = lockValue;
            _logger = logger;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            {
                return;
            }

            try
            {
                // Execute compare-and-delete Lua script
                var result = await _db.ScriptEvaluateAsync(
                    ReleaseLockScript,
                    new RedisKey[] { _lockKey },
                    new RedisValue[] { _lockValue });

                if ((int)result == 1)
                {
                    _logger.LogDebug("Successfully released distributed lock {LockKey} for owner {LockValue}", _lockKey, _lockValue);
                }
                else
                {
                    _logger.LogWarning("Lock {LockKey} was already expired or claimed by another owner during release.", _lockKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to release distributed lock {LockKey}", _lockKey);
            }
        }
    }
}
