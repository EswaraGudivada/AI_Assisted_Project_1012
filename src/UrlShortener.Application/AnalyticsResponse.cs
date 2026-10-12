namespace UrlShortener.Application;

/// <summary>
/// Response DTO for analytics.
/// </summary>
public record AnalyticsResponse
{
    public string ShortCode { get; init; } = string.Empty;

    public string OriginalUrl { get; init; } = string.Empty;

    public int TotalClicks { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? LastClickAtUtc { get; init; }
}
