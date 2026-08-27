using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Softbase.Cdc.Factory.Models;

namespace Softbase.Cdc.Factory.Interfaces;

/// <summary>
/// Platform-specific provider for database lifecycle operations.
/// Implementations: SqlServerDatabaseProvider, (future) PostgreSqlDatabaseProvider, etc.
/// </summary>
public interface IDatabaseProvider
{
    /// <summary>
    /// Restores a database from a backup file to a new database name.
    /// </summary>
    Task<SqlResult> RestoreBackupAsync(string backupFilePath, string databaseName, string connectionString);

    /// <summary>
    /// Creates a new empty database.
    /// </summary>
    Task<SqlResult> CreateDatabaseAsync(string databaseName, string connectionString);

    /// <summary>
    /// Drops an existing database.
    /// </summary>
    Task<SqlResult> DropDatabaseAsync(string databaseName, string connectionString);

    /// <summary>
    /// Tests connectivity to the server specified by the connection string.
    /// </summary>
    Task<bool> TestConnectionAsync(string connectionString);

    /// <summary>
    /// Executes a SQL script (batch of statements) against the database.
    /// </summary>
    Task<SqlResult> ExecuteSqlAsync(string connectionString, string sql, IReadOnlyDictionary<string, object?>? parameters = null);
}
