using ArticleService.Data;
using ArticleService.Extensions;
using ArticleService.Middleware;
using ArticleService.Repositories;
using ArticleService.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

//var builder = WebApplication.CreateBuilder(args);

//// Configuration
//var redisConnection = builder.Configuration["Redis:Connection"] ?? "localhost:6379";
//var dbConnection = builder.Configuration["Database:Connection"]
//    ?? "Host=localhost;Port=5432;Database=articles;User Id=postgres;Password=password;";

//// Services
////var redis = ConnectionMultiplexer.Connect(redisConnection);
//var redis = ConnectionMultiplexer.Connect("host.docker.internal:6379");

//builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
//    ConnectionMultiplexer.Connect("host.docker.internal:6379"));
////builder.Services.AddSingleton(redis);
//builder.Services.AddSingleton<ICacheService, RedisCacheService>();
//builder.Services.AddSingleton<IInMemoryCacheService, InMemoryCacheService>();

//builder.Services.AddDbContext<ArticleDbContext>(options =>
//    options.UseNpgsql(dbConnection));

//builder.Services.AddScoped<IArticleReadRepository>(sp =>
//    new DapperArticleReadRepository(dbConnection));
//builder.Services.AddScoped<IArticleWriteRepository, EFArticleWriteRepository>();

//var app = builder.Build();

//// Middleware
//app.UseMiddleware<JwtMiddleware>();

//// One line!
//app.RegisterEndpoints();

//// Database Migration
////using (var scope = app.Services.CreateScope())
////{
////    var context = scope.ServiceProvider.GetRequiredService<ArticleDbContext>();
////    //await context.Database.MigrateAsync();
////}

//app.Run();

var builder = WebApplication.CreateBuilder(args);

// Load environment-specific configuration
var cfg = builder.Configuration;
// Determine environment
var env = builder.Environment;
// Clear default sources and add base appsettings.json
if (env.IsDevelopment())
{
    cfg.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
}
else
{
    cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
}

// ==================== Configuration ====================
var redisConnection = builder.Configuration["Redis:Connection"] ?? "localhost:6379";
var dbConnection = builder.Configuration["Database:Connection"]
    ?? "Host=localhost;Port=5432;Database=articles;User Id=postgres;Password=password;";

Console.WriteLine(builder.Environment.EnvironmentName);
Console.WriteLine(dbConnection);

// ==================== Service Registration ====================
// Redis & Caching
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(redisConnection));
builder.Services.AddSingleton<ICacheService, RedisCacheService>();
builder.Services.AddSingleton<IInMemoryCacheService, InMemoryCacheService>();

// Cache Publisher (Pub/Sub)
builder.Services.AddSingleton<ICachePublisherService, RedisCachePublisherService>();

// Cache Invalidation Subscriber (Background Service)
builder.Services.AddHostedService<CacheInvalidationSubscriber>();

// Database
builder.Services.AddDbContext<ArticleDbContext>(options =>
    options.UseNpgsql(dbConnection));

// Repositories
builder.Services.AddScoped<IArticleReadRepository>(sp =>
    new DapperArticleReadRepository(dbConnection, sp.GetRequiredService<ILogger<DapperArticleReadRepository>>()));
//new DapperArticleReadRepository(dbConnection));
//builder.Services.AddScoped<IArticleWriteRepository, EFArticleWriteRepository>();
builder.Services.AddScoped<IArticleWriteRepository>(sp =>
    new EFArticleWriteRepository(sp.GetRequiredService<ArticleDbContext>(), sp.GetRequiredService<ILogger<EFArticleWriteRepository>>()));

var app = builder.Build();

// ==================== Middleware Configuration ==================
app.UseMiddleware<JwtMiddleware>();

// ==================== Register All Endpoints ====================
app.RegisterEndpoints();

// Auto Migration (only when needed)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ArticleDbContext>();
    context.Database.Migrate();
}

app.MapGet("/health", () => Results.Json(new { status = "healthy" }));

app.Run();
