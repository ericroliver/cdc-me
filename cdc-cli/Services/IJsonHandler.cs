using cdc_cli.Configuration;

namespace cdc_cli.Services;

/// <summary>
/// Interface for handling JSON input/output operations
/// </summary>
public interface IJsonHandler
{
    /// <summary>
    /// Reads and deserializes JSON input from various sources
    /// </summary>
    /// <typeparam name="T">Type to deserialize to</typeparam>
    /// <param name="dataString">Inline JSON string (highest priority)</param>
    /// <param name="filePath">Path to JSON file (second priority)</param>
    /// <param name="allowStdin">Whether to read from stdin if other sources unavailable</param>
    /// <returns>Deserialized object, or null if no input available</returns>
    Task<T?> ReadInputAsync<T>(string? dataString, string? filePath, bool allowStdin = true) where T : class;

    /// <summary>
    /// Gets input from various sources with fallback to default object
    /// </summary>
    /// <typeparam name="T">Type to deserialize to</typeparam>
    /// <param name="dataString">Inline JSON string (highest priority)</param>
    /// <param name="filePath">Path to JSON file (second priority)</param>
    /// <param name="defaultObject">Default object to use if no JSON input available</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deserialized object from JSON or default object</returns>
    Task<object> GetInputAsync<T>(string? dataString, string? filePath, T defaultObject, CancellationToken cancellationToken);

    /// <summary>
    /// Writes output in the specified format
    /// </summary>
    /// <typeparam name="T">Type of data to write</typeparam>
    /// <param name="data">Data to serialize and write</param>
    /// <param name="format">Output format (json, json-pretty, or text)</param>
    Task WriteOutputAsync<T>(T data, OutputFormat format = OutputFormat.Json);

    /// <summary>
    /// Writes an error message to stderr and returns appropriate exit code
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="exitCode">Exit code to return</param>
    Task WriteErrorAsync(string message, int exitCode);
}
