using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Softbase.Cdc.Utilities;

namespace Softbase.Cdc
{
    public class CdcCaptureResult
    {
        public IDictionary<string, IEnumerable<IDictionary<string, object>>> Data { get; set; } = new Dictionary<string, IEnumerable<IDictionary<string, object>>>();
        public List<string> Errors { get; set; } = new List<string>();
        public bool IsSuccess { get; set; }
    }

    public class CdcDataUtilities
    {

        public static void EnableCdcOnDatabase(SimpleDac dac)
        {
            const string cdcOn = "exec sys.sp_cdc_enable_db";
            var cdcOnResult = dac.ExecuteCommand(cdcOn);
        }

        public static void DisableCdcOnDatabase(SimpleDac dac)
        {
            const string cdcOff = "exec sys.sp_cdc_disable_db";
            var cdcOnResult = dac.ExecuteCommand(cdcOff);
        }

        /// <summary>
        /// Check if CDC is enabled on the database
        /// </summary>
        /// <param name="dac">Database connection</param>
        /// <returns>True if CDC is enabled, false otherwise</returns>
        public static bool IsCdcEnabled(SimpleDac dac)
        {
            try
            {
                const string sql = @"
                    SELECT is_cdc_enabled
                    FROM sys.databases
                    WHERE name = DB_NAME()";

                var result = dac.ExecuteScalar<bool>(sql);
                return result;
            }
            catch
            {
                // If the query fails, CDC is not enabled
                return false;
            }
        }

        public static IDictionary<string, IEnumerable<IDictionary<string, object>>> BuildProfile(SimpleDac dac, IEnumerable<SqlTable> tableResult, ILogger logger)
        {
            var allResults = new Dictionary<string, IEnumerable<IDictionary<string, object>>>();
            const string cdcTableTemplate = "[cdc].[{0}_{1}_CT]";
            const string cdcTableSelect = "select * from {0};";

            foreach (var table in tableResult)
            {
                var cdcTableName = string.Format(cdcTableTemplate, table.Schema, table.Name);
                var tableSelect = string.Format(cdcTableSelect, cdcTableName);

                try
                {
                    var tableResults = dac.ExecuteReader<IEnumerable<IDictionary<string, object>>>(tableSelect, (reader) =>
                    {
                        var models = new List<IDictionary<string, object>>();
                        while (reader.Read())
                        {
                            var model = new Dictionary<string, object>();

                            for (var i = 0; i < reader.FieldCount; i++)
                                model[reader.GetName(i)] = reader.GetValue(i);

                            models.Add(model);
                        }
                        return models;
                    });

                    if (tableResults.Count() > 0)
                        allResults[$"{table.Schema}_{table.Name}"] = tableResults;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Unable to turn CDC on for table {table.Schema}.{table.Name}");
                }
            }

            return allResults;
        }

        //private static void NetProfile(SimpleDac dac, ILogger logger)
        //{
        //    var tableResult = default(IEnumerable<SqlTable>);
        //    try
        //    {
        //        tableResult = GetTables(dac);
        //        var profile = BuildNetProfile(dac, tableResult, logger);
        //        File.WriteAllText("/Users/sakamoto/.cdc/netprofile.json", profile.ToJson(true));
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.LogError(ex, $"err");
        //        throw;
        //    }


        //}

