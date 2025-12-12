using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Text;
using System.Linq;

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

                var tableSelect = GetChangesSqlFromTemplate(table.Schema, table.Name);

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

        private static string GetNetSqlFromTemplate(string schema, string tableName)
        {
            /*
             declare @min BINARY(10), @max BINARY(10);

             select @min = sys.fn_cdc_get_min_lsn('dbo_WO'), @max = sys.fn_cdc_get_max_lsn()
             print @min
             print @max

             select * from cdc.fn_cdc_get_net_changes_dbo_WO(@min, @max, 'all')
            */
            var sb = new StringBuilder();
            sb.AppendLine("declare @min BINARY(10), @max BINARY(10);");
            sb.AppendLine($"select @min = sys.fn_cdc_get_min_lsn('{schema}_{tableName}'), @max = sys.fn_cdc_get_max_lsn()");
            sb.AppendLine($"select * from cdc.fn_cdc_get_net_changes_{schema}_{tableName}(@min, @max, 'all')");

            return sb.ToString();
        }

        private static string GetChangesSqlFromTemplate(string schema, string tableName)
        {
            /*
             Gets all changes including old and new values for updates
             This allows us to identify exactly which fields changed
            */
            var sb = new StringBuilder();
            sb.AppendLine("declare @min BINARY(10), @max BINARY(10);");
            sb.AppendLine($"select @min = sys.fn_cdc_get_min_lsn('{schema}_{tableName}'), @max = sys.fn_cdc_get_max_lsn()");
            sb.AppendLine($"select * from cdc.fn_cdc_get_all_changes_{schema}_{tableName}(@min, @max, 'all update old')");

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
            var tableCdcOnTemplate = "EXEC sys.sp_cdc_enable_table @source_schema = '{0}',@source_name = '{1}',@role_name = null,@supports_net_changes =1,@index_name ='{2}';";
            foreach (var table in tableResult)
            {
                if (table.Indexes.Count() > 0)
                {
                    var index = table.Indexes.FirstOrDefault(i => i.IndexType.Contains("primary"));
                    var enableTableCdc = string.Format(tableCdcOnTemplate, table.Schema, table.Name, index?.IndexName);

                    try
                    {
                        logger.LogDebug($"enabling cdc for {table.Schema}.{table.Name}, index: {index?.IndexName} : {enableTableCdc}");
                        var enableTableResult = dac.ExecuteCommand(enableTableCdc);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, $"Unable to turn CDC on for table {table.Schema}.{table.Name}");
                    }
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
            }

            return allTables;
        }

        public static IEnumerable<SqlIndex> GetIndexes(SimpleDac dac, string schema, string tableName)
        {
            var tableSelect = $"EXEC sp_helpindex '{schema}.{tableName}';";
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

