#nullable enable

using Microsoft.EntityFrameworkCore;
using UrlShortener.Api;
using UrlShortener.Application;
using UrlShortener.Domain;
using UrlShortener.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddScoped<IShortUrlService, ShortUrlService>();
builder.Services.AddScoped<IUrlRepository, UrlRepository>();
builder.Services.AddSingleton<UrlValidator>();
builder.Services.AddSingleton<ShortCodeGenerator>();
builder.Services.AddLogging(cfg => cfg.AddConsole());

// Add DbContext
var databasePath = builder.Configuration["Database:Path"] ?? "url_shortener.db";
var connectionString = $"Data Source={databasePath}";
builder.Services.AddDbContext<UrlShortenerDbContext>(options =>
    options.UseSqlite(connectionString));

// Add API features
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

// Add exception handling middleware
var app = builder.Build();

// Use middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Custom exception handling middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Map controllers and health endpoints
app.MapControllers();
app.MapHealthEndpoints();

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<UrlShortenerDbContext>();
    await dbContext.Database.MigrateAsync();
}

await app.RunAsync();

