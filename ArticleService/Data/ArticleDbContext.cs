using Microsoft.EntityFrameworkCore;
using ArticleService.Models;

namespace ArticleService.Data;

public class ArticleDbContext : DbContext
{
    public ArticleDbContext(DbContextOptions<ArticleDbContext> options) : base(options) { }

    public DbSet<Article> Articles => Set<Article>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Article>(entity =>
        {
            entity.ToTable("Articles");
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.CustomerId, e.ArticleNumber })
                .IsUnique()
                .HasDatabaseName("IX_Articles_CustomerId_ArticleNumber");

            entity.Property(e => e.ArticleNumber).IsRequired();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.CustomerId).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
        });
    }
}
