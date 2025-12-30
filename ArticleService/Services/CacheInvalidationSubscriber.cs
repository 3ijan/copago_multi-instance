using System.Text.Json;
using StackExchange.Redis;

namespace ArticleService.Services;

public class CacheInvalidationSubscriber : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IInMemoryCacheService _memCache;
    private readonly ILogger<CacheInvalidationSubscriber> _logger;
    private const string ChannelName = "cache-invalidation";

    public CacheInvalidationSubscriber(
        IConnectionMultiplexer redis,
        IInMemoryCacheService memCache,
        ILogger<CacheInvalidationSubscriber> logger)
    {
        _redis = redis;
        _memCache = memCache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cache invalidation subscriber starting...");

        try
        {
            var subscriber = _redis.GetSubscriber();

            await subscriber.SubscribeAsync(ChannelName, (channel, message) =>
            {
                ProcessInvalidationMessage(message);
            });

            _logger.LogInformation("Cache invalidation subscriber started on channel '{Channel}'", ChannelName);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Cache invalidation subscriber stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in cache invalidation subscriber");
            throw;
        }
    }

    private void ProcessInvalidationMessage(RedisValue message)
    {
        try
        {
            if (message.IsNullOrEmpty)
                return;

            var invalidationMessage = JsonSerializer.Deserialize<CacheInvalidationMessage>(message.ToString());

            if (invalidationMessage == null)
                return;

            if (invalidationMessage.InstanceId == Environment.MachineName)
            {
                _logger.LogDebug("Skipping cache invalidation from same instance");
                return;
            }

            _memCache.Remove(invalidationMessage.CacheKey);

            _logger.LogInformation(
                "Cache invalidated for key '{CacheKey}' (from instance {InstanceId})",
                invalidationMessage.CacheKey, invalidationMessage.InstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing cache invalidation message");
        }
    }

    private class CacheInvalidationMessage
    {
        public string CacheKey { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string InstanceId { get; set; } = string.Empty;
    }
}
