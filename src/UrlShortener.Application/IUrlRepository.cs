namespace UrlShortener.Application;

using UrlShortener.Domain;

/// <summary>
/// Abstraction for URL persistence.
/// </summary>
public interface IUrlRepository
{
    /// <summary>
    /// Creates a new shortened URL. Throws if short code is not unique.
    /// </summary>
    Task<ShortenedUrl> CreateAsync(ShortenedUrl url, CancellationToken ct);

    /// <summary>
    /// Retrieves a short URL by its code. Returns null if not found.
    /// </summary>
    Task<ShortenedUrl?> GetByShortCodeAsync(string shortCode, CancellationToken ct);

    /// <summary>
    /// Records a click event for a short URL.
    /// </summary>
    Task RecordClickAsync(Guid shortenedUrlId, CancellationToken ct);

    /// <summary>
    /// Gets the total click count for a short URL.
    /// </summary>
    Task<int> GetClickCountAsync(Guid shortenedUrlId, CancellationToken ct);

    /// <summary>
    /// Gets the timestamp of the most recent click, or null if no clicks exist.
    /// </summary>
    Task<DateTime?> GetLastClickAsync(Guid shortenedUrlId, CancellationToken ct);
}
