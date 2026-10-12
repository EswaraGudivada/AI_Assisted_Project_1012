namespace UrlShortener.Application;

using Microsoft.Extensions.Logging;
using UrlShortener.Domain;

/// <summary>
/// Application Service that orchestrates URL shortening use cases.
/// </summary>
public interface IShortUrlService
{
    /// <summary>
    /// Creates a new short URL and returns the response.
    /// Throws UrlValidationException if URL is invalid.
    /// Throws CollisionExhaustionException if unable to generate a unique code.
    /// </summary>
    Task<CreateShortUrlResponse> CreateAsync(CreateShortUrlRequest request, CancellationToken ct);

    /// <summary>
    /// Resolves a short code and records a click event.
    /// Returns the ShortenedUrl if found, throws NotFoundException otherwise.
    /// If click recording fails, the URL is still returned (availability over consistency).
    /// </summary>
    Task<ShortenedUrl> ResolveAndRecordClickAsync(string shortCode, CancellationToken ct);

    /// <summary>
    /// Retrieves analytics (aggregate click statistics) for a short code.
    /// Throws NotFoundException if code does not exist.
    /// </summary>
    Task<AnalyticsResponse> GetAnalyticsAsync(string shortCode, CancellationToken ct);
}

/// <summary>
/// Implementation of IShortUrlService.
/// </summary>
public class ShortUrlService : IShortUrlService
{
    private const int MaxCollisionRetries = 5;
    private readonly IUrlRepository _repository;
    private readonly UrlValidator _urlValidator;
    private readonly ShortCodeGenerator _codeGenerator;
    private readonly ILogger<ShortUrlService> _logger;

    public ShortUrlService(
        IUrlRepository repository,
        UrlValidator urlValidator,
        ShortCodeGenerator codeGenerator,
        ILogger<ShortUrlService> logger)
    {
        _repository = repository;
        _urlValidator = urlValidator;
        _codeGenerator = codeGenerator;
        _logger = logger;
    }

    public async Task<CreateShortUrlResponse> CreateAsync(CreateShortUrlRequest request, CancellationToken ct)
    {
        // Validate URL
        try
        {
            _urlValidator.Validate(request.OriginalUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "URL validation failed for length {UrlLength}", request.OriginalUrl.Length);
            throw new UrlValidationException("Invalid URL", ex);
        }

        // Try to create with retry on collision
        for (int attempt = 1; attempt <= MaxCollisionRetries; attempt++)
        {
            var shortCode = _codeGenerator.Generate();

            try
            {
                var shortenedUrl = new ShortenedUrl
                {
                    Id = Guid.NewGuid(),
                    ShortCode = shortCode,
                    OriginalUrl = request.OriginalUrl,
                    CreatedAtUtc = DateTime.UtcNow
                };

                var created = await _repository.CreateAsync(shortenedUrl, ct);

                _logger.LogInformation("Short URL created: {ShortCode}", shortCode);

                return new CreateShortUrlResponse
                {
                    ShortCode = created.ShortCode,
                    OriginalUrl = created.OriginalUrl,
                    CreatedAtUtc = created.CreatedAtUtc,
                    ShortUrl = $"http://localhost:5000/{created.ShortCode}"
                };
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("unique", StringComparison.OrdinalIgnoreCase))
            {
                // Unique constraint violation (collision) — retry
                _logger.LogDebug("Short code collision on attempt {Attempt}/{MaxAttempts}: {ShortCode}", attempt, MaxCollisionRetries, shortCode);
                if (attempt == MaxCollisionRetries)
                {
                    _logger.LogError("Short code collision retry exhausted after {AttemptCount} attempts", MaxCollisionRetries);
                    throw new CollisionExhaustionException($"Unable to generate unique short code after {MaxCollisionRetries} attempts");
                }
            }
        }

        // Should not reach here
        throw new CollisionExhaustionException("Collision exhaustion");
    }

    public async Task<ShortenedUrl> ResolveAndRecordClickAsync(string shortCode, CancellationToken ct)
    {
        var url = await _repository.GetByShortCodeAsync(shortCode, ct);
        if (url is null)
        {
            _logger.LogWarning("Short code not found: {ShortCode}", shortCode);
            throw new NotFoundException($"Short code '{shortCode}' not found");
        }

        // Record click (fire-and-forget with error logging)
        try
        {
            await _repository.RecordClickAsync(url.Id, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record click for short code {ShortCode}", shortCode);
            // Don't rethrow: availability > consistency. Redirect still succeeds.
        }

        return url;
    }

    public async Task<AnalyticsResponse> GetAnalyticsAsync(string shortCode, CancellationToken ct)
    {
        var url = await _repository.GetByShortCodeAsync(shortCode, ct);
        if (url is null)
        {
            _logger.LogWarning("Short code not found for analytics: {ShortCode}", shortCode);
            throw new NotFoundException($"Short code '{shortCode}' not found");
        }

        var totalClicks = await _repository.GetClickCountAsync(url.Id, ct);
        var lastClick = await _repository.GetLastClickAsync(url.Id, ct);

        return new AnalyticsResponse
        {
            ShortCode = url.ShortCode,
            OriginalUrl = url.OriginalUrl,
            TotalClicks = totalClicks,
            CreatedAtUtc = url.CreatedAtUtc,
            LastClickAtUtc = lastClick
        };
    }
}
