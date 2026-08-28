using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Softbase.Cdc.Factory.Providers;
using Xunit;

namespace cdc_api.Tests.Factory;

public class LocalFileStorageProviderTests
{
    private readonly string _tempDir;
    private readonly LocalFileStorageProvider _provider;

    public LocalFileStorageProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"factory_test_{Guid.NewGuid():N}");
        _provider = new LocalFileStorageProvider(_tempDir, NullLogger<LocalFileStorageProvider>.Instance);
    }

    [Fact]
    public void Constructor_CreatesDirectoryIfNotExists()
    {
        Directory.Exists(_tempDir).Should().BeTrue();
    }

    [Fact]
    public void Exists_ReturnsFalse_ForNonExistentFile()
    {
        _provider.Exists("nonexistent.bak").Should().BeFalse();
    }

    [Fact]
    public async Task StoreAsync_CreatesFileWithContent()
    {
        var content = "test backup content";
        var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

        var path = await _provider.StoreAsync(stream, "test.bak");

        File.Exists(path).Should().BeTrue();
        var fileContent = await File.ReadAllTextAsync(path);
        fileContent.Should().Be(content);
    }

    [Fact]
    public async Task RetrieveAsync_ReturnsStreamWithContent()
    {
        var content = "backup data";
        var path = Path.Combine(_tempDir, "retrieve-test.bak");
        await File.WriteAllTextAsync(path, content);

        var stream = await _provider.RetrieveAsync(path);
        using var reader = new StreamReader(stream);
        var readContent = await reader.ReadToEndAsync();

        readContent.Should().Be(content);
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile()
    {
        var path = Path.Combine(_tempDir, "delete-test.bak");
        await File.WriteAllTextAsync(path, "content");

        var deleted = await _provider.DeleteAsync(path);

        deleted.Should().BeTrue();
        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_ForNonExistentFile()
    {
        var deleted = await _provider.DeleteAsync("nonexistent.bak");
        deleted.Should().BeFalse();
    }

    [Fact]
    public void Exists_ReturnsTrue_ForExistingFile()
    {
        var path = Path.Combine(_tempDir, "exists-test.bak");
        File.WriteAllText(path, "content");

        _provider.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void ComputeChecksum_ReturnsConsistentHash()
    {
        var path = Path.Combine(_tempDir, "checksum-test.bak");
        File.WriteAllText(path, "test content");

        var checksum1 = LocalFileStorageProvider.ComputeChecksum(path);
        var checksum2 = LocalFileStorageProvider.ComputeChecksum(path);

        checksum1.Should().Be(checksum2);
        checksum1.Should().HaveLength(64); // SHA256 hex = 64 chars
    }
}
