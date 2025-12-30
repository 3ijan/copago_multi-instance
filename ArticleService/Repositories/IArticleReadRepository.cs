using ArticleService.Models;

namespace ArticleService.Repositories;

public interface IArticleReadRepository : IArticleRepository
{
    Task<Article?> GetArticleAsync(int customerId, int articleNumber);
    Task<List<Article>> GetAllArticlesAsync(int customerId);
}