        public static CdcCaptureResult BuildNetProfile(SimpleDac dac, IEnumerable<SqlTable> tableResult, ILogger logger)
        {
            var allResults = new Dictionary<string, IEnumerable<IDictionary<string, object>>>();
            var errors = new List<string>();

            foreach (var table in tableResult)
            {
                if (!table.HasPrimaryKey)
                    continue;

                // Skip if CDC is not enabled on this table
                if (string.IsNullOrEmpty(table.CdcCaptureInstanceName))
                {
                    logger.LogDebug("Skipping table {Schema}.{Table} - CDC not enabled", table.Schema, table.Name);
                    continue;
                }

                var tableSelect = GetChangesSqlFromTemplate(table);

                try
                {
                    var tableResults = dac.ExecuteReader<IEnumerable<IDictionary<string, object>>>(tableSelect, (reader) =>
                    {
                        var models = new List<IDictionary<string, object>>();
                        var changesByKey = new Dictionary<string, List<IDictionary<string, object>>>();

                        // First pass: collect all changes grouped by primary key
                        while (reader.Read())
                        {
                            var model = new Dictionary<string, object>();
                            for (var i = 0; i < reader.FieldCount; i++)
                                model[reader.GetName(i)] = reader.GetValue(i);

                            // Get primary key value(s) to group changes
                            var pkValue = GetPrimaryKeyValue(model, table);
                            if (!changesByKey.ContainsKey(pkValue))
                                changesByKey[pkValue] = new List<IDictionary<string, object>>();

                            changesByKey[pkValue].Add(model);
                        }

                        // Second pass: process changes to extract only changed fields with old/new values
                        foreach (var kvp in changesByKey)
                        {
                            // Sort by LSN (binary) - convert to byte array for comparison
                            var changes = kvp.Value.OrderBy(c =>
                            {
                                var lsn = c["__$start_lsn"];
                                if (lsn is byte[] bytes)
                                    return Convert.ToHexString(bytes);
                                return lsn?.ToString() ?? "";
                            }).ToList();

                            var processedChange = ProcessChangesForRecord(changes, table);
                            if (processedChange != null)
                                models.Add(processedChange);
                        }

                        return models;
                    });

                    if (tableResults.Count() > 0)
                        allResults[$"{table.Schema}_{table.Name}"] = tableResults;
                }
                catch (Exception ex)
                {
                    var errorMessage = $"Unable to retrieve net changes for {table.Schema}.{table.Name}: {ex.Message}";
                    logger.LogError(ex, errorMessage);
                    errors.Add(errorMessage);
                }
            }

            return new CdcCaptureResult
            {
                Data = allResults,
                Errors = errors,
                IsSuccess = errors.Count == 0
            };
        }

        private static string GetNetSqlFromTemplate(SqlTable table)
        {
            /*
             declare @min BINARY(10), @max BINARY(10);

             select @min = sys.fn_cdc_get_min_lsn('dbo_WO'), @max = sys.fn_cdc_get_max_lsn()
             print @min
             print @max

             select * from cdc.fn_cdc_get_net_changes_dbo_WO(@min, @max, 'all')
            */
            if (string.IsNullOrEmpty(table.CdcCaptureInstanceName))
            {
                throw new InvalidOperationException($"CDC capture instance name not found for table {table.Schema}.{table.Name}");
            }

            // Validate the capture instance name to prevent SQL injection
            var validatedCaptureInstance = SqlIdentifierValidator.ValidateIdentifier(table.CdcCaptureInstanceName, "capture instance");

            var sb = new StringBuilder();
            sb.AppendLine("declare @min BINARY(10), @max BINARY(10);");
            sb.AppendLine($"select @min = sys.fn_cdc_get_min_lsn('{validatedCaptureInstance}'), @max = sys.fn_cdc_get_max_lsn()");
            sb.AppendLine($"select * from cdc.fn_cdc_get_net_changes_{validatedCaptureInstance}(@min, @max, 'all')");

            return sb.ToString();
        }

        private static string GetChangesSqlFromTemplate(SqlTable table)
        {
            /*
             Gets all changes including old and new values for updates
             This allows us to identify exactly which fields changed
            */
            if (string.IsNullOrEmpty(table.CdcCaptureInstanceName))
            {
                throw new InvalidOperationException($"CDC capture instance name not found for table {table.Schema}.{table.Name}");
            }

            // Validate the capture instance name to prevent SQL injection
            var validatedCaptureInstance = SqlIdentifierValidator.ValidateIdentifier(table.CdcCaptureInstanceName, "capture instance");

            var sb = new StringBuilder();
            sb.AppendLine("declare @min BINARY(10), @max BINARY(10);");
            sb.AppendLine($"select @min = sys.fn_cdc_get_min_lsn('{validatedCaptureInstance}'), @max = sys.fn_cdc_get_max_lsn()");
            sb.AppendLine($"select * from cdc.fn_cdc_get_all_changes_{validatedCaptureInstance}(@min, @max, 'all update old')");

            return sb.ToString();
        }

        private static string GetPrimaryKeyValue(IDictionary<string, object> record, SqlTable table)
        {
            // For simplicity, we'll use the first primary key column
            // In a more robust implementation, we'd handle composite keys properly
            var pkIndex = table.Indexes.FirstOrDefault(i => i.IndexType.Contains("primary"));
            if (pkIndex == null)
                return "unknown";

            // Extract the first key column name from index_keys (format like "column1, column2")
            var keyColumn = pkIndex.IndexKeys.Split(',')[0].Trim();
            var pkValue = record.ContainsKey(keyColumn) ? record[keyColumn]?.ToString() : "null";

            return pkValue ?? "null";
        }

        private static IDictionary<string, object> ProcessChangesForRecord(List<IDictionary<string, object>> changes, SqlTable table)
        {
            if (changes.Count == 0)
                return null;

            var result = new Dictionary<string, object>();
            var lastChange = changes.Last();

            // Add metadata
            result["__$operation"] = lastChange["__$operation"];
            result["__$start_lsn"] = lastChange["__$start_lsn"];
            result["__$table"] = $"{table.Schema}.{table.Name}";

            // Get primary key for identification
            var pkIndex = table.Indexes.FirstOrDefault(i => i.IndexType.Contains("primary"));
            if (pkIndex != null)
            {
                var keyColumn = pkIndex.IndexKeys.Split(',')[0].Trim();
                if (lastChange.ContainsKey(keyColumn))
                    result["__$primary_key"] = lastChange[keyColumn];
            }

            var operation = Convert.ToInt32(lastChange["__$operation"]);

            if (operation == 1) // Insert
            {
                // For inserts, capture all non-null values
                foreach (var kvp in lastChange)
                {
                    if (!kvp.Key.StartsWith("__$") && kvp.Value != null && kvp.Value != DBNull.Value)
                    {
                        result[$"new_{kvp.Key}"] = kvp.Value;
                    }
                }
            }
            else if (operation == 2) // Delete
            {
                // For deletes, capture the deleted values
                foreach (var kvp in lastChange)
                {
                    if (!kvp.Key.StartsWith("__$") && kvp.Value != null && kvp.Value != DBNull.Value)
                    {
                        result[$"old_{kvp.Key}"] = kvp.Value;
                    }
                }
            }
            else if (operation == 3 || operation == 4) // Update (before/after)
            {
                // For updates, we need to find the before and after records
                var beforeRecord = changes.FirstOrDefault(c => Convert.ToInt32(c["__$operation"]) == 3);
                var afterRecord = changes.FirstOrDefault(c => Convert.ToInt32(c["__$operation"]) == 4);

                if (beforeRecord != null && afterRecord != null)
                {
                    // Compare fields to find what actually changed
                    foreach (var key in afterRecord.Keys)
                    {
                        if (key.StartsWith("__$"))
                            continue;

                        var oldValue = beforeRecord.ContainsKey(key) ? beforeRecord[key] : null;
                        var newValue = afterRecord[key];

                        // Check if the value actually changed
                        if (!AreValuesEqual(oldValue, newValue))
                        {
                            result[$"old_{key}"] = oldValue;
                            result[$"new_{key}"] = newValue;
                        }
                    }
                }
            }

            // Only return the record if it has actual data changes
            return result.Keys.Any(k => k.StartsWith("old_") || k.StartsWith("new_")) ? result : null;
        }

        private static bool AreValuesEqual(object value1, object value2)
        {
            if (value1 == null && value2 == null)
                return true;
            if (value1 == null || value2 == null)
                return false;
            if (value1 == DBNull.Value && value2 == DBNull.Value)
                return true;
            if (value1 == DBNull.Value || value2 == DBNull.Value)
                return false;

            return value1.Equals(value2);
        }

        public static void EnableTableCdc(SimpleDac dac, IEnumerable<SqlTable> tableResult, ILogger logger)
        {
            foreach (var table in tableResult)
            {
                if (table.Indexes.Count() > 0)
                {
                    // Validate identifiers to prevent SQL injection
                    var validatedSchema = SqlIdentifierValidator.ValidateIdentifier(table.Schema, "schema");
                    var validatedTableName = SqlIdentifierValidator.ValidateIdentifier(table.Name, "table name");

                    var index = table.Indexes.FirstOrDefault(i => i.IndexType.Contains("primary"));
                    var validatedIndexName = index?.IndexName != null
                        ? SqlIdentifierValidator.ValidateIdentifier(index.IndexName, "index name")
                        : null;

                    // Build SQL command with proper null handling for optional parameters
                    string enableTableCdc;
                    if (validatedIndexName != null)
                    {
                        enableTableCdc = $"EXEC sys.sp_cdc_enable_table @source_schema = '{validatedSchema}', @source_name = '{validatedTableName}', @role_name = null, @supports_net_changes = 1, @index_name = '{validatedIndexName}';";
                    }
                    else
                    {
                        enableTableCdc = $"EXEC sys.sp_cdc_enable_table @source_schema = '{validatedSchema}', @source_name = '{validatedTableName}', @role_name = null, @supports_net_changes = 1, @index_name = null;";
                    }

                    // Retry logic for transient errors like "Resource temporarily unavailable"
                    const int maxRetries = 3;
                    var retryDelays = new[] { 1000, 2000, 4000 }; // Exponential backoff in milliseconds

                    for (int attempt = 0; attempt <= maxRetries; attempt++)
                    {
                        try
                        {
                            logger.LogDebug($"enabling cdc for {validatedSchema}.{validatedTableName}, index: {validatedIndexName} (attempt {attempt + 1}/{maxRetries + 1})");
                            var enableTableResult = dac.ExecuteCommand(enableTableCdc);
                            logger.LogDebug($"Successfully enabled CDC for {validatedSchema}.{validatedTableName}");
                            break; // Success, exit retry loop
                        }
                        catch (Exception ex)
                        {
                            var isLastAttempt = attempt == maxRetries;
                            var isTransientError = ex.Message.Contains("Resource temporarily unavailable") ||
                                                  ex.Message.Contains("timeout") ||
                                                  ex.Message.Contains("deadlock");

                            if (isTransientError && !isLastAttempt)
                            {
                                var delay = retryDelays[attempt];
                                logger.LogWarning($"Transient error enabling CDC on {table.Schema}.{table.Name} (attempt {attempt + 1}): {ex.Message}. Retrying in {delay}ms...");
                                Thread.Sleep(delay);
                            }
                            else
                            {
                                logger.LogError(ex, $"Unable to turn CDC on for table {table.Schema}.{table.Name} after {attempt + 1} attempts");
                                throw; // Re-throw on last attempt or non-transient error
                            }
                        }
                    }

                    // Add a small delay between tables to reduce lock contention
                    Thread.Sleep(100);
                }
            }
        }

        public static IEnumerable<SqlTable> GetTables(SimpleDac dac)
        {
            const string tableSelect = "select * from INFORMATION_SCHEMA.TABLES where table_type = 'BASE TABLE' and table_schema <> 'cdc';";
            var allTables = dac.ExecuteReader<IEnumerable<SqlTable>>(tableSelect, (reader) =>
            {
                var tables = new List<SqlTable>();
                while (reader.Read())
                {
                    var tableName = reader.TryReadField<string>("TABLE_NAME");
                    if ("systranschemas,lsn_time_mapping,ddl_history,change_tables,captured_columns,index_columns".IndexOf(tableName) == -1)
                    {
                        tables.Add(new SqlTable(reader.TryReadField<string>("TABLE_CATALOG"),
                                    reader.TryReadField<string>("TABLE_SCHEMA"), reader.TryReadField<string>("TABLE_NAME")));
                    }
                }

                return tables;
            });

            foreach (var table in allTables)
            {
                table.Indexes = GetIndexes(dac, table.Schema, table.Name);
                table.CdcCaptureInstanceName = GetCdcCaptureInstanceName(dac, table.Schema, table.Name);
            }

            return allTables;
        }

        public static string? GetCdcCaptureInstanceName(SimpleDac dac, string schema, string tableName)
        {
            try
            {
                // Query the cdc.change_tables system table to get the actual capture instance name
                const string sql = @"
                    SELECT capture_instance
                    FROM cdc.change_tables
                    WHERE source_object_id = OBJECT_ID(@tableName)";

                var parameters = new Dictionary<string, object>
                {
                    ["tableName"] = $"{schema}.{tableName}"
                };

                return dac.ExecuteScalar<string>(sql, parameters);
            }
            catch
            {
                // If CDC is not enabled or table not found, return null
                return null;
            }
        }

        public static IEnumerable<SqlIndex> GetIndexes(SimpleDac dac, string schema, string tableName)
        {
            // Escape identifiers for safe SQL execution
            // Note: Table names come from INFORMATION_SCHEMA and are trusted, but may contain
            // special characters like spaces or $ that require bracketing in SQL Server
            var escapedSchema = SqlIdentifierValidator.EscapeIdentifier(schema);
            var escapedTableName = SqlIdentifierValidator.EscapeIdentifier(tableName);

            // sp_helpindex expects a string parameter like 'schema.table' or '[schema].[table]'
            // We need to escape any single quotes in the bracketed identifiers for the SQL string
            var tableIdentifier = $"{escapedSchema}.{escapedTableName}";
            var escapedTableIdentifier = tableIdentifier.Replace("'", "''");

            var tableSelect = $"EXEC sp_helpindex '{escapedTableIdentifier}';";
            return dac.ExecuteReader<IEnumerable<SqlIndex>>(tableSelect, (reader) =>
            {
                var models = new List<SqlIndex>();
                while (reader.Read())
                {
                    var indexDesc = reader.TryReadField<string>("index_description");

                    models.Add(new SqlIndex(reader.TryReadField<string>("index_name"),
                                indexDesc, reader.TryReadField<string>("index_keys")));
                }

                return models;
            });
        }
    }
}

