using System.Collections.Concurrent;

namespace ArticleService.Services;

public class InMemoryCacheService : IInMemoryCacheService
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ReaderWriterLockSlim _cacheLock = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _stampedeLocks = new();
    private readonly ILogger<InMemoryCacheService> _logger;

    private class CacheEntry
    {
        public object? Value { get; set; }
        public DateTime? ExpiresAt { get; set; }

        public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
    }

    public InMemoryCacheService(ILogger<InMemoryCacheService> logger)
    {
        _logger = logger;
    }

    public T? Get<T>(string key)
    {
        _cacheLock.EnterReadLock();
        try
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.IsExpired)
                {
                    _cacheLock.ExitReadLock();
                    _cacheLock.EnterWriteLock();
                    try
                    {
                        if (_cache.TryGetValue(key, out var entry2) && entry2.IsExpired)
                        {
                            _cache.TryRemove(key, out _);
                            _logger.LogDebug("Cache entry expired and removed: {CacheKey}", key);
                        }
                    }
                    finally
                    {
                        _cacheLock.ExitWriteLock();
                    }
                    return default;
                }

                _logger.LogDebug("Cache hit: {CacheKey}", key);
                return (T?)entry.Value;
            }

            _logger.LogDebug("Cache miss: {CacheKey}", key);
            return default;
        }
        finally
        {
            _cacheLock.ExitReadLock();
        }
    }

    public async Task<T?> GetOrFetchAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiration = null)
    {
        var cached = Get<T>(key);
        if (cached != null)
            return cached;

        var semaphore = _stampedeLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync();
        try
        {
            cached = Get<T>(key);
            if (cached != null)
            {
                _logger.LogDebug("Cache hit after waiting: {CacheKey}", key);
                return cached;
            }

            _logger.LogInformation("Cache miss - fetching fresh data: {CacheKey}", key);
            var result = await factory();

            if (result != null)
            {
                Set(key, result, expiration);
                _logger.LogInformation("Cache populated: {CacheKey}", key);
            }

            return result;
        }
        finally
        {
            semaphore.Release();

            if (semaphore.CurrentCount == 1)
            {
                _stampedeLocks.TryRemove(key, out _);
            }
        }
    }

    public void Set<T>(string key, T value, TimeSpan? expiration = null)
    {
        _cacheLock.EnterWriteLock();
        try
        {
            var entry = new CacheEntry
            {
                Value = value,
                ExpiresAt = expiration.HasValue ? DateTime.UtcNow.Add(expiration.Value) : null
            };

            _cache[key] = entry;

            string expiryInfo = expiration.HasValue
                ? $" (expires in {expiration.Value.TotalSeconds}s)"
                : " (no expiry)";

            _logger.LogDebug("Cache set: {CacheKey}{ExpiryInfo}", key, expiryInfo);
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    public void Remove(string key)
    {
        _cacheLock.EnterWriteLock();
        try
        {
            if (_cache.TryRemove(key, out _))
            {
                _logger.LogDebug("Cache entry removed: {CacheKey}", key);
            }
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    public void RemoveByPrefix(string prefix)
    {
        _cacheLock.EnterWriteLock();
        try
        {
            var keysToRemove = _cache.Keys
                .Where(k => k.StartsWith(prefix))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
            }

            _logger.LogInformation(
                "Removed {Count} cache entries with prefix: {Prefix}",
                keysToRemove.Count, prefix);
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    public void Clear()
    {
        _cacheLock.EnterWriteLock();
        try
        {
            int count = _cache.Count;
            _cache.Clear();
            _logger.LogWarning("Cache cleared. Removed {Count} entries", count);
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    public void LogStatistics()
    {
        _cacheLock.EnterReadLock();
        try
        {
            int totalEntries = _cache.Count;
            int expiredEntries = _cache.Values.Count(e => e.IsExpired);
            int validEntries = totalEntries - expiredEntries;

            _logger.LogInformation(
                "Cache statistics - Total: {Total}, Valid: {Valid}, Expired: {Expired}",
                totalEntries, validEntries, expiredEntries);
        }
        finally
        {
            _cacheLock.ExitReadLock();
        }
    }

    public void Dispose()
    {
        _cacheLock?.Dispose();
        foreach (var semaphore in _stampedeLocks.Values)
        {
            semaphore?.Dispose();
        }
    }
}
