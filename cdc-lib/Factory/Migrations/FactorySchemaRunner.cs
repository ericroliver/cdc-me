using System;
using System.Linq;
using System.Reflection;
using DbUp;
using DbUp.Builder;
using Microsoft.Extensions.Logging;
using Softbase.Cdc.Data;

namespace Softbase.Cdc.Factory;

/// <summary>
/// Runs DbUp database migrations for the Factory schema against the DTAI metadata database.
/// Embedded SQL migration scripts are applied in order, with DbUp tracking applied versions
/// in a <c>SchemaVersions</c> table to ensure idempotent execution on startup.
/// Supports both PostgreSQL and SQL Server providers.
/// </summary>
public class FactorySchemaRunner : IFactorySchemaRunner
{
    private readonly string _connectionString;
    private readonly DatabaseProvider _provider;
    private readonly ILogger<FactorySchemaRunner> _logger;

    /// <summary>
    /// The embedded-resource prefix used to filter PostgreSQL Factory migration SQL scripts.
    /// Manifest resource names follow the pattern:
    ///   <c>cdc_lib.Factory.Migrations.Factory.&lt;NNN&gt;_&lt;description&gt;.sql</c>
    /// </summary>
    internal const string PostgresMigrationResourcePrefix = "cdc_lib.Factory.Migrations.Factory.";

    /// <summary>
    /// The embedded-resource prefix used to filter SQL Server Factory migration SQL scripts.
    /// Manifest resource names follow the pattern:
    ///   <c>cdc_lib.Factory.Migrations.Factory.SqlServer.&lt;NNN&gt;_&lt;description&gt;.sql</c>
    /// </summary>
    internal const string SqlServerMigrationResourcePrefix = "cdc_lib.Factory.Migrations.Factory.SqlServer.";

    /// <summary>
    /// Creates a new <see cref="FactorySchemaRunner"/>.
    /// </summary>
    /// <param name="connectionString">
    /// The connection string for the DTAI metadata database.
    /// </param>
    /// <param name="provider">The database provider (PostgreSQL or SQL Server).</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public FactorySchemaRunner(string connectionString, DatabaseProvider provider, ILogger<FactorySchemaRunner> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _provider = provider;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public bool RunMigrations()
    {
        _logger.LogInformation("Starting Factory schema migration against {Provider}", _provider);

        var resourcePrefix = GetResourcePrefix();
        var upgradeBuilder = CreateUpgradeBuilder();

        var upgrader = upgradeBuilder
            .WithScriptsEmbeddedInAssembly(
                Assembly.GetExecutingAssembly(),
                scriptPath => ScriptFilter(scriptPath, resourcePrefix))
            .WithTransaction()
            .LogTo(_logger)
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            _logger.LogError(result.Error, "Factory schema migration failed");
            return false;
        }

        _logger.LogInformation(
            "Factory schema migration completed successfully. Applied {Count} script(s).",
            result.Scripts.Count());
        return true;
    }

    private string GetResourcePrefix() =>
        _provider switch
        {
            DatabaseProvider.PostgreSQL => PostgresMigrationResourcePrefix,
            DatabaseProvider.SqlServer => SqlServerMigrationResourcePrefix,
            _ => throw new NotSupportedException($"Provider {_provider} is not supported for factory migrations.")
        };

    private UpgradeEngineBuilder CreateUpgradeBuilder() =>
        _provider switch
        {
            DatabaseProvider.PostgreSQL => DeployChanges.To.PostgresqlDatabase(_connectionString),
            DatabaseProvider.SqlServer => DeployChanges.To.SqlDatabase(_connectionString),
            _ => throw new NotSupportedException($"Provider {_provider} is not supported for factory migrations.")
        };

    /// <summary>
    /// Filter predicate that selects only Factory migration SQL scripts matching the given
    /// resource prefix from the embedded resource manifest. When filtering for the PostgreSQL
    /// prefix, SQL Server scripts (which share a longer prefix) are excluded.
    /// Exposed internally for unit testing.
    /// </summary>
    internal static bool ScriptFilter(string scriptPath, string resourcePrefix)
    {
        if (!scriptPath.StartsWith(resourcePrefix, StringComparison.Ordinal)
            || !scriptPath.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            return false;

        // PostgreSQL prefix is a substring of the SQL Server prefix, so exclude
        // SQL Server scripts when filtering for PostgreSQL.
        if (resourcePrefix == PostgresMigrationResourcePrefix
            && scriptPath.StartsWith(SqlServerMigrationResourcePrefix, StringComparison.Ordinal))
            return false;

        return true;
    }
}
