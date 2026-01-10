using cdc_cli.Configuration;
using FluentAssertions;

namespace cdc_cli.Tests.Configuration;

/// <summary>
/// Tests for CLI configuration management
/// </summary>
public class CliConfigurationTests
{
    /// <summary>
    /// Tests that default configuration values are set correctly
    /// </summary>
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var config = new CliConfiguration();

        // Assert
        config.BaseUrl.Should().Be("http://localhost:5000");
        config.OutputFormat.Should().Be(OutputFormat.Json);
        config.Verbose.Should().BeFalse();
        config.Quiet.Should().BeFalse();
    }

    /// <summary>
    /// Tests configuration validation with valid HTTP URL
    /// </summary>
    [Fact]
    public void Validate_WithValidHttpUrl_DoesNotThrow()
    {
        // Arrange
        var config = new CliConfiguration { BaseUrl = "http://localhost:5000" };

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().NotThrow();
    }

    /// <summary>
    /// Tests configuration validation with valid HTTPS URL
    /// </summary>
    [Fact]
    public void Validate_WithValidHttpsUrl_DoesNotThrow()
    {
        // Arrange
        var config = new CliConfiguration { BaseUrl = "https://api.example.com" };

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().NotThrow();
    }

    /// <summary>
    /// Tests configuration validation rejects empty URL
    /// </summary>
    [Fact]
    public void Validate_WithEmptyUrl_ThrowsException()
    {
        // Arrange
        var config = new CliConfiguration { BaseUrl = "" };

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot be empty*");
    }

    /// <summary>
    /// Tests configuration validation rejects invalid URL
    /// </summary>
    [Fact]
    public void Validate_WithInvalidUrl_ThrowsException()
    {
        // Arrange
        var config = new CliConfiguration { BaseUrl = "not-a-valid-url" };

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a valid URI*");
    }

    /// <summary>
    /// Tests configuration validation rejects FTP scheme
    /// </summary>
    [Fact]
    public void Validate_WithFtpScheme_ThrowsException()
    {
        // Arrange
        var config = new CliConfiguration { BaseUrl = "ftp://example.com" };

        // Act
        var act = () => config.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must use http or https*");
    }

    /// <summary>
    /// Tests loading configuration from environment with CDC_API_URL set
    /// </summary>
    [Fact]
    public void LoadFromEnvironment_WithCdcApiUrl_UsesEnvironmentValue()
    {
        // Arrange
        var testUrl = "http://test-api.example.com";
        Environment.SetEnvironmentVariable("CDC_API_URL", testUrl);

        try
        {
            // Act
            var config = CliConfiguration.LoadFromEnvironment();

            // Assert
            config.BaseUrl.Should().Be(testUrl);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("CDC_API_URL", null);
        }
    }

    /// <summary>
    /// Tests loading configuration from environment without CDC_API_URL
    /// </summary>
    [Fact]
    public void LoadFromEnvironment_WithoutCdcApiUrl_UsesDefaultValue()
    {
        // Arrange
        Environment.SetEnvironmentVariable("CDC_API_URL", null);

        // Act
        var config = CliConfiguration.LoadFromEnvironment();

        // Assert
        config.BaseUrl.Should().Be("http://localhost:5000");
    }
}
