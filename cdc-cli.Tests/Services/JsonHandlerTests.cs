using System.Text.Json;
using cdc_cli.Configuration;
using cdc_cli.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace cdc_cli.Tests.Services;

/// <summary>
/// Tests for JSON input/output handler
/// </summary>
public class JsonHandlerTests
{
    private readonly Mock<ILogger<JsonHandler>> _loggerMock;
    private readonly CliConfiguration _configuration;
    private readonly JsonHandler _handler;

    /// <summary>
    /// Initializes a new instance of the JsonHandlerTests class
    /// </summary>
    public JsonHandlerTests()
    {
        _loggerMock = new Mock<ILogger<JsonHandler>>();
        _configuration = new CliConfiguration();
        _handler = new JsonHandler(_loggerMock.Object, _configuration);
    }

    /// <summary>
    /// Tests reading input from inline data string
    /// </summary>
    [Fact]
    public async Task ReadInputAsync_WithInlineData_ReturnsDeserializedObject()
    {
        // Arrange
        var jsonData = """{"name":"test","value":123}""";

        // Act
        var result = await _handler.ReadInputAsync<TestModel>(jsonData, null, false);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("test");
        result.Value.Should().Be(123);
    }

    /// <summary>
    /// Tests reading input from file
    /// </summary>
    [Fact]
    public async Task ReadInputAsync_WithFile_ReturnsDeserializedObject()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var jsonData = """{"name":"file-test","value":456}""";
        await File.WriteAllTextAsync(tempFile, jsonData);

        try
        {
            // Act
            var result = await _handler.ReadInputAsync<TestModel>(null, tempFile, false);

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().Be("file-test");
            result.Value.Should().Be(456);
        }
        finally
        {
            // Cleanup
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Tests reading from file throws exception when file doesn't exist
    /// </summary>
    [Fact]
    public async Task ReadInputAsync_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentFile = "/path/to/nonexistent/file.json";

        // Act
        var act = async () => await _handler.ReadInputAsync<TestModel>(null, nonExistentFile, false);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    /// <summary>
    /// Tests reading invalid JSON throws exception
    /// </summary>
    [Fact]
    public async Task ReadInputAsync_WithInvalidJson_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidJson = "{invalid json}";

        // Act
        var act = async () => await _handler.ReadInputAsync<TestModel>(invalidJson, null, false);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid JSON*");
    }

    /// <summary>
    /// Tests reading with no input returns null
    /// </summary>
    [Fact]
    public async Task ReadInputAsync_WithNoInput_ReturnsNull()
    {
        // Act
        var result = await _handler.ReadInputAsync<TestModel>(null, null, false);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Tests priority: inline data is used over file
    /// </summary>
    [Fact]
    public async Task ReadInputAsync_WithBothInlineAndFile_PrefersInlineData()
    {
        // Arrange
        var inlineData = """{"name":"inline","value":1}""";
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, """{"name":"file","value":2}""");

        try
        {
            // Act
            var result = await _handler.ReadInputAsync<TestModel>(inlineData, tempFile, false);

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().Be("inline");
            result.Value.Should().Be(1);
        }
        finally
        {
            // Cleanup
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Tests writing error to stderr sets exit code
    /// </summary>
    [Fact]
    public async Task WriteErrorAsync_SetsExitCode()
    {
        // Arrange
        var originalExitCode = Environment.ExitCode;
        var testMessage = "Test error message";
        var expectedExitCode = 42;

        try
        {
            // Act
            await _handler.WriteErrorAsync(testMessage, expectedExitCode);

            // Assert
            Environment.ExitCode.Should().Be(expectedExitCode);
        }
        finally
        {
            // Cleanup
            Environment.ExitCode = originalExitCode;
        }
    }

    /// <summary>
    /// Tests writing error with empty message throws exception
    /// </summary>
    [Fact]
    public async Task WriteErrorAsync_WithEmptyMessage_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _handler.WriteErrorAsync("", 1);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    /// <summary>
    /// Test model for deserialization
    /// </summary>
    private class TestModel
    {
        /// <summary>
        /// Test name property
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Test value property
        /// </summary>
        public int Value { get; set; }
    }
}
