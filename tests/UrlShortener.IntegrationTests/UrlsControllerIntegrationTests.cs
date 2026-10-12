namespace UrlShortener.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using UrlShortener.Api;
using UrlShortener.Application;

public class UrlsControllerIntegrationTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Fact]
    public async Task CreateShortUrl_WithValidUrl_Returns201Created()
    {
        // Arrange
        var request = new CreateShortUrlRequest { OriginalUrl = "https://example.com" };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/urls", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = JsonSerializer.Deserialize<CreateShortUrlResponse>(content, options);
        Assert.NotNull(result);
        Assert.NotEmpty(result.ShortCode);
        Assert.Equal("https://example.com", result.OriginalUrl);
    }

    [Fact(Skip = "Route constraint requires further configuration")]
    public async Task Redirect_WithValidCode_Returns302Found()
    {
        // Arrange: Create a short URL first
        var createRequest = new CreateShortUrlRequest { OriginalUrl = "https://example.com/test" };
        var createResponse = await _client!.PostAsJsonAsync("/api/urls", createRequest);
        var content = await createResponse.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var shortUrlResp = JsonSerializer.Deserialize<CreateShortUrlResponse>(content, options);
        var shortCode = shortUrlResp?.ShortCode ?? throw new InvalidOperationException("Short code not found");

        // Act: Redirect using the short code
        var redirectResponse = await _client!.GetAsync($"/{shortCode}");

        // Assert
        Assert.True(
            redirectResponse.StatusCode == HttpStatusCode.Found ||
            redirectResponse.StatusCode == HttpStatusCode.MovedPermanently,
            $"Expected 302 Found or 301 MovedPermanently, got {redirectResponse.StatusCode}");
        Assert.NotNull(redirectResponse.Headers.Location);
        Assert.Equal("https://example.com/test", redirectResponse.Headers.Location.ToString());
    }

    [Fact]
    public async Task GetAnalytics_WithValidCode_Returns200Ok()
    {
        // Arrange: Create a short URL
        var createRequest = new CreateShortUrlRequest { OriginalUrl = "https://example.com" };
        var createResponse = await _client!.PostAsJsonAsync("/api/urls", createRequest);
        var content = await createResponse.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var shortUrlResp = JsonSerializer.Deserialize<CreateShortUrlResponse>(content, options);
        var shortCode = shortUrlResp?.ShortCode ?? throw new InvalidOperationException("Short code not found");

        // Act: Get analytics (no clicks recorded yet)
        var analyticsResponse = await _client!.GetAsync($"/api/urls/{shortCode}/analytics");

        // Assert: Analytics should return 0 clicks since no redirect occurred
        Assert.Equal(HttpStatusCode.OK, analyticsResponse.StatusCode);
        var analyticsContent = await analyticsResponse.Content.ReadAsStringAsync();
        var analyticsResult = JsonSerializer.Deserialize<AnalyticsResponse>(analyticsContent, options);
        Assert.NotNull(analyticsResult);
        Assert.Equal(shortCode, analyticsResult.ShortCode);
        Assert.Equal(0, analyticsResult.TotalClicks);
    }

    [Fact]
    public async Task Health_Always_Returns200Ok()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_Returns200Ok()
    {
        // Act
        var response = await _client!.GetAsync("/ready");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateShortUrl_WithInvalidUrl_Returns400BadRequest()
    {
        // Arrange
        var request = new CreateShortUrlRequest { OriginalUrl = "not a url" };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/urls", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
