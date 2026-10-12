namespace UrlShortener.UnitTests.Application;

using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using UrlShortener.Application;
using UrlShortener.Domain;

public class ShortUrlServiceTests
{
    private readonly Mock<IUrlRepository> _mockRepository;
    private readonly Mock<ILogger<ShortUrlService>> _mockLogger;
    private readonly UrlValidator _urlValidator;
    private readonly ShortCodeGenerator _codeGenerator;
    private readonly ShortUrlService _service;

    public ShortUrlServiceTests()
    {
        _mockRepository = new Mock<IUrlRepository>();
        _mockLogger = new Mock<ILogger<ShortUrlService>>();
        _urlValidator = new UrlValidator();
        _codeGenerator = new ShortCodeGenerator();
        _service = new ShortUrlService(_mockRepository.Object, _urlValidator, _codeGenerator, _mockLogger.Object);
    }

    [Fact]
    public async Task CreateAsync_WithValidUrl_ReturnsCreatedResponse()
    {
        // Arrange
        var request = new CreateShortUrlRequest { OriginalUrl = "https://example.com" };
        _mockRepository.Setup(r => r.CreateAsync(It.IsAny<ShortenedUrl>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortenedUrl url, CancellationToken ct) => url);

        // Act
        var response = await _service.CreateAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.ShortCode);
        Assert.Equal("https://example.com", response.OriginalUrl);
        Assert.NotEqual(default, response.CreatedAtUtc);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidUrl_ThrowsUrlValidationException()
    {
        // Arrange
        var request = new CreateShortUrlRequest { OriginalUrl = "not a url" };

        // Act & Assert
        await Assert.ThrowsAsync<UrlValidationException>(() => _service.CreateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAndRecordClickAsync_WithValidCode_ReturnsUrl()
    {
        // Arrange
        var url = new ShortenedUrl
        {
            Id = Guid.NewGuid(),
            ShortCode = "abc123",
            OriginalUrl = "https://example.com",
            CreatedAtUtc = DateTime.UtcNow
        };
        _mockRepository.Setup(r => r.GetByShortCodeAsync("abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(url);
        _mockRepository.Setup(r => r.RecordClickAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ResolveAndRecordClickAsync("abc123", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("abc123", result.ShortCode);
        Assert.Equal("https://example.com", result.OriginalUrl);
    }

    [Fact]
    public async Task ResolveAndRecordClickAsync_WithUnknownCode_ThrowsNotFoundException()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetByShortCodeAsync("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortenedUrl?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _service.ResolveAndRecordClickAsync("unknown", CancellationToken.None));
    }

    [Fact]
    public async Task GetAnalyticsAsync_WithValidCode_ReturnsAggregates()
    {
        // Arrange
        var url = new ShortenedUrl
        {
            Id = Guid.NewGuid(),
            ShortCode = "abc123",
            OriginalUrl = "https://example.com",
            CreatedAtUtc = DateTime.UtcNow
        };
        _mockRepository.Setup(r => r.GetByShortCodeAsync("abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(url);
        _mockRepository.Setup(r => r.GetClickCountAsync(url.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
        _mockRepository.Setup(r => r.GetLastClickAsync(url.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTime.UtcNow);

        // Act
        var result = await _service.GetAnalyticsAsync("abc123", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("abc123", result.ShortCode);
        Assert.Equal(42, result.TotalClicks);
        Assert.NotNull(result.LastClickAtUtc);
    }
}
