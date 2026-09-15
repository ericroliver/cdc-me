using System.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Softbase.Cdc.Factory.Models;
using Softbase.Cdc.Factory.Repositories.SqlServer;
using Xunit;

namespace cdc_api.Tests.Factory;

public class SqlServerScriptLibraryTests
{
    private readonly ILogger<SqlServerScriptLibrary> _logger = NullLogger<SqlServerScriptLibrary>.Instance;

    [Fact]
    public void Constructor_ThrowsWhenConnectionStringIsNull()
    {
        var act = () => new SqlServerScriptLibrary(null!, _logger);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("connectionString");
    }

    [Fact]
    public void Constructor_ThrowsWhenLoggerIsNull()
    {
        var act = () => new SqlServerScriptLibrary("Server=localhost", null!);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("logger");
    }

    [Fact]
    public void MapScript_MapsAllFieldsCorrectly()
    {
        var id = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var values = new object?[]
        {
            id,
            "CreateUsers.sql",
            "Creates test users",
            "SqlScript",
            "CREATE LOGIN test_user ...",
            DBNull.Value,
            groupId,
            1,
            now,
            now
        };
        var schema = new[] { "id", "name", "description", "type", "content", "file_path", "script_group_id", "order", "created_at", "updated_at" };

        var reader = new FakeDataReader(values, schema);

        var script = SqlServerScriptLibrary.MapScript(reader);

        script.Id.Should().Be(id);
        script.Name.Should().Be("CreateUsers.sql");
        script.Description.Should().Be("Creates test users");
        script.Type.Should().Be("SqlScript");
        script.Content.Should().Be("CREATE LOGIN test_user ...");
        script.FilePath.Should().BeNull();
        script.ScriptGroupId.Should().Be(groupId);
        script.Order.Should().Be(1);
    }
}
