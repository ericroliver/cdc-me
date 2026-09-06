using System.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Softbase.Cdc.Factory.Models;
using Softbase.Cdc.Factory.Repositories.SqlServer;
using Xunit;

namespace cdc_api.Tests.Factory;

public class SqlServerDatabaseRegistryTests
{
    private readonly ILogger<SqlServerDatabaseRegistry> _logger = NullLogger<SqlServerDatabaseRegistry>.Instance;

    [Fact]
    public void Constructor_ThrowsWhenConnectionStringIsNull()
    {
        var act = () => new SqlServerDatabaseRegistry(null!, _logger);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("connectionString");
    }

    [Fact]
    public void Constructor_ThrowsWhenLoggerIsNull()
    {
        var act = () => new SqlServerDatabaseRegistry("Server=localhost", null!);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("logger");
    }

    [Fact]
    public void MapProvisionedDatabase_MapsAllFieldsCorrectly()
    {
        var id = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var values = new object?[]
        {
            id,
            orderId,
            "TestDB_001",
            connectionId,
            templateId,
            "Active",
            now,
            DBNull.Value
        };
        var schema = new[] { "id", "order_id", "database_name", "connection_id", "template_id", "status", "created_at", "decommissioned_at" };

        var reader = new FakeDataReader(values, schema);

        var db = SqlServerDatabaseRegistry.MapProvisionedDatabase(reader);

        db.Id.Should().Be(id);
        db.OrderId.Should().Be(orderId);
        db.DatabaseName.Should().Be("TestDB_001");
        db.ConnectionId.Should().Be(connectionId);
        db.TemplateId.Should().Be(templateId);
        db.Status.Should().Be("Active");
        db.CreatedAt.Should().Be(now);
        db.DecommissionedAt.Should().BeNull();
    }
}
