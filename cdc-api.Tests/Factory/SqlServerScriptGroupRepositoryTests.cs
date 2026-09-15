using System.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Softbase.Cdc.Factory.Models;
using Softbase.Cdc.Factory.Repositories.SqlServer;
using Xunit;

namespace cdc_api.Tests.Factory;

public class SqlServerScriptGroupRepositoryTests
{
    private readonly ILogger<SqlServerScriptGroupRepository> _logger = NullLogger<SqlServerScriptGroupRepository>.Instance;

    [Fact]
    public void Constructor_ThrowsWhenConnectionStringIsNull()
    {
        var act = () => new SqlServerScriptGroupRepository(null!, _logger);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("connectionString");
    }

    [Fact]
    public void Constructor_ThrowsWhenLoggerIsNull()
    {
        var act = () => new SqlServerScriptGroupRepository("Server=localhost", null!);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("logger");
    }

    [Fact]
    public void MapGroup_MapsAllFieldsCorrectly()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var values = new object?[]
        {
            id,
            "Pre-Hydration",
            "Scripts run before restore",
            1,
            10,
            now,
            now
        };
        var schema = new[] { "id", "name", "description", "layer", "order", "created_at", "updated_at" };

        var reader = new FakeDataReader(values, schema);

        var group = SqlServerScriptGroupRepository.MapGroup(reader);

        group.Id.Should().Be(id);
        group.Name.Should().Be("Pre-Hydration");
        group.Description.Should().Be("Scripts run before restore");
        group.Layer.Should().Be(1);
        group.Order.Should().Be(10);
    }

    [Fact]
    public void MapGroup_HandlesNullDescription()
    {
        var values = new object?[]
        {
            Guid.NewGuid(),
            "Post-Hydration",
            DBNull.Value,
            2,
            5,
            DateTime.UtcNow,
            DateTime.UtcNow
        };
        var schema = new[] { "id", "name", "description", "layer", "order", "created_at", "updated_at" };

        var reader = new FakeDataReader(values, schema);

        var group = SqlServerScriptGroupRepository.MapGroup(reader);

        group.Description.Should().BeNull();
    }
}
