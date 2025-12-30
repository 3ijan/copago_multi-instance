namespace ArticleService.DTOs;

public class ArticleRequest
{
    public int ArticleNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? Price { get; set; }
}
