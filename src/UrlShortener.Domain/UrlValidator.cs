namespace UrlShortener.Domain;

/// <summary>
/// Domain Service for validating URLs.
/// </summary>
public class UrlValidator
{
    private const int MaxUrlLength = 2048;

    /// <summary>
    /// Validates that a URL is well-formed and uses HTTP/HTTPS scheme.
    /// Throws an exception if validation fails.
    /// </summary>
    public void Validate(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL cannot be null or empty", nameof(url));
        }

        if (url.Length > MaxUrlLength)
        {
            throw new ArgumentException($"URL cannot exceed {MaxUrlLength} characters", nameof(url));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("URL is not a valid absolute URI");
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Only HTTP and HTTPS schemes are supported");
        }
    }
}
