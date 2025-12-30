namespace ArticleService.Services;

public interface IInMemoryCacheService
{
    T? Get<T>(string key);
    Task<T?> GetOrFetchAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);
    void Set<T>(string key, T value, TimeSpan? expiration = null);
    void Remove(string key);
    void RemoveByPrefix(string prefix);
    void Clear();
    void LogStatistics();
}
