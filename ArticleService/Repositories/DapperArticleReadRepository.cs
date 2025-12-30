using ArticleService.Models;
using Dapper;
using Npgsql;
using System.Data;

namespace ArticleService.Repositories;

public class DapperArticleReadRepository : IArticleReadRepository
{
    private readonly string _connectionString;
    private readonly ILogger<DapperArticleReadRepository> _logger;

    public DapperArticleReadRepository(
        string connectionString,
        ILogger<DapperArticleReadRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<Article?> GetArticleAsync(int customerId, int articleNumber)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);

        try
        {
            var sql = @"
            SELECT ""Id"", ""ArticleNumber"", ""Name"", ""Price"", ""CustomerId"",
                   ""CreatedAt"", ""UpdatedAt""
            FROM public.""Articles""
            WHERE ""CustomerId"" = @CustomerId AND ""ArticleNumber"" = @ArticleNumber
            LIMIT 1";

            var article = await connection.QueryFirstOrDefaultAsync<Article>(
                sql,
                new { CustomerId = customerId, ArticleNumber = articleNumber },
                transaction);

            await transaction.CommitAsync();

            if (article != null)
            {
                _logger.LogDebug(
                    "Article retrieved: CustomerId={CustomerId}, ArticleNumber={ArticleNumber}",
                    customerId, articleNumber);
            }

            return article;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(
                ex,
                "Error retrieving article: CustomerId={CustomerId}, ArticleNumber={ArticleNumber}",
                customerId, articleNumber);
            throw;
        }
    }

    public async Task<List<Article>> GetAllArticlesAsync(int customerId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);

        try
        {
            var sql = @"
            SELECT ""Id"", ""ArticleNumber"", ""Name"", ""Price"", ""CustomerId"",
                   ""CreatedAt"", ""UpdatedAt""
            FROM public.""Articles""
            WHERE ""CustomerId"" = @CustomerId
            ORDER BY ""ArticleNumber"" ASC";

            var articles = await connection.QueryAsync<Article>(
                sql,
                new { CustomerId = customerId },
                transaction);

            await transaction.CommitAsync();

            var result = articles.ToList();

            _logger.LogDebug(
                "Retrieved {Count} articles for CustomerId={CustomerId}",
                result.Count, customerId);

            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(
                ex,
                "Error retrieving articles for CustomerId={CustomerId}",
                customerId);
            throw;
        }
    }
}