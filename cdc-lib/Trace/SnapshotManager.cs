using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Softbase.Cdc.Models;
using Softbase.Cdc.Utilities;

namespace Softbase.Cdc.Trace
{
    public class SnapshotManager : ISnapshotManager
    {
        private readonly SimpleDac _dac;
        private readonly ILogger _logger;

        public SnapshotManager(SimpleDac dac, ILogger logger)
        {
            _dac = dac ?? throw new ArgumentNullException(nameof(dac));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SnapshotResult> CreateSnapshotAsync(string databaseName, string snapshotName)
        {
            _logger.LogInformation("Creating snapshot {SnapshotName} for database {DatabaseName}", snapshotName, databaseName);

            try
            {
                // Validate identifiers to prevent SQL injection
                var validatedDatabaseName = SqlIdentifierValidator.ValidateIdentifier(databaseName, "database name");
                var validatedSnapshotName = SqlIdentifierValidator.ValidateIdentifier(snapshotName, "snapshot name");

                // Check if snapshot already exists
                if (await SnapshotExistsAsync(validatedSnapshotName))
                {
                    return new SnapshotResult
                    {
                        Success = false,
                        Message = $"Snapshot '{validatedSnapshotName}' already exists. Only one snapshot is allowed.",
                        SnapshotName = validatedSnapshotName
                    };
                }

                // Get database file paths
                var dataFiles = await GetDatabaseFilesAsync(validatedDatabaseName);

                // Build CREATE DATABASE AS SNAPSHOT statement
                var snapshotFiles = new List<string>();
                foreach (var file in dataFiles)
                {
                    var snapshotFileName = $"{file.LogicalName}_snapshot.ss";
                    var snapshotFilePath = Path.Combine(Path.GetDirectoryName(file.PhysicalName) ?? "", snapshotFileName);
                    snapshotFiles.Add($"(NAME = '{file.LogicalName}', FILENAME = '{snapshotFilePath}')");
                }

                var createSnapshotSql = $@"
                    CREATE DATABASE {SqlIdentifierValidator.EscapeIdentifier(validatedSnapshotName)} ON
                    {string.Join(",\n", snapshotFiles)}
                    AS SNAPSHOT OF {SqlIdentifierValidator.EscapeIdentifier(validatedDatabaseName)};";

                await _dac.ExecuteCommandAsync(createSnapshotSql);
                _logger.LogInformation("Successfully created snapshot {SnapshotName}", validatedSnapshotName);

                return new SnapshotResult
                {
                    Success = true,
                    Message = "Snapshot created successfully",
                    SnapshotName = validatedSnapshotName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create snapshot {SnapshotName}", snapshotName);
                return new SnapshotResult
                {
                    Success = false,
                    Message = $"Failed to create snapshot: {ex.Message}",
                    SnapshotName = snapshotName,
                    ErrorDetails = ex.ToString()
                };
            }
        }

        public async Task<SnapshotResult> RestoreSnapshotAsync(string snapshotName, string targetDatabaseName)
        {
            _logger.LogInformation("Restoring snapshot {SnapshotName} to database {TargetDatabase}", snapshotName, targetDatabaseName);

            try
            {
                // Validate identifiers to prevent SQL injection
                var validatedSnapshotName = SqlIdentifierValidator.ValidateIdentifier(snapshotName, "snapshot name");
                var validatedTargetDatabaseName = SqlIdentifierValidator.ValidateIdentifier(targetDatabaseName, "target database name");

                // Check if snapshot exists
                if (!await SnapshotExistsAsync(validatedSnapshotName))
                {
                    return new SnapshotResult
                    {
                        Success = false,
                        Message = $"Snapshot '{validatedSnapshotName}' does not exist",
                        SnapshotName = validatedSnapshotName
                    };
                }

                // Check if target database exists using parameterized query
                const string databaseExistsSql = "SELECT COUNT(1) FROM sys.databases WHERE name = @databaseName";
                var databaseExists = await _dac.ExecuteScalarAsync<int>(databaseExistsSql, new Dictionary<string, object>
                {
                    ["@databaseName"] = validatedTargetDatabaseName
                }) > 0;

                if (!databaseExists)
                {
                    return new SnapshotResult
                    {
                        Success = false,
                        Message = $"Target database '{validatedTargetDatabaseName}' does not exist. Cannot restore snapshot to non-existent database.",
                        SnapshotName = validatedSnapshotName
                    };
                }

                // Set database to single user mode
                var setSingleUserSql = $"ALTER DATABASE {SqlIdentifierValidator.EscapeIdentifier(validatedTargetDatabaseName)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;";

                // Restore from snapshot
                var restoreSql = $"use master;RESTORE DATABASE {SqlIdentifierValidator.EscapeIdentifier(validatedTargetDatabaseName)} FROM DATABASE_SNAPSHOT = {SqlIdentifierValidator.EscapeIdentifier(validatedSnapshotName)};";

                // Set back to multi user mode
                var setMultiUserSql = $"ALTER DATABASE {SqlIdentifierValidator.EscapeIdentifier(validatedTargetDatabaseName)} SET MULTI_USER;";

                try
                {
                    await _dac.ExecuteCommandAsync(setSingleUserSql);
                    await _dac.ExecuteCommandAsync(restoreSql);
                    await _dac.ExecuteCommandAsync(setMultiUserSql);
                }
                catch (Exception)
                {
                    // Try to set back to multi user mode if restore failed
                    try
                    {
                        await _dac.ExecuteCommandAsync(setMultiUserSql);
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogError(cleanupEx, "Failed to reset database to multi-user mode after restore failure");
                    }
                    throw;
                }
                _logger.LogInformation("Successfully restored snapshot {SnapshotName} to {TargetDatabase}", validatedSnapshotName, validatedTargetDatabaseName);

                return new SnapshotResult
                {
                    Success = true,
                    Message = "Snapshot restored successfully",
                    SnapshotName = validatedSnapshotName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore snapshot {SnapshotName}", snapshotName);
                return new SnapshotResult
                {
                    Success = false,
                    Message = $"Failed to restore snapshot: {ex.Message}",
                    SnapshotName = snapshotName,
                    ErrorDetails = ex.ToString()
                };
            }
        }

        public async Task<bool> SnapshotExistsAsync(string snapshotName)
        {
            const string checkSnapshotSql = @"
                SELECT COUNT(1)
                FROM sys.databases
                WHERE name = @snapshotName AND source_database_id IS NOT NULL";

            var count = await _dac.ExecuteScalarAsync<int>(checkSnapshotSql, new Dictionary<string, object>
            {
                ["@snapshotName"] = snapshotName
            });

            return count > 0;
        }

        public async Task RestoreFromSnapshotAsync(string databaseName, string snapshotName)
        {
            // Delegate to RestoreSnapshotAsync and throw on failure for backward compatibility
            var result = await RestoreSnapshotAsync(snapshotName, databaseName);

            if (!result.Success)
            {
                throw new InvalidOperationException(result.Message);
            }
        }

        public async Task<SnapshotResult> DropSnapshotAsync(string snapshotName)
        {
            _logger.LogInformation("Dropping snapshot {SnapshotName}", snapshotName);

            // Validate identifier to prevent SQL injection
            var validatedSnapshotName = SqlIdentifierValidator.ValidateIdentifier(snapshotName, "snapshot name");

            if (!await SnapshotExistsAsync(validatedSnapshotName))
            {
                _logger.LogWarning("Snapshot {SnapshotName} does not exist, nothing to drop", validatedSnapshotName);
                return new SnapshotResult
                {
                    Success = false,
                    Message = $"Snapshot {validatedSnapshotName} does not exist, nothing to drop",
                    SnapshotName = validatedSnapshotName
                };
            }

            var dropSnapshotSql = $"DROP DATABASE {SqlIdentifierValidator.EscapeIdentifier(validatedSnapshotName)};";

            try
            {
                await _dac.ExecuteCommandAsync(dropSnapshotSql);
                _logger.LogInformation("Successfully dropped snapshot {SnapshotName}", validatedSnapshotName);

                return new SnapshotResult
                {
                    Success = true,
                    Message = $"Successfully dropped snapshot {validatedSnapshotName}",
                    SnapshotName = validatedSnapshotName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to drop snapshot {SnapshotName}", snapshotName);
                return new SnapshotResult
                {
                    Success = false,
                    Message = $"Failed to drop snapshot: {ex.Message}",
                    SnapshotName = snapshotName,
                    ErrorDetails = ex.ToString()
                };
            }
        }

        public async Task<SnapshotInfo> GetSnapshotInfoAsync(string snapshotName)
        {
            const string getSnapshotInfoSql = @"
                SELECT
                    d.name AS SnapshotName,
                    sd.name AS SourceDatabase,
                    d.create_date AS CreatedTime,
                    ISNULL(SUM(mf.size * 8 * 1024), 0) AS SizeInBytes,
                    d.state_desc AS Status
                FROM sys.databases d
                LEFT JOIN sys.databases sd ON d.source_database_id = sd.database_id
                LEFT JOIN sys.master_files mf ON d.database_id = mf.database_id
                WHERE d.name = @snapshotName AND d.source_database_id IS NOT NULL
                GROUP BY d.name, sd.name, d.create_date, d.state_desc";

            return await _dac.ExecuteReaderAsync(getSnapshotInfoSql, reader =>
            {
                if (reader.Read())
                {
                    return new SnapshotInfo
                    {
                        SnapshotName = reader.GetString(0),
                        SourceDatabase = reader.GetString(1),
                        CreatedTime = reader.GetDateTime(2),
                        SizeInBytes = reader.GetInt64(3),
                        Status = reader.GetString(4)
                    };
                }
                throw new InvalidOperationException($"Snapshot '{snapshotName}' not found.");
            }, new Dictionary<string, object>
            {
                ["@snapshotName"] = snapshotName
            });
        }

        public async Task<List<SnapshotInfo>> ListSnapshotsAsync(string databaseName)
        {
            // For API compatibility - filter by database name if provided
            var allSnapshots = await ListSnapshotsAsync();
            if (string.IsNullOrEmpty(databaseName))
                return allSnapshots;

            return allSnapshots.Where(s => s.SourceDatabase.Equals(databaseName, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public async Task<List<SnapshotInfo>> ListSnapshotsAsync()
        {
            const string listSnapshotsSql = @"
                SELECT
                    d.name AS SnapshotName,
                    sd.name AS SourceDatabase,
                    d.create_date AS CreatedTime,
                    CAST(ISNULL(SUM(mf.size * 8 * 1024), 0) AS BIGINT) AS SizeInBytes,
                    d.state_desc AS Status
                FROM sys.databases d
                LEFT JOIN sys.databases sd ON d.source_database_id = sd.database_id
                LEFT JOIN sys.master_files mf ON d.database_id = mf.database_id
                WHERE d.source_database_id IS NOT NULL
                GROUP BY d.name, sd.name, d.create_date, d.state_desc
                ORDER BY d.create_date DESC";

            return await _dac.ExecuteReaderAsync(listSnapshotsSql, reader =>
            {
                var snapshots = new List<SnapshotInfo>();
                while (reader.Read())
                {
                    snapshots.Add(new SnapshotInfo
                    {
                        SnapshotName = reader.GetString(0),
                        SourceDatabase = reader.GetString(1),
                        CreatedTime = reader.GetDateTime(2),
                        SizeInBytes = reader.GetInt64(3),
                        Status = reader.GetString(4)
                    });
                }
                return snapshots;
            });
        }

        private async Task<List<DatabaseFileInfo>> GetDatabaseFilesAsync(string databaseName)
        {
            const string getFilesSql = @"
                SELECT 
                    name AS LogicalName,
                    physical_name AS PhysicalName,
                    type_desc AS FileType
                FROM sys.master_files 
                WHERE database_id = DB_ID(@databaseName) AND type = 0"; // Only data files for snapshots

            return await _dac.ExecuteReaderAsync(getFilesSql, reader =>
            {
                var files = new List<DatabaseFileInfo>();
                while (reader.Read())
                {
                    files.Add(new DatabaseFileInfo
                    {
                        LogicalName = reader.GetString(0),
                        PhysicalName = reader.GetString(1),
                        FileType = reader.GetString(2)
                    });
                }
                return files;
            }, new Dictionary<string, object>
            {
                ["@databaseName"] = databaseName
            });
        }
    }
}
