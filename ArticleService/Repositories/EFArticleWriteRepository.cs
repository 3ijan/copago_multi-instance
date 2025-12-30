using ArticleService.Data;
using ArticleService.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;

namespace ArticleService.Repositories;

public class EFArticleWriteRepository : IArticleWriteRepository
{
    private readonly ArticleDbContext _context;
    private readonly ILogger<EFArticleWriteRepository> _logger;

    private const int MaxRetries = 3;
    private const int InitialDelayMs = 100;

    public EFArticleWriteRepository(
        ArticleDbContext context,
        ILogger<EFArticleWriteRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task UpsertArticleAsync(Article article)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            if (article.ArticleNumber <= 0)
                throw new ArgumentException("ArticleNumber must be positive");

            if (string.IsNullOrWhiteSpace(article.Name))
                throw new ArgumentException("Name cannot be empty");
            
            if (article.Price < 0)
                throw new ArgumentException("Price cannot be less than zero");

            using var transaction = await _context.Database
                .BeginTransactionAsync(IsolationLevel.RepeatableRead);

            try
            {
                var existing = await _context.Articles
                    .FirstOrDefaultAsync(a => a.CustomerId == article.CustomerId &&
                                              a.ArticleNumber == article.ArticleNumber);

                if (existing != null)
                {
                    if (!string.IsNullOrEmpty(article.Name) && existing.Name != article.Name) existing.Name = article.Name;
                    if (article.Price >= 0 && existing.Price != article.Price) existing.Price = article.Price;

                    existing.UpdatedAt = DateTime.UtcNow;

                    _context.Articles.Update(existing);

                    article.Id = existing.Id;
                }
                else
                {
                    article.CreatedAt = DateTime.UtcNow;
                    article.UpdatedAt = DateTime.UtcNow;

                    _context.Articles.Add(article);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Article upserted: CustomerId={CustomerId}, ArticleNumber={ArticleNumber}, Name={Name}",
                    article.CustomerId, article.ArticleNumber, article.Name);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public async Task<bool> DeleteArticleAsync(int customerId, int articleNumber)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            using var transaction = await _context.Database
                .BeginTransactionAsync(IsolationLevel.RepeatableRead);

            try
            {
                var article = await _context.Articles
                    .FirstOrDefaultAsync(a =>
                        a.CustomerId == customerId &&
                        a.ArticleNumber == articleNumber);

                if (article == null)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                _context.Articles.Remove(article);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Article deleted: CustomerId={CustomerId}, ArticleNumber={ArticleNumber}",
                    customerId, articleNumber);

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }
    
    private async Task ExecuteWithRetryAsync(Func<Task> operation)
    {
        int retryCount = 0;
        int delayMs = InitialDelayMs;

        while (retryCount < MaxRetries)
        {
            try
            {
                await operation();
                return;
            }
            catch (PostgresException ex) when (ex.SqlState == "40P01")
            {
                retryCount++;

                if (retryCount >= MaxRetries)
                {
                    _logger.LogError(
                        ex,
                        "Deadlock detected after {RetryCount} retries. Giving up.",
                        retryCount);
                    throw;
                }

                _logger.LogWarning(
                    "Deadlock detected. Retrying in {DelayMs}ms (Attempt {RetryCount}/{MaxRetries})",
                    delayMs, retryCount, MaxRetries);

                await Task.Delay(delayMs);
                delayMs *= 2;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                retryCount++;

                if (retryCount >= MaxRetries)
                {
                    _logger.LogError(ex, "Concurrency error after {RetryCount} retries", retryCount);
                    throw;
                }

                _logger.LogWarning(
                    "Concurrency conflict. Retrying in {DelayMs}ms (Attempt {RetryCount}/{MaxRetries})",
                    delayMs, retryCount, MaxRetries);

                _context.ChangeTracker.Clear();

                await Task.Delay(delayMs);
                delayMs *= 2;
            }
        }
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
    {
        int retryCount = 0;
        int delayMs = InitialDelayMs;

        while (retryCount < MaxRetries)
        {
            try
            {
                return await operation();
            }
            catch (PostgresException ex) when (ex.SqlState == "40P01")
            {
                retryCount++;

                if (retryCount >= MaxRetries)
                {
                    _logger.LogError(
                        ex,
                        "Deadlock detected after {RetryCount} retries. Giving up.",
                        retryCount);
                    throw;
                }

                _logger.LogWarning(
                    "Deadlock detected. Retrying in {DelayMs}ms (Attempt {RetryCount}/{MaxRetries})",
                    delayMs, retryCount, MaxRetries);

                await Task.Delay(delayMs);
                delayMs *= 2;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                retryCount++;

                if (retryCount >= MaxRetries)
                {
                    _logger.LogError(ex, "Concurrency error after {RetryCount} retries", retryCount);
                    throw;
                }

                _logger.LogWarning(
                    "Concurrency conflict. Retrying in {DelayMs}ms (Attempt {RetryCount}/{MaxRetries})",
                    delayMs, retryCount, MaxRetries);

                _context.ChangeTracker.Clear();

                await Task.Delay(delayMs);
                delayMs *= 2;
            }
        }

        throw new InvalidOperationException("Retry loop completed without return");
    }
}
