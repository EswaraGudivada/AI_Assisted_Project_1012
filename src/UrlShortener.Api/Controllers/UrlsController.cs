namespace UrlShortener.Api;

using Microsoft.AspNetCore.Mvc;
using UrlShortener.Application;

/// <summary>
/// API controller for URL shortening operations.
/// </summary>
[ApiController]
[Route("api/urls")]
public class UrlsController : ControllerBase
{
    private readonly IShortUrlService _service;
    private readonly ILogger<UrlsController> _logger;

    public UrlsController(IShortUrlService service, ILogger<UrlsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new short URL.
    /// </summary>
    /// <param name="request">The request containing the original URL</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>201 Created with the short URL details</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CreateShortUrlResponse>> CreateShortUrl(
        [FromBody] CreateShortUrlRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Creating short URL for URL length {UrlLength}", request.OriginalUrl?.Length ?? 0);
        var response = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAnalytics), new { code = response.ShortCode }, response);
    }

    /// <summary>
    /// Retrieves analytics for a short code.
    /// </summary>
    /// <param name="code">The short code</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>200 OK with analytics data</returns>
    [HttpGet("{code}/analytics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AnalyticsResponse>> GetAnalytics(string code, CancellationToken ct)
    {
        _logger.LogDebug("Getting analytics for short code: {ShortCode}", code);
        var analytics = await _service.GetAnalyticsAsync(code, ct);
        return Ok(analytics);
    }
}
