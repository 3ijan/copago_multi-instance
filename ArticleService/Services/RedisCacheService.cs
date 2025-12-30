using StackExchange.Redis;
using System.Text.Json;

namespace ArticleService.Services;

public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(
        IConnectionMultiplexer redis,
        ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
        _db = redis.GetDatabase();
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var value = await _db.StringGetAsync(key);
            if (value.IsNull)
                return default;

            return JsonSerializer.Deserialize<T>(value.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache key {CacheKey}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        try
        {
            var serialized = JsonSerializer.Serialize(value);
            await _db.StringSetAsync(key, serialized, expiration, When.Always);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache key {CacheKey}", key);
        }
    }

    public async Task InvalidateAsync(string key)
    {
        try
        {
            await _db.KeyDeleteAsync(key);
            _logger.LogDebug("Cache invalidated: {CacheKey}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache key {CacheKey}", key);
        }
    }
}
