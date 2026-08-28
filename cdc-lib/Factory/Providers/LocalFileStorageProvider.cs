using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Softbase.Cdc.Factory.Interfaces;

namespace Softbase.Cdc.Factory.Providers;

/// <summary>
/// Local file system implementation of <see cref="ITemplateStorageProvider"/>.
/// Reads and writes template backup files on a mounted volume.
/// </summary>
public class LocalFileStorageProvider : ITemplateStorageProvider
{
    private readonly string _baseDirectory;
    private readonly ILogger<LocalFileStorageProvider> _logger;

    public LocalFileStorageProvider(string baseDirectory, ILogger<LocalFileStorageProvider> logger)
    {
        _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!Directory.Exists(_baseDirectory))
            Directory.CreateDirectory(_baseDirectory);
    }

    public async Task<string> StoreAsync(Stream stream, string fileName)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required", nameof(fileName));

        var fullPath = Path.Combine(_baseDirectory, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fileStream = new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);
        await stream.CopyToAsync(fileStream);

        _logger.LogInformation("Stored template file '{FileName}' at '{Path}'", fileName, fullPath);
        return fullPath;
    }

    public Task<Stream> RetrieveAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required", nameof(filePath));

        var fullPath = ResolvePath(filePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Template file not found: {filePath}", filePath);

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream>(stream);
    }

    public Task<bool> DeleteAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required", nameof(filePath));

        var fullPath = ResolvePath(filePath);

        if (!File.Exists(fullPath))
            return Task.FromResult(false);

        File.Delete(fullPath);
        _logger.LogInformation("Deleted template file at '{Path}'", fullPath);
        return Task.FromResult(true);
    }

    public bool Exists(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        var fullPath = ResolvePath(filePath);
        return File.Exists(fullPath);
    }

    /// <summary>
    /// Computes a SHA256 checksum for a file.
    /// Exposed internally for use by the template repository.
    /// </summary>
    internal static string ComputeChecksum(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Resolves a file path relative to the base directory.
    /// If the path is already absolute, it is used as-is.
    /// </summary>
    private string ResolvePath(string filePath)
    {
        if (Path.IsPathRooted(filePath))
            return filePath;

        return Path.Combine(_baseDirectory, filePath);
    }
}
