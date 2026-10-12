namespace UrlShortener.UnitTests.Domain;

using Xunit;
using UrlShortener.Domain;

public class ShortCodeGeneratorTests
{
    [Fact]
    public void Generate_ReturnsNonEmptyString()
    {
        // Arrange
        var generator = new ShortCodeGenerator();

        // Act
        var code = generator.Generate();

        // Assert
        Assert.NotEmpty(code);
    }

    [Fact]
    public void Generate_ReturnsExactly6Characters()
    {
        // Arrange
        var generator = new ShortCodeGenerator();

        // Act
        var code = generator.Generate();

        // Assert
        Assert.Equal(6, code.Length);
    }

    [Fact]
    public void Generate_ContainsOnlyAlphanumeric()
    {
        // Arrange
        var generator = new ShortCodeGenerator();

        // Act
        var code = generator.Generate();

        // Assert
        Assert.Matches(@"^[a-zA-Z0-9]+$", code);
    }

    [Fact]
    public void Generate_ProducesDifferentCodes()
    {
        // Arrange
        var generator = new ShortCodeGenerator();

        // Act
        var code1 = generator.Generate();
        var code2 = generator.Generate();

        // Assert
        Assert.NotEqual(code1, code2);
    }
}
