using System.Text.Json;
using StackExchange.Redis;

namespace ArticleService.Services;

public class RedisCachePublisherService : ICachePublisherService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCachePublisherService> _logger;
    private const string ChannelName = "cache-invalidation";

    public RedisCachePublisherService(
        IConnectionMultiplexer redis,
        ILogger<RedisCachePublisherService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<long> PublishInvalidationAsync(string cacheKey)
    {
        try
        {
            var subscriber = _redis.GetSubscriber();
            var message = new CacheInvalidationMessage
            {
                CacheKey = cacheKey,
                Timestamp = DateTime.UtcNow,
                InstanceId = Environment.MachineName
            };

            var json = JsonSerializer.Serialize(message);
            var numSubscribers = await subscriber.PublishAsync(ChannelName, json);

            _logger.LogInformation(
                "Published cache invalidation for key '{CacheKey}' to {SubscriberCount} subscribers",
                cacheKey, numSubscribers);

            return numSubscribers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing cache invalidation for key '{CacheKey}'", cacheKey);
            return 0;
        }
    }

    private class CacheInvalidationMessage
    {
        public string CacheKey { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string InstanceId { get; set; } = string.Empty;
    }
}
