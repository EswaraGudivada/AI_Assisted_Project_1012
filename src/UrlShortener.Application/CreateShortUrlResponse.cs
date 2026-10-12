namespace UrlShortener.Application;

/// <summary>
/// Response DTO for creating a short URL.
/// </summary>
public record CreateShortUrlResponse
{
    public string ShortCode { get; init; } = string.Empty;

    public string OriginalUrl { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    public string ShortUrl { get; init; } = string.Empty;
}
