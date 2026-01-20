using System.Text.Json;
using cdc_cli.Configuration;
using Microsoft.Extensions.Logging;

namespace cdc_cli.Services;

/// <summary>
/// Implementation of JSON input/output handler
/// </summary>
public class JsonHandler : IJsonHandler
{
    private readonly ILogger<JsonHandler> _logger;
    private readonly CliConfiguration _configuration;
    private readonly JsonSerializerOptions _compactOptions;
    private readonly JsonSerializerOptions _prettyOptions;

    /// <summary>
    /// Initializes a new instance of the JsonHandler
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">CLI configuration</param>
    public JsonHandler(ILogger<JsonHandler> logger, CliConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        _compactOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        _prettyOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };
    }

    /// <inheritdoc />
    public async Task<T?> ReadInputAsync<T>(string? dataString, string? filePath, bool allowStdin = true) where T : class
    {
        try
        {
            // Priority 1: Inline data string
            if (!string.IsNullOrWhiteSpace(dataString))
            {
                if (_configuration.Verbose)
                {
                    _logger.LogDebug("Reading input from inline data string");
                }
                return JsonSerializer.Deserialize<T>(dataString, _compactOptions);
            }

            // Priority 2: File path
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                // SECURITY: Validate file path to prevent path traversal attacks
                var validatedPath = ValidateFilePath(filePath);
                
                if (_configuration.Verbose)
                {
                    _logger.LogDebug("Reading input from file: {FilePath}", validatedPath);
                }

                if (!File.Exists(validatedPath))
                {
                    throw new FileNotFoundException($"Input file not found: {validatedPath}", validatedPath);
                }

                var fileContent = await File.ReadAllTextAsync(validatedPath);
                return JsonSerializer.Deserialize<T>(fileContent, _compactOptions);
            }

            // Priority 3: Stdin (if allowed and data is available)
            if (allowStdin && Console.IsInputRedirected)
            {
                if (_configuration.Verbose)
                {
                    _logger.LogDebug("Reading input from stdin");
                }

                using var reader = Console.In;
                var stdinContent = await reader.ReadToEndAsync();

                if (!string.IsNullOrWhiteSpace(stdinContent))
                {
                    return JsonSerializer.Deserialize<T>(stdinContent, _compactOptions);
                }
            }

            // No input available
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON input");
            throw new InvalidOperationException($"Invalid JSON input: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "File I/O error while reading input");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<object> GetInputAsync<T>(string? dataString, string? filePath, T defaultObject, CancellationToken cancellationToken)
    {
        // Try to read JSON input first
        var jsonInput = await ReadInputAsync<object>(dataString, filePath, allowStdin: false);
        if (jsonInput != null)
        {
            return jsonInput;
        }

        // If no JSON input, serialize the default object and return it
        // This ensures consistent handling of the data
        var json = JsonSerializer.Serialize(defaultObject, _compactOptions);
        return JsonSerializer.Deserialize<object>(json, _compactOptions) ?? defaultObject!;
    }

    /// <inheritdoc />
    public async Task WriteOutputAsync<T>(T data, OutputFormat format = OutputFormat.Json)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        try
        {
            switch (format)
            {
                case OutputFormat.Json:
                    var compactJson = JsonSerializer.Serialize(data, _compactOptions);
                    await Console.Out.WriteLineAsync(compactJson);
                    break;

                case OutputFormat.JsonPretty:
                    var prettyJson = JsonSerializer.Serialize(data, _prettyOptions);
                    await Console.Out.WriteLineAsync(prettyJson);
                    break;

                case OutputFormat.Text:
                    await WriteTextOutputAsync(data);
                    break;

                default:
                    throw new ArgumentException($"Unsupported output format: {format}", nameof(format));
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to serialize output");
            throw new InvalidOperationException($"Failed to serialize output: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task WriteErrorAsync(string message, int exitCode)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Error message cannot be empty", nameof(message));
        }

        await Console.Error.WriteLineAsync($"Error: {message}");
        await Console.Error.WriteLineAsync($"Error: {message}");
        if (_configuration.Verbose)
        {
            _logger.LogError("Command failed with exit code {ExitCode}: {Message}", exitCode, message);
        }

        Environment.ExitCode = exitCode;
    }

    /// <summary>
    /// Writes output in human-readable text format
    /// </summary>
    /// <typeparam name="T">Type of data to write</typeparam>
    /// <param name="data">Data to write</param>
    private async Task WriteTextOutputAsync<T>(T data)
    {
        // For text output, we'll use reflection to display properties
        // This is a simple implementation that can be enhanced later
        var type = data?.GetType() ?? typeof(T);
        await Console.Out.WriteLineAsync($"{type.Name}:");
        await Console.Out.WriteLineAsync(new string('-', 40));

        var properties = type.GetProperties();
        foreach (var prop in properties)
        {
            var value = prop.GetValue(data);
            var valueStr = FormatValue(value);
            await Console.Out.WriteLineAsync($"{prop.Name}: {valueStr}");
        }
    }

    /// <summary>
    /// Validates a file path to prevent path traversal attacks
    /// </summary>
    /// <param name="filePath">File path to validate</param>
    /// <returns>Validated and normalized file path</returns>
    /// <exception cref="ArgumentException">Thrown if path is invalid or contains traversal patterns</exception>
    private static string ValidateFilePath(string filePath)
    {
        // SECURITY: Prevent path traversal attacks
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be empty", nameof(filePath));
        }

        // Get the full normalized path
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(filePath);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid file path: {ex.Message}", nameof(filePath), ex);
        }

        // Check for path traversal patterns
        if (filePath.Contains("..", StringComparison.Ordinal) ||
            filePath.Contains("~", StringComparison.Ordinal))
        {
            // Verify the normalized path is within current directory or is an absolute path
            var currentDir = Directory.GetCurrentDirectory();
            if (!fullPath.StartsWith(currentDir, StringComparison.OrdinalIgnoreCase) &&
                !Path.IsPathRooted(filePath))
            {
                throw new ArgumentException(
                    "Path traversal patterns detected. Use absolute paths or paths relative to current directory.",
                    nameof(filePath));
            }
        }

        return fullPath;
    }

    /// <summary>
    /// Formats a property value for text display
    /// </summary>
    /// <param name="value">Value to format</param>
    /// <returns>Formatted string representation</returns>
    private static string FormatValue(object? value)
    {
        if (value == null)
        {
            return "(null)";
        }

        if (value is System.Collections.IEnumerable enumerable and not string)
        {
            var items = new List<string>();
            foreach (var item in enumerable)
            {
                items.Add(item?.ToString() ?? "(null)");
            }
            return items.Count > 0 ? $"[{string.Join(", ", items)}]" : "[]";
        }

        return value.ToString() ?? "(empty)";
    }
}
