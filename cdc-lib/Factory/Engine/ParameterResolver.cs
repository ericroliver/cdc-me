using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;

namespace Softbase.Cdc.Factory.Engine;

/// <summary>
/// Resolves and merges parameters from inline JSON and JSON/YAML parameter files.
/// Also resolves target database name template strings using the parameter bag.
/// </summary>
public class ParameterResolver
{
    private readonly ILogger<ParameterResolver> _logger;

    public ParameterResolver(ILogger<ParameterResolver> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Merges inline parameters with parameters loaded from a file.
    /// Inline values take precedence over file-loaded values.
    /// </summary>
    public IReadOnlyDictionary<string, object?> MergeParameters(
        IReadOnlyDictionary<string, object?>? inline,
        string? parameterFilePath)
    {
        var merged = new Dictionary<string, object?>();

        // Load from file first (lower precedence)
        if (!string.IsNullOrWhiteSpace(parameterFilePath))
        {
            var fileParams = LoadFromFile(parameterFilePath);
            foreach (var (key, value) in fileParams)
            {
                merged[key] = value;
            }
        }

        // Then overlay inline (higher precedence)
        if (inline != null)
        {
            foreach (var (key, value) in inline)
            {
                merged[key] = value;
            }
        }

        return merged;
    }

    /// <summary>
    /// Loads parameters from a JSON or YAML file.
    /// Format is determined by file extension.
    /// </summary>
    public IReadOnlyDictionary<string, object?> LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Parameter file not found: {filePath}");

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var content = File.ReadAllText(filePath);

        return extension switch
        {
            ".json" => LoadJson(content),
            ".yaml" or ".yml" => LoadYaml(content),
            _ => throw new NotSupportedException(
                $"Unsupported parameter file format: '{extension}'. Supported: .json, .yaml, .yml")
        };
    }

    /// <summary>
    /// Resolves a template string using the parameter bag.
    /// Replaces {key} tokens with corresponding values.
    /// Supports built-in tokens: {date}, {user}, {guid}
    /// </summary>
    public string ResolveDatabaseName(
        string template,
        IReadOnlyDictionary<string, object?> parameters)
    {
        if (string.IsNullOrWhiteSpace(template))
            return template;

        var result = template;

        // Replace {key} tokens with parameter values (case-insensitive)
        foreach (var (key, value) in parameters)
        {
            result = result.Replace($"{{{key}}}", value?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
        }

        // Built-in tokens
        result = result.Replace("{date}", DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{user}", Environment.UserName ?? "unknown", StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{guid}", Guid.NewGuid().ToString("N")[..8], StringComparison.OrdinalIgnoreCase);

        return result;
    }

    /// <summary>
    /// Deserializes JSON content into a parameter dictionary.
    /// Exposed internally for testing.
    /// </summary>
    internal static IReadOnlyDictionary<string, object?> LoadJson(string content)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content, options)
                   ?? new Dictionary<string, JsonElement>();

        var dict = new Dictionary<string, object?>();
        foreach (var (key, element) in raw)
        {
            dict[key] = ConvertJsonElement(element);
        }

        return dict;
    }

    /// <summary>
    /// Converts a <see cref="JsonElement"/> to its .NET equivalent (int, string, bool, etc.).
    /// </summary>
    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out var i) => i,
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }

    /// <summary>
    /// Deserializes YAML content into a parameter dictionary.
    /// Exposed internally for testing.
    /// </summary>
    internal static IReadOnlyDictionary<string, object?> LoadYaml(string content)
    {
        var deserializer = new DeserializerBuilder().Build();
        var raw = deserializer.Deserialize<Dictionary<string, object?>>(content)
                   ?? new Dictionary<string, object?>();

        // YamlDotNet may return string values for scalars that look like numbers.
        // Post-process to convert to proper .NET types.
        var dict = new Dictionary<string, object?>();
        foreach (var (key, value) in raw)
        {
            dict[key] = ConvertYamlValue(value);
        }

        return dict;
    }

    /// <summary>
    /// Converts a YAML scalar value to its .NET equivalent (int, bool, string, etc.).
    /// </summary>
    private static object? ConvertYamlValue(object? value)
    {
        if (value is string s)
        {
            if (int.TryParse(s, out var i)) return i;
            if (long.TryParse(s, out var l)) return l;
            if (double.TryParse(s, out var d)) return d;
            if (bool.TryParse(s, out var b)) return b;
        }

        return value;
    }
}
