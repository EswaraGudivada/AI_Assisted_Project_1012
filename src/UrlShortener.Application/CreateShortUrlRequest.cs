namespace UrlShortener.Application;

/// <summary>
/// Request DTO for creating a short URL.
/// </summary>
public record CreateShortUrlRequest
{
    public string OriginalUrl { get; init; } = string.Empty;
}
