using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Softbase.Cdc.Factory.Providers;
using Xunit;

namespace cdc_api.Tests.Factory;

public class SqlServerDatabaseProviderTests
{
    private readonly SqlServerDatabaseProvider _provider = new(
        NullLogger<SqlServerDatabaseProvider>.Instance);

    [Fact]
    public void Constructor_ThrowsWhenLoggerIsNull()
    {
        var act = () => new SqlServerDatabaseProvider(null!);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("logger");
    }

    [Theory]
    [InlineData("", "db", "cs")]
    [InlineData("  ", "db", "cs")]
    [InlineData("/backup.bak", "", "cs")]
    [InlineData("/backup.bak", "  ", "cs")]
    [InlineData("/backup.bak", "db", "")]
    [InlineData("/backup.bak", "db", "  ")]
    public async Task RestoreBackupAsync_ThrowsWhenArgsEmpty(
        string backupPath, string dbName, string connStr)
    {
        var act = () => _provider.RestoreBackupAsync(backupPath, dbName, connStr);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("", "cs")]
    [InlineData("  ", "cs")]
    [InlineData("db", "")]
    [InlineData("db", "  ")]
    public async Task CreateDatabaseAsync_ThrowsWhenArgsEmpty(string dbName, string connStr)
    {
        var act = () => _provider.CreateDatabaseAsync(dbName, connStr);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("", "cs")]
    [InlineData("  ", "cs")]
    [InlineData("db", "")]
    [InlineData("db", "  ")]
    public async Task DropDatabaseAsync_ThrowsWhenArgsEmpty(string dbName, string connStr)
    {
        var act = () => _provider.DropDatabaseAsync(dbName, connStr);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task TestConnectionAsync_ThrowsWhenEmpty(string connStr)
    {
        var act = () => _provider.TestConnectionAsync(connStr);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("", "SELECT 1")]
    [InlineData("cs", "")]
    [InlineData("cs", "  ")]
    public async Task ExecuteSqlAsync_ThrowsWhenArgsEmpty(string connStr, string sql)
    {
        var act = () => _provider.ExecuteSqlAsync(connStr, sql);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TestConnectionAsync_ReturnsFalse_WhenInvalidConnectionString()
    {
        var result = await _provider.TestConnectionAsync(
            "Server=nonexistent;Database=none;User Id=x;Password=y;TrustServerCertificate=true;");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteSqlAsync_ReturnsFail_WhenInvalidConnectionString()
    {
        var result = await _provider.ExecuteSqlAsync(
            "Server=nonexistent;Database=none;User Id=x;Password=y;TrustServerCertificate=true;",
            "SELECT 1");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateDatabaseAsync_ReturnsFail_WhenInvalidConnectionString()
    {
        var result = await _provider.CreateDatabaseAsync(
            "testdb",
            "Server=nonexistent;Database=none;User Id=x;Password=y;TrustServerCertificate=true;");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DropDatabaseAsync_ReturnsFail_WhenInvalidConnectionString()
    {
        var result = await _provider.DropDatabaseAsync(
            "testdb",
            "Server=nonexistent;Database=none;User Id=x;Password=y;TrustServerCertificate=true;");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void EscapeSqlLiteral_EscapesSingleQuotes()
    {
        SqlServerDatabaseProvider.EscapeSqlLiteral("O'Brien").Should().Be("O''Brien");
        SqlServerDatabaseProvider.EscapeSqlLiteral("normal").Should().Be("normal");
        SqlServerDatabaseProvider.EscapeSqlLiteral("a'b'c").Should().Be("a''b''c");
    }

    [Fact]
    public async Task ExecuteSqlAsync_WithParameters_PassesParamsToCommand()
    {
        // This test verifies the method handles parameters without throwing.
        // With an invalid connection, it should return a failure result.
        var parameters = new Dictionary<string, object?>
        {
            ["@BranchCount"] = 10,
            ["@Industry"] = "HD"
        };

        var result = await _provider.ExecuteSqlAsync(
            "Server=nonexistent;Database=none;User Id=x;Password=y;TrustServerCertificate=true;",
            "SELECT @BranchCount",
            parameters);

        result.Success.Should().BeFalse();
    }
}
