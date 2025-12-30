namespace ArticleService.Models;

public class Article
{
    public int Id { get; set; }
    public int ArticleNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int CustomerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
