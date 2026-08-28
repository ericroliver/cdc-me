using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;

namespace Softbase.Cdc.Factory.Providers;

/// <summary>
/// SQL Server implementation of <see cref="IDatabaseProvider"/>.
/// Uses ADO.NET (<see cref="SqlConnection"/>) for all database lifecycle operations.
/// </summary>
public class SqlServerDatabaseProvider : IDatabaseProvider
{
    private readonly ILogger<SqlServerDatabaseProvider> _logger;

    public SqlServerDatabaseProvider(ILogger<SqlServerDatabaseProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SqlResult> RestoreBackupAsync(
        string backupFilePath,
        string databaseName,
        string connectionString)
    {
        if (string.IsNullOrWhiteSpace(backupFilePath))
            throw new ArgumentException("Backup file path is required", nameof(backupFilePath));
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name is required", nameof(databaseName));
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required", nameof(connectionString));

        try
        {
            // Step 1: Get logical file names from the backup
            var (logicalDataName, logicalLogName) = await GetLogicalFileNamesAsync(backupFilePath, connectionString);

            // Step 2: Build RESTORE DATABASE with MOVE clauses
            var escapedDbName = EscapeSqlIdentifier(databaseName);
            var dataFileName = $"{databaseName}_data.mdf";
            var logFileName = $"{databaseName}_log.ldf";

            var restoreSql = $@"
                RESTORE DATABASE [{escapedDbName}] 
                FROM DISK = '{EscapeSqlLiteral(backupFilePath)}'
                WITH 
                    MOVE '{logicalDataName}' TO '/var/opt/mssql/data/{dataFileName}',
                    MOVE '{logicalLogName}' TO '/var/opt/mssql/data/{logFileName}',
                    REPLACE,
                    STATS = 10";

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(restoreSql, connection);
            command.CommandTimeout = 300; // 5 minutes for large backups
            await command.ExecuteNonQueryAsync();

            _logger.LogInformation(
                "Restored database '{Db}' from '{Backup}'", databaseName, backupFilePath);
            return SqlResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore database '{Db}' from '{Backup}'", databaseName, backupFilePath);
            return SqlResult.Fail(ex.Message);
        }
    }

    public async Task<SqlResult> CreateDatabaseAsync(string databaseName, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name is required", nameof(databaseName));
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required", nameof(connectionString));

        try
        {
            var sql = $"CREATE DATABASE [{EscapeSqlIdentifier(databaseName)}]";

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();

            _logger.LogInformation("Created database '{Db}'", databaseName);
            return SqlResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create database '{Db}'", databaseName);
            return SqlResult.Fail(ex.Message);
        }
    }

    public async Task<SqlResult> DropDatabaseAsync(string databaseName, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name is required", nameof(databaseName));
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required", nameof(connectionString));

        try
        {
            // Set database to single-user mode to kick out existing connections
            var setSingleUserSql = $@"
                ALTER DATABASE [{EscapeSqlIdentifier(databaseName)}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE";

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await using var setSingleCommand = new SqlCommand(setSingleUserSql, connection);
            setSingleCommand.CommandTimeout = 30;
            try
            {
                await setSingleCommand.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (ex.Number == 6115 || ex.Number == 5060)
            {
                // Database may not exist or already in single user mode — continue
                _logger.LogWarning("Could not set single-user mode for '{Db}': {Msg}", databaseName, ex.Message);
            }

            var dropSql = $"DROP DATABASE [{EscapeSqlIdentifier(databaseName)}]";
            await using var dropCommand = new SqlCommand(dropSql, connection);
            await dropCommand.ExecuteNonQueryAsync();

            _logger.LogInformation("Dropped database '{Db}'", databaseName);
            return SqlResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to drop database '{Db}'", databaseName);
            return SqlResult.Fail(ex.Message);
        }
    }

    public async Task<bool> TestConnectionAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required", nameof(connectionString));

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            return connection.State == ConnectionState.Open;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SQL Server connection test failed");
            return false;
        }
    }

    public async Task<SqlResult> ExecuteSqlAsync(
        string connectionString,
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required", nameof(connectionString));
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL is required", nameof(sql));

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(sql, connection);
            command.CommandTimeout = 120;

            if (parameters != null)
            {
                foreach (var (key, value) in parameters)
                {
                    command.Parameters.AddWithValue(key, value ?? DBNull.Value);
                }
            }

            var rowsAffected = await command.ExecuteNonQueryAsync();

            _logger.LogDebug("Executed SQL, {Rows} rows affected", rowsAffected);
            return SqlResult.Ok(rowsAffected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute SQL");
            return SqlResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Retrieves the logical file names (data + log) from a SQL Server backup file
    /// using RESTORE FILELISTONLY.
    /// </summary>
    internal async Task<(string DataName, string LogName)> GetLogicalFileNamesAsync(
        string backupFilePath,
        string connectionString)
    {
        var filelistSql = $"RESTORE FILELISTONLY FROM DISK = '{EscapeSqlLiteral(backupFilePath)}'";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(filelistSql, connection);
        command.CommandTimeout = 120;

        await using var reader = await command.ExecuteReaderAsync();

        string? dataName = null;
        string? logName = null;

        while (await reader.ReadAsync())
        {
            var logicalName = reader.GetString(reader.GetOrdinal("LogicalName"));
            var type = reader.GetString(reader.GetOrdinal("Type"));

            if (type == "D" && dataName == null)
                dataName = logicalName;
            else if (type == "L" && logName == null)
                logName = logicalName;
        }

        if (string.IsNullOrEmpty(dataName))
            throw new InvalidOperationException(
                $"No data file found in backup '{backupFilePath}'");

        if (string.IsNullOrEmpty(logName))
            throw new InvalidOperationException(
                $"No log file found in backup '{backupFilePath}'");

        return (dataName!, logName!);
    }

    /// <summary>
    /// Escapes single quotes in a SQL literal to prevent injection in
    /// file path parameters used in RESTORE statements.
    /// </summary>
    internal static string EscapeSqlLiteral(string input) => input.Replace("'", "''");

    /// <summary>
    /// Escapes a SQL Server identifier (e.g., database name) for use inside
    /// bracket-quoted identifiers. In SQL Server, ']' inside '[...]' is
    /// escaped by doubling it: '[' becomes '[[' and ']' becomes ']]'.
    /// </summary>
    internal static string EscapeSqlIdentifier(string identifier) => identifier.Replace("]", "]]");
}
