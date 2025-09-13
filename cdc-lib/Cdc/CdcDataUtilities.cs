using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Softbase.Cdc
{
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

        public static IDictionary<string, IEnumerable<IDictionary<string, object>>> BuildNetProfile(SimpleDac dac, IEnumerable<SqlTable> tableResult, ILogger logger)
        {
            var allResults = new Dictionary<string, IEnumerable<IDictionary<string, object>>>();

            foreach (var table in tableResult)
            {

                if (!table.HasPrimaryKey)
                    continue;

                var tableSelect = GetNetSqlFromTemplate(table.Schema, table.Name);

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
                    logger.LogError(ex, $"Unable to retrieve net changes for {table.Schema}.{table.Name}");
                }
            }

            return allResults;
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

