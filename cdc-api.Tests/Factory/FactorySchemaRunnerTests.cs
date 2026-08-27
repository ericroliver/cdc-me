using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Softbase.Cdc.Factory;
using Xunit;

namespace cdc_api.Tests.Factory;

public class FactorySchemaRunnerTests
{
    private readonly ILogger<FactorySchemaRunner> _logger = NullLogger<FactorySchemaRunner>.Instance;

    [Fact]
    public void Constructor_ThrowsWhenConnectionStringIsNull()
    {
        var act = () => new FactorySchemaRunner(null!, _logger);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("connectionString");
    }

    [Fact]
    public void Constructor_ThrowsWhenLoggerIsNull()
    {
        var act = () => new FactorySchemaRunner("Host=localhost;Database=dtai", null!);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("logger");
    }

    [Theory]
    [InlineData("cdc_lib.Factory.Migrations.Factory.001_create_connections_table.sql", true)]
    [InlineData("cdc_lib.Factory.Migrations.Factory.002_create_templates_table.sql", true)]
    [InlineData("cdc_lib.Factory.Migrations.Factory.099_create_provisioned_databases_table.sql", true)]
    [InlineData("cdc_lib.Other.Migrations.Foo.001_bar.sql", false)]
    [InlineData("cdc_lib.Factory.Migrations.Factory.001_create_connections_table.txt", false)]
    [InlineData("SomeOtherAssembly.Factory.Migrations.Factory.001.sql", false)]
    public void ScriptFilter_SelectsOnlyFactoryMigrations(string scriptPath, bool expected)
    {
        FactorySchemaRunner.ScriptFilter(scriptPath).Should().Be(expected);
    }

    [Fact]
    public void MigrationResourcePrefix_MatchesEmbeddedResourceNaming()
    {
        // The embedded SQL file lives at:
        //   Factory/Migrations/Factory/001_create_connections_table.sql
        // With RootNamespace = "cdc_lib" the manifest resource name should start
        // with the configured prefix.
        FactorySchemaRunner.MigrationResourcePrefix
            .Should().Be("cdc_lib.Factory.Migrations.Factory.");
    }

    [Fact]
    public void EmbeddedMigrationScript_ExistsAndContainsCreateTable()
    {
        // --- Arrange: load the cdc-lib assembly (where the migration SQL lives) ---
        var cdcLibAssembly = Assembly.Load("cdc-lib")!;

        // --- Act: find embedded resources matching the Factory migration filter ---
        var migrationResources = cdcLibAssembly.GetManifestResourceNames()
            .Where(FactorySchemaRunner.ScriptFilter)
            .ToList();

        // --- Assert: at least the 001 migration is present ---
        migrationResources.Should().NotBeEmpty();
        migrationResources.Should().Contain(r => r.Contains("001_create_connections_table"));

        // Read and verify the SQL content contains the expected DDL
        var resourceName = migrationResources.First(r => r.Contains("001_create_connections_table"));
        using var stream = cdcLibAssembly.GetManifestResourceStream(resourceName);
        stream.Should().NotBeNull();
        using var reader = new StreamReader(stream!);
        var sql = reader.ReadToEnd();

        sql.Should().Contain("CREATE TABLE");
        sql.Should().Contain("factory_connections");
    }
}
