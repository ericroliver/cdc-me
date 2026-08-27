using System;
using System.Linq;
using System.Reflection;
using DbUp;
using Microsoft.Extensions.Logging;

namespace Softbase.Cdc.Factory;

/// <summary>
/// Runs DbUp database migrations for the Factory schema against the DTAI PostgreSQL database.
/// Embedded SQL migration scripts are applied in order, with DbUp tracking applied versions
/// in a <c>SchemaVersions</c> table to ensure idempotent execution on startup.
/// </summary>
public class FactorySchemaRunner : IFactorySchemaRunner
{
    private readonly string _connectionString;
    private readonly ILogger<FactorySchemaRunner> _logger;

    /// <summary>
    /// The embedded-resource prefix used to filter Factory migration SQL scripts.
    /// Manifest resource names follow the pattern:
    ///   <c>cdc_lib.Factory.Migrations.Factory.&lt;NNN&gt;_&lt;description&gt;.sql</c>
    /// </summary>
    internal const string MigrationResourcePrefix = "cdc_lib.Factory.Migrations.Factory.";

    /// <summary>
    /// Creates a new <see cref="FactorySchemaRunner"/>.
    /// </summary>
    /// <param name="connectionString">
    /// The PostgreSQL connection string for the DTAI metadata database.
    /// </param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public FactorySchemaRunner(string connectionString, ILogger<FactorySchemaRunner> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public bool RunMigrations()
    {
        _logger.LogInformation("Starting Factory schema migration against PostgreSQL");

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(_connectionString)
            .WithScriptsEmbeddedInAssembly(
                Assembly.GetExecutingAssembly(),
                ScriptFilter)
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

    /// <summary>
    /// Filter predicate that selects only Factory migration SQL scripts from the
    /// embedded resource manifest. Exposed internally for unit testing.
    /// </summary>
    internal static bool ScriptFilter(string scriptPath)
    {
        return scriptPath.StartsWith(MigrationResourcePrefix, StringComparison.Ordinal)
               && scriptPath.EndsWith(".sql", StringComparison.OrdinalIgnoreCase);
    }
}
