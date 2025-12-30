namespace ArticleService.APIs;

/// <summary>
/// Base interface for all endpoint groups
/// Enables automatic discovery and registration of endpoints
/// </summary>
public interface IEndpointExtension
{
    /// <summary>
    /// Maps all endpoints for this group to the web application
    /// </summary>
    /// <param name="app">WebApplication instance</param>
    void MapEndpoints(WebApplication app);
}
