namespace UrlShortener.Api;

using Microsoft.EntityFrameworkCore;
using UrlShortener.Infrastructure;

/// <summary>
/// Extension methods for mapping health and readiness endpoints.
/// </summary>
public static class HealthEndpoints
{
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () =>
        {
            return Results.Ok(new { status = "Healthy" });
        })
        .WithName("Health")
        .Produces(StatusCodes.Status200OK);

        app.MapGet("/ready", async (UrlShortenerDbContext dbContext) =>
        {
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
                return Results.Ok(new { status = "Ready" });
            }
            catch
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        })
        .WithName("Ready")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status503ServiceUnavailable);

        return app;
    }
}
