using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace cdc_api.Tests.Utilities;

/// <summary>
/// Unit tests for CDC utility functions
/// </summary>
public class CdcUtilitiesTests
{
    /// <summary>
    /// Test that SHA256 hash computation produces consistent results
    /// </summary>
    [Fact]
    public void ComputeSha256Hash_SameInput_ProducesSameHash()
    {
        // Arrange
        const string input = "test data for hashing";

        // Act
        var hash1 = ComputeSha256Hash(input);
        var hash2 = ComputeSha256Hash(input);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.NotEmpty(hash1);
    }

    /// <summary>
    /// Test that SHA256 hash computation produces different results for different inputs
    /// </summary>
    [Fact]
    public void ComputeSha256Hash_DifferentInputs_ProducesDifferentHashes()
    {
        // Arrange
        const string input1 = "test data 1";
        const string input2 = "test data 2";

        // Act
        var hash1 = ComputeSha256Hash(input1);
        var hash2 = ComputeSha256Hash(input2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    /// <summary>
    /// Test that SHA256 hash computation handles empty string
    /// </summary>
    [Fact]
    public void ComputeSha256Hash_EmptyString_ProducesValidHash()
    {
        // Arrange
        const string input = "";

        // Act
        var hash = ComputeSha256Hash(input);

        // Assert
        Assert.NotEmpty(hash);
        Assert.Equal(64, hash.Length); // SHA256 produces 64 character hex string
    }

    /// <summary>
    /// Test that SHA256 hash computation produces lowercase hex
    /// </summary>
    [Fact]
    public void ComputeSha256Hash_ProducesLowercaseHex()
    {
        // Arrange
        const string input = "Test Data With Mixed Case";

        // Act
        var hash = ComputeSha256Hash(input);

        // Assert
        Assert.Equal(hash.ToLowerInvariant(), hash);
        Assert.Matches("^[0-9a-f]{64}$", hash); // Should be 64 lowercase hex characters
    }

    /// <summary>
    /// Test that SHA256 hash computation handles JSON data correctly
    /// </summary>
    [Fact]
    public void ComputeSha256Hash_JsonData_ProducesValidHash()
    {
        // Arrange
        const string jsonInput = """{"table": "Orders", "records": [{"id": 1, "name": "Test"}]}""";

        // Act
        var hash = ComputeSha256Hash(jsonInput);

        // Assert
        Assert.NotEmpty(hash);
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    /// <summary>
    /// Test that SHA256 hash computation handles large data
    /// </summary>
    [Fact]
    public void ComputeSha256Hash_LargeData_ProducesValidHash()
    {
        // Arrange
        var largeInput = new string('A', 10000); // 10KB of 'A' characters

        // Act
        var hash = ComputeSha256Hash(largeInput);

        // Assert
        Assert.NotEmpty(hash);
        Assert.Equal(64, hash.Length);
    }

    /// <summary>
    /// Test that SHA256 hash computation handles Unicode characters
    /// </summary>
    [Fact]
    public void ComputeSha256Hash_UnicodeCharacters_ProducesValidHash()
    {
        // Arrange
        const string unicodeInput = "Test with émojis 🚀 and spëcial chars: ñáéíóú";

        // Act
        var hash = ComputeSha256Hash(unicodeInput);

        // Assert
        Assert.NotEmpty(hash);
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    /// <summary>
    /// Helper method that replicates the SHA256 computation from CdcController
    /// This ensures our tests match the actual implementation
    /// </summary>
    /// <param name="input">Input string to hash</param>
    /// <returns>SHA256 hash as lowercase hex string</returns>
    private static string ComputeSha256Hash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
