using System.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Softbase.Cdc.Factory.Models;
using Softbase.Cdc.Factory.Repositories.SqlServer;
using Xunit;

namespace cdc_api.Tests.Factory;

public class SqlServerOrderRepositoryTests
{
    private readonly ILogger<SqlServerOrderRepository> _logger = NullLogger<SqlServerOrderRepository>.Instance;

    [Fact]
    public void Constructor_ThrowsWhenConnectionStringIsNull()
    {
        var act = () => new SqlServerOrderRepository(null!, _logger);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("connectionString");
    }

    [Fact]
    public void Constructor_ThrowsWhenLoggerIsNull()
    {
        var act = () => new SqlServerOrderRepository("Server=localhost", null!);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("logger");
    }

    [Fact]
    public void MapOrder_MapsAllFieldsCorrectly()
    {
        var orderId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var values = new object?[]
        {
            orderId,
            templateId,
            connectionId,
            "TestDB",
            "Completed",
            DBNull.Value,
            now,
            now.AddMinutes(-5),
            now
        };
        var schema = new[] { "id", "template_id", "target_connection_id", "target_database_name", "status", "error_message", "created_at", "started_at", "completed_at" };

        var reader = new FakeDataReader(values, schema);

        var order = SqlServerOrderRepository.MapOrder(reader);

        order.Id.Should().Be(orderId);
        order.TemplateId.Should().Be(templateId);
        order.TargetConnectionId.Should().Be(connectionId);
        order.TargetDatabaseName.Should().Be("TestDB");
        order.Status.Should().Be("Completed");
        order.ErrorMessage.Should().BeNull();
        order.CreatedAt.Should().Be(now);
        order.StartedAt.Should().Be(now.AddMinutes(-5));
        order.CompletedAt.Should().Be(now);
    }

    [Fact]
    public void MapOrder_HandlesNullOptionalFields()
    {
        var orderId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var values = new object?[]
        {
            orderId,
            templateId,
            DBNull.Value,
            "TestDB",
            "Pending",
            DBNull.Value,
            now,
            DBNull.Value,
            DBNull.Value
        };
        var schema = new[] { "id", "template_id", "target_connection_id", "target_database_name", "status", "error_message", "created_at", "started_at", "completed_at" };

        var reader = new FakeDataReader(values, schema);

        var order = SqlServerOrderRepository.MapOrder(reader);

        order.TargetConnectionId.Should().BeNull();
        order.ErrorMessage.Should().BeNull();
        order.StartedAt.Should().BeNull();
        order.CompletedAt.Should().BeNull();
    }
}
