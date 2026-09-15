using Softbase.Cdc.Data;

namespace Softbase.Cdc.Factory;

/// <summary>
/// Runs DbUp database migrations for the Factory schema.
/// Called on application startup to ensure all factory tables are up to date.
/// </summary>
public interface IFactorySchemaRunner
{
    /// <summary>
    /// Executes any pending embedded SQL migrations against the DTAI metadata database.
    /// </summary>
    /// <returns><c>true</c> if all migrations applied successfully; <c>false</c> on failure.</returns>
    bool RunMigrations();
}
