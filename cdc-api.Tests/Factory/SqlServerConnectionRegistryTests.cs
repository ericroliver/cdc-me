using System.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;
using Softbase.Cdc.Factory.Repositories.SqlServer;
using Xunit;

namespace cdc_api.Tests.Factory;

public class SqlServerConnectionRegistryTests
{
    private readonly ILogger<SqlServerConnectionRegistry> _logger = NullLogger<SqlServerConnectionRegistry>.Instance;

    [Fact]
    public void Constructor_ThrowsWhenConnectionStringIsNull()
    {
        var act = () => new SqlServerConnectionRegistry(null!, _logger);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("connectionString");
    }

    [Fact]
    public void Constructor_ThrowsWhenLoggerIsNull()
    {
        var act = () => new SqlServerConnectionRegistry("Server=localhost", null!);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("logger");
    }

    [Fact]
    public void MapConnection_MapsAllFieldsCorrectly()
    {
        var values = new object?[]
        {
            Guid.NewGuid(),
            "dev-sqlserver",
            "SqlServer",
            "sqlserver",
            1433,
            "Server=sqlserver;User Id=sa;Password=Test123!;TrustServerCertificate=true;",
            "Development SQL Server",
            true,
            DateTime.UtcNow,
            DateTime.UtcNow
        };
        var schema = new[] { "id", "name", "platform", "host", "port", "connection_string", "description", "is_default", "created_at", "updated_at" };

        var reader = new FakeDataReader(values, schema);

        var connection = SqlServerConnectionRegistry.MapConnection(reader);

        connection.Id.Should().Be((Guid)values[0]!);
        connection.Name.Should().Be("dev-sqlserver");
        connection.Platform.Should().Be("SqlServer");
        connection.Host.Should().Be("sqlserver");
        connection.Port.Should().Be(1433);
        connection.ConnectionString.Should().NotBeNullOrEmpty();
        connection.Description.Should().Be("Development SQL Server");
        connection.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void MapConnection_HandlesNullOptionalFields()
    {
        var values = new object?[]
        {
            Guid.NewGuid(),
            "qa-sqlserver",
            "SqlServer",
            DBNull.Value,
            DBNull.Value,
            "Server=qa;User Id=sa;Password=Test123!;",
            DBNull.Value,
            false,
            DateTime.UtcNow,
            DateTime.UtcNow
        };
        var schema = new[] { "id", "name", "platform", "host", "port", "connection_string", "description", "is_default", "created_at", "updated_at" };

        var reader = new FakeDataReader(values, schema);

        var connection = SqlServerConnectionRegistry.MapConnection(reader);

        connection.Host.Should().BeEmpty();
        connection.Port.Should().BeNull();
        connection.Description.Should().BeNull();
        connection.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenNameIsEmpty()
    {
        var registry = new SqlServerConnectionRegistry("Server=localhost", _logger);
        var request = new CreateConnectionRequest { Name = "", ConnectionString = "cs" };

        var act = () => registry.CreateAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
           .WithMessage("*Name is required*");
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenConnectionStringIsEmpty()
    {
        var registry = new SqlServerConnectionRegistry("Server=localhost", _logger);
        var request = new CreateConnectionRequest { Name = "test", ConnectionString = "" };

        var act = () => registry.CreateAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
           .WithMessage("*ConnectionString is required*");
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsNull_WhenNameIsWhitespace()
    {
        var registry = new SqlServerConnectionRegistry("Server=localhost", _logger);
        var result = await registry.GetByNameAsync("   ");
        result.Should().BeNull();
    }
}
