using ArticleService.Models;

namespace ArticleService.Repositories;

public interface IArticleWriteRepository : IArticleRepository
{
    Task UpsertArticleAsync(Article article);
    Task<bool> DeleteArticleAsync(int customerId, int articleNumber);
}
