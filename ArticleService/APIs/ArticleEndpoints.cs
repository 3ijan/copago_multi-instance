using ArticleService.DTOs;
using ArticleService.Helpers;
using ArticleService.Models;
using ArticleService.Repositories;
using ArticleService.Services;

namespace ArticleService.APIs;

/// <summary>
/// Article-related API endpoints
/// Implements IEndpointExtension for automatic registration
/// </summary>
public class ArticleEndpoints : IEndpointExtension
{
    /// <summary>
    /// Maps all article endpoints
    /// Called automatically by reflection in Program.cs
    /// </summary>
    public void MapEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/articles")
            .WithTags("Articles")
            .WithDescription("Article management endpoints");

        // GET single article
        group.MapGet("/{articleNumber}", GetArticle)
            .WithName("GetArticle")
            .Produces<Article>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithDescription("Retrieve a single article by its number");

        // GET all articles
        group.MapGet("", GetAllArticles)
            .WithName("GetAllArticles")
            .Produces<List<Article>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithDescription("Retrieve all articles for the authenticated customer");

        //// POST create
        //group.MapPost("", UpsertArticle)
        //    .WithName("CreateArticle")
        //    .Produces<Article>(StatusCodes.Status200OK)
        //    .Produces(StatusCodes.Status400BadRequest)
        //    .Produces(StatusCodes.Status401Unauthorized)
        //    .WithDescription("Create a new article or update an existing one");

        //// PUT update
        //group.MapPut("/{articleNumber}", UpsertArticle)
        //    .WithName("UpdateArticle")
        //    .Produces<Article>(StatusCodes.Status200OK)
        //    .Produces(StatusCodes.Status400BadRequest)
        //    .Produces(StatusCodes.Status401Unauthorized)
        //    .WithDescription("Update an existing one");

        // POST / PUT create or update(upsert)
        app.MapMethods("/articles", new[] { "POST", "PUT" }, UpsertArticle)
            .WithName("UpsertArticle")
            .Produces<Article>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithDescription("Create a new article or update an existing one");

        // DELETE article
        group.MapDelete("/{articleNumber}", DeleteArticle)
            .WithName("DeleteArticle")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithDescription("Delete an article by its number");
    }

    // ==================== Route Handlers ====================

    /// <summary>
    /// Handler for GET /articles/{articleNumber}
    /// Retrieves single article with two-layer caching
    /// </summary>
    private static async Task<IResult> GetArticle(
        int articleNumber,
        HttpContext context,
        IArticleReadRepository readRepo,
        IInMemoryCacheService memCache,
        ILogger<ArticleEndpoints> logger)
    {
        try
        {
            var customerId = AuthHelper.GetCustomerId(context);
            var cacheKey = $"{customerId}:{articleNumber}";

            var article = await memCache.GetOrFetchAsync(
                cacheKey,
                async () => await readRepo.GetArticleAsync(customerId, articleNumber),
                TimeSpan.FromMinutes(10));

            if (article == null)
                return Results.NotFound(new { error = "Article not found" });

            return Results.Ok(article);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving article");
            return Results.Problem("An error occurred while retrieving the article",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Handler for GET /articles
    /// Retrieves all articles for customer with caching
    /// </summary>
    private static async Task<IResult> GetAllArticles(
        HttpContext context,
        IArticleReadRepository readRepo,
        IInMemoryCacheService memCache,
        ILogger<ArticleEndpoints> logger)
    {
        try
        {
            var customerId = AuthHelper.GetCustomerId(context);
            var cacheKey = $"{customerId}:all";

            var articles = await memCache.GetOrFetchAsync(
                cacheKey,
                async () => await readRepo.GetAllArticlesAsync(customerId),
                TimeSpan.FromMinutes(10));

            return Results.Ok(articles ?? new List<Article>());
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving articles");
            return Results.Problem("An error occurred while retrieving articles",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Handler for POST /articles
    /// Creates new article or updates existing (upsert pattern)
    /// Invalidates both cache layers
    /// </summary>
    private static async Task<IResult> UpsertArticle(
        ArticleRequest request,
        HttpContext context,
        IArticleWriteRepository writeRepo,
        ICacheService cacheService,
        IInMemoryCacheService memCache,
        ICachePublisherService cachePublisher,
        ILogger<ArticleEndpoints> logger)
    {
        try
        {
            var customerId = AuthHelper.GetCustomerId(context);

            // Validation
            if (request.ArticleNumber <= 0)
                return Results.BadRequest(new { error = "ArticleNumber must be greater than 0" });

            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { error = "Name cannot be empty" });

            var article = new Article
            {
                ArticleNumber = request.ArticleNumber,
                Name = request.Name.Trim(),
                CustomerId = customerId,
                Price = request.Price ?? 0m
            };

            // Write to database
            await writeRepo.UpsertArticleAsync(article);

            // Invalidate both cache layers
            var allCacheKey = $"{customerId}:all";
            var singleCacheKey = $"{customerId}:{request.ArticleNumber}";

            // Redis (cross-instance sync)
            await cacheService.InvalidateAsync(allCacheKey);
            await cacheService.InvalidateAsync(singleCacheKey);

            // In-memory (local cache)
            memCache.Remove(allCacheKey);
            memCache.Remove(singleCacheKey);

            // Publish invalidation messages for other instances
            await cachePublisher.PublishInvalidationAsync(allCacheKey);
            await cachePublisher.PublishInvalidationAsync(singleCacheKey);

            logger.LogInformation(
                "Article upserted: CustomerId={CustomerId}, ArticleNumber={ArticleNumber}",
                customerId, article.ArticleNumber);

            return Results.Ok(article);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error upserting article");
            return Results.Problem("An error occurred while saving the article",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Handler for DELETE /articles/{articleNumber}
    /// Deletes article and invalidates caches
    /// </summary>
    private static async Task<IResult> DeleteArticle(
        int articleNumber,
        HttpContext context,
        IArticleWriteRepository writeRepo,
        ICacheService cacheService,
        IInMemoryCacheService memCache,
        ICachePublisherService cachePublisher,
        ILogger<ArticleEndpoints> logger)
    {
        try
        {
            var customerId = AuthHelper.GetCustomerId(context);

            // Delete from database
            var success = await writeRepo.DeleteArticleAsync(customerId, articleNumber);
            if (!success)
                return Results.NotFound(new { error = "Article not found" });

            // Invalidate both cache layers
            var allCacheKey = $"{customerId}:all";
            var singleCacheKey = $"{customerId}:{articleNumber}";

            // Redis (cross-instance sync)
            await cacheService.InvalidateAsync(allCacheKey);
            await cacheService.InvalidateAsync(singleCacheKey);

            // In-memory (local cache)
            memCache.Remove(allCacheKey);
            memCache.Remove(singleCacheKey);

            // Publish invalidation messages for other instances
            await cachePublisher.PublishInvalidationAsync(allCacheKey);
            await cachePublisher.PublishInvalidationAsync(singleCacheKey);

            logger.LogInformation(
                "Article deleted: CustomerId={CustomerId}, ArticleNumber={ArticleNumber}",
                customerId, articleNumber);

            return Results.NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting article");
            return Results.Problem("An error occurred while deleting the article",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
