namespace UrlShortener.Domain;

/// <summary>
/// Entity representing a shortened URL with metadata.
/// </summary>
public class ShortenedUrl
{
    public Guid Id { get; set; }

    public string ShortCode { get; set; } = string.Empty;

    public string OriginalUrl { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
