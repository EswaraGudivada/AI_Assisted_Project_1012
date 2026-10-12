namespace UrlShortener.Domain;

/// <summary>
/// Value Object representing a click event on a short URL.
/// </summary>
public class ClickEvent
{
    public Guid Id { get; set; }

    public Guid ShortenedUrlId { get; set; }

    public DateTime ClickedAtUtc { get; set; }
}
