namespace ArticleService.Services;

public interface ICachePublisherService
{
    Task<long> PublishInvalidationAsync(string cacheKey);
}
