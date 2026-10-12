namespace UrlShortener.UnitTests.Domain;

using Xunit;
using UrlShortener.Domain;

public class UrlValidatorTests
{
    private readonly UrlValidator _validator = new();

    [Fact]
    public void Validate_WithValidHttpUrl_Succeeds()
    {
        // Act & Assert (no exception)
        _validator.Validate("http://example.com");
    }

    [Fact]
    public void Validate_WithValidHttpsUrl_Succeeds()
    {
        // Act & Assert (no exception)
        _validator.Validate("https://example.com/path?query=value");
    }

    [Fact]
    public void Validate_WithNullUrl_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _validator.Validate(null));
    }

    [Fact]
    public void Validate_WithEmptyUrl_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _validator.Validate(string.Empty));
    }

    [Fact]
    public void Validate_WithFtpUrl_Throws()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _validator.Validate("ftp://example.com"));
    }

    [Fact]
    public void Validate_WithMalformedUrl_Throws()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _validator.Validate("not a url"));
    }

    [Fact]
    public void Validate_WithUrlExceeding2048Chars_Throws()
    {
        // Arrange
        var longUrl = "https://example.com/" + new string('a', 2049);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _validator.Validate(longUrl));
    }
}
