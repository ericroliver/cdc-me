namespace cdc_cli.Configuration;

/// <summary>
/// Output format options for CLI commands
/// </summary>
public enum OutputFormat
{
    /// <summary>
    /// Compact JSON output
    /// </summary>
    Json,

    /// <summary>
    /// Pretty-printed JSON with indentation
    /// </summary>
    JsonPretty,

    /// <summary>
    /// Human-readable text output
    /// </summary>
    Text
}

/// <summary>
/// Configuration settings for the CDC CLI tool
/// </summary>
public class CliConfiguration
{
    /// <summary>
    /// Base URL for the CDC API
    /// Priority: CLI parameter > environment variable CDC_API_URL > default
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:5000";

    /// <summary>
    /// Output format for command results
    /// </summary>
    public OutputFormat OutputFormat { get; set; } = OutputFormat.Json;

    /// <summary>
    /// Enable verbose logging
    /// </summary>
    public bool Verbose { get; set; }

    /// <summary>
    /// Suppress non-essential output
    /// </summary>
    public bool Quiet { get; set; }

    /// <summary>
    /// Validates the configuration settings
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            throw new InvalidOperationException("Base URL cannot be empty");
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Base URL '{BaseUrl}' is not a valid URI");
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"Base URL must use http or https scheme, got: {uri.Scheme}");
        }
    }

    /// <summary>
    /// Loads configuration from environment variables
    /// </summary>
    public static CliConfiguration LoadFromEnvironment()
    {
        var config = new CliConfiguration();

        var apiUrl = Environment.GetEnvironmentVariable("CDC_API_URL");
        if (!string.IsNullOrWhiteSpace(apiUrl))
        {
            config.BaseUrl = apiUrl;
        }

        return config;
    }
}
