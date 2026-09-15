using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Softbase.Cdc.Data;
using Softbase.Cdc.Factory;
using Xunit;

namespace cdc_api.Tests.Factory;

public class FactorySchemaRunnerTests
{
    private readonly ILogger<FactorySchemaRunner> _logger = NullLogger<FactorySchemaRunner>.Instance;

    [Fact]
    public void Constructor_ThrowsWhenConnectionStringIsNull()
    {
        var act = () => new FactorySchemaRunner(null!, DatabaseProvider.PostgreSQL, _logger);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("connectionString");
    }

    [Fact]
    public void Constructor_ThrowsWhenLoggerIsNull()
    {
        var act = () => new FactorySchemaRunner("Host=localhost;Database=dtai", DatabaseProvider.PostgreSQL, null!);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("logger");
    }

    [Theory]
    [InlineData("cdc_lib.Factory.Migrations.Factory.001_create_connections_table.sql", true, FactorySchemaRunner.PostgresMigrationResourcePrefix)]
    [InlineData("cdc_lib.Factory.Migrations.Factory.002_create_templates_table.sql", true, FactorySchemaRunner.PostgresMigrationResourcePrefix)]
    [InlineData("cdc_lib.Factory.Migrations.Factory.009_create_provisioned_databases_table.sql", true, FactorySchemaRunner.PostgresMigrationResourcePrefix)]
    [InlineData("cdc_lib.Factory.Migrations.Factory.SqlServer.001_create_connections_table.sql", true, FactorySchemaRunner.SqlServerMigrationResourcePrefix)]
    [InlineData("cdc_lib.Factory.Migrations.Factory.SqlServer.009_create_provisioned_databases_table.sql", true, FactorySchemaRunner.SqlServerMigrationResourcePrefix)]
    [InlineData("cdc_lib.Other.Migrations.Foo.001_bar.sql", false, FactorySchemaRunner.PostgresMigrationResourcePrefix)]
    [InlineData("cdc_lib.Factory.Migrations.Factory.001_create_connections_table.txt", false, FactorySchemaRunner.PostgresMigrationResourcePrefix)]
    [InlineData("SomeOtherAssembly.Factory.Migrations.Factory.001.sql", false, FactorySchemaRunner.PostgresMigrationResourcePrefix)]
    [InlineData("cdc_lib.Factory.Migrations.Factory.001_create_connections_table.sql", false, FactorySchemaRunner.SqlServerMigrationResourcePrefix)]
    [InlineData("cdc_lib.Factory.Migrations.Factory.SqlServer.001_create_connections_table.sql", false, FactorySchemaRunner.PostgresMigrationResourcePrefix)]
    public void ScriptFilter_SelectsOnlyMatchingMigrations(string scriptPath, bool expected, string resourcePrefix)
    {
        FactorySchemaRunner.ScriptFilter(scriptPath, resourcePrefix).Should().Be(expected);
    }

    [Fact]
    public void PostgresMigrationResourcePrefix_MatchesEmbeddedResourceNaming()
    {
        FactorySchemaRunner.PostgresMigrationResourcePrefix
            .Should().Be("cdc_lib.Factory.Migrations.Factory.");
    }

    [Fact]
    public void SqlServerMigrationResourcePrefix_MatchesEmbeddedResourceNaming()
    {
        FactorySchemaRunner.SqlServerMigrationResourcePrefix
            .Should().Be("cdc_lib.Factory.Migrations.Factory.SqlServer.");
    }

    [Fact]
    public void EmbeddedMigrationScript_Postgres_ExistsAndContainsCreateTable()
    {
        var cdcLibAssembly = Assembly.Load("cdc-lib")!;

        var migrationResources = cdcLibAssembly.GetManifestResourceNames()
            .Where(r => FactorySchemaRunner.ScriptFilter(r, FactorySchemaRunner.PostgresMigrationResourcePrefix))
            .ToList();

        migrationResources.Should().HaveCount(9);

        var expectedMigrations = new[]
        {
            "001_create_connections_table",
            "002_create_templates_table",
            "003_create_script_groups_table",
            "004_create_script_group_dependencies_table",
            "005_create_scripts_table",
            "006_create_orders_table",
            "007_create_order_script_groups_table",
            "008_create_order_parameters_table",
            "009_create_provisioned_databases_table",
        };

        foreach (var expected in expectedMigrations)
        {
            migrationResources.Should().Contain(r => r.Contains(expected),
                $"PostgreSQL migration '{expected}' should be embedded as a resource");
        }

        var resourceName = migrationResources.First(r => r.Contains("001_create_connections_table"));
        using var stream = cdcLibAssembly.GetManifestResourceStream(resourceName);
        stream.Should().NotBeNull();
        using var reader = new StreamReader(stream!);
        var sql = reader.ReadToEnd();

        sql.Should().Contain("CREATE TABLE");
        sql.Should().Contain("factory_connections");
    }

    [Fact]
    public void EmbeddedMigrationScript_SqlServer_ExistsAndContainsCreateTable()
    {
        var cdcLibAssembly = Assembly.Load("cdc-lib")!;

        var migrationResources = cdcLibAssembly.GetManifestResourceNames()
            .Where(r => FactorySchemaRunner.ScriptFilter(r, FactorySchemaRunner.SqlServerMigrationResourcePrefix))
            .ToList();

        migrationResources.Should().HaveCount(9);

        var expectedMigrations = new[]
        {
            "001_create_connections_table",
            "002_create_templates_table",
            "003_create_script_groups_table",
            "004_create_script_group_dependencies_table",
            "005_create_scripts_table",
            "006_create_orders_table",
            "007_create_order_script_groups_table",
            "008_create_order_parameters_table",
            "009_create_provisioned_databases_table",
        };

        foreach (var expected in expectedMigrations)
        {
            migrationResources.Should().Contain(r => r.Contains(expected),
                $"SQL Server migration '{expected}' should be embedded as a resource");
        }

        var resourceName = migrationResources.First(r => r.Contains("001_create_connections_table"));
        using var stream = cdcLibAssembly.GetManifestResourceStream(resourceName);
        stream.Should().NotBeNull();
        using var reader = new StreamReader(stream!);
        var sql = reader.ReadToEnd();

        sql.Should().Contain("CREATE TABLE");
        sql.Should().Contain("factory_connections");
        sql.Should().Contain("UNIQUEIDENTIFIER");
        sql.Should().Contain("NEWID()");
    }
}
