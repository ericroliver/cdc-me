using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.IO;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Softbase;


class Program
{
    static async Task Main(string[] args) => await BuildCommandLine()
        .UseHost(_ => Host.CreateDefaultBuilder(),
            host =>
            {
                host.ConfigureServices(services =>
                {
                    services.AddSingleton<ICommand, InitCommand>();
                });
            })
        .UseDefaults()
        .Build()
        .InvokeAsync(args);

    private static CommandLineBuilder BuildCommandLine()
    {
        var root = new RootCommand("nan - needs a name");
        root.Handler = CommandHandler.Create<CommandOptions, IHost>(Run);

        var initCommand = new Command("init", "initialize cdc.");
        root.AddCommand(initCommand);


        return new CommandLineBuilder(root);
    }

    private static void Run(CommandOptions options, IHost host)
    {
        var serviceProvider = host.Services;
        var command = serviceProvider.GetRequiredService<ICommand>();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(typeof(Program));

        var name = options.Name;
        logger.LogDebug($"Greeting was requested for: {name}");
        command.Run();
    }
}

public interface ICommand
{
    public void Run();
}

public class InitCommand : ICommand
{
    public void Run()
    {
        Console.WriteLine("init");
    }
}

public class CommandOptions
{
    public string Name { get; }

    public CommandOptions(string name)
    {
        Name = name;
    }
}

//systranschemas,lsn_time_mapping,ddl_history,change_tables,captured_columns,index_columns
internal class OldProgram
{
    private static void Main(string[] args)
    {
        const string configPath = "/usr/.cdc";

        //var init = args.Any(a => a.Equals("init"));
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var logger = loggerFactory.CreateLogger("CDC Utililty");

        //var connectionString = "Server= 192.168.230.153;Database=sb-test-cdc;User Id=appuser;Password=appuser;";
        var connectionString = "Server= 192.168.0.107,5443;Database=sbtest;User Id=sa;Password=A123_Z321!;";
        //var connectionString = "Server= 192.168.1.125,5443;Database=sbtest;User Id=sa;Password=A123_Z321!;";
        var dac = new SimpleDac(connectionString, logger);


        if (args.Any(a => a == "--test"))
        {
            try
            {
                logger.LogDebug("test command..");
                Diff(dac, logger);
            }
            catch (Exception)
            {
                return;
            }
        }

        if (args.Any(a => a == "--bteardown"))
        {
            try
            {
                logger.LogDebug("pre-run teardown..");
                ClearCdc(dac, logger);
            }
            catch (Exception)
            {
                return;
            }
        }

        if (args.Any(a => a == "--init"))
        {
            try
            {
                logger.LogDebug("initialize..");
                Init(dac, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Init failed");
                return;
            }
        }
        if (args.Any(a => a == "--report"))
        {
            try
            {
                logger.LogDebug("report..");
                Report(dac, logger);
            }
            catch (Exception)
            {
                return;
            }
        }
        if (args.Any(a => a == "--advance"))
        {
            try
            {
                logger.LogDebug("advance pointers..");
                Init(dac, logger);
            }
            catch (Exception)
            {
                return;
            }
        }

        if (args.Any(a => a == "--profile"))
        {
            try
            {
                logger.LogDebug("generate full profile..");
                Profile(dac, logger);
            }
            catch (Exception)
            {
                return;
            }
        }

        if (args.Any(a => a == "--netprofile"))
        {
            try
            {
                logger.LogDebug("generate net profile..");
                NetProfile(dac, logger);
            }
            catch (Exception)
            {
                return;
            }
        }

        if (args.Any(a => a == "--diff"))
        {
            try
            {
                logger.LogDebug("diff profiles..");
                Diff(dac, logger);
            }
            catch (Exception)
            {
                return;
            }
        }

        if (args.Any(a => a == "--ateardown"))
        {
            try
            {
                logger.LogDebug("post-run teardown..");
                ClearCdc(dac, logger);
            }
            catch (Exception)
            {
                return;
            }
        }
    }

    private static void Profile(SimpleDac dac, ILogger logger)
    {
        var tableResult = default(IEnumerable<SqlTable>);
        try
        {
            tableResult = GetTables(dac);
            var profile = BuildProfile(dac, tableResult, logger);
            File.WriteAllText("/Users/sakamoto/.cdc/profile1.json", profile.ToJson(true));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"err");
            throw;
        }


    }

    private static void NetProfile(SimpleDac dac, ILogger logger)
    {
        var tableResult = default(IEnumerable<SqlTable>);
        try
        {
            tableResult = GetTables(dac);
            var profile = BuildNetProfile(dac, tableResult, logger);
            File.WriteAllText("/Users/sakamoto/.cdc/netprofile.json", profile.ToJson(true));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"err");
            throw;
        }


    }

    private static IDictionary<string, IEnumerable<IDictionary<string, object>>> BuildProfile(SimpleDac dac, IEnumerable<SqlTable> tableResult, ILogger logger)
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

    private static void Init(SimpleDac dac, ILogger logger)
    {
        try
        {
            EnableCdcOnDatabase(dac);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Unable to turn CDC on exit");
            throw;
        }

        var tableResult = default(IEnumerable<SqlTable>);
        try
        {
            tableResult = GetTables(dac);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Unable to retrieve the list of tables");
            throw;
        }

        EnableTableCdc(dac, tableResult, logger);
    }

    private static void Advance(SimpleDac dac, ILogger logger)
    {
        var tableResult = default(IEnumerable<SqlTable>);
        try
        {
            tableResult = GetTables(dac);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Unable to retrieve the list of tables");
            throw;
        }

        foreach (var table in tableResult)
        {

        }

    }

    private static void Diff(SimpleDac dac, ILogger logger)
    {
        try
        {
            var rollup1 = File.ReadAllText("/Users/sakamoto/.cdc/netprofile.7.json").FromJson<IDictionary<string, IEnumerable<IDictionary<string, object>>>>();
            var rollup2 = File.ReadAllText("/Users/sakamoto/.cdc/netprofile.8.json").FromJson<IDictionary<string, IEnumerable<IDictionary<string, object>>>>();
            var tables = GetTables(dac);

            var differ = new ProfileDiffer();
            var result = differ.Diff(tables, rollup1, rollup2);
            File.WriteAllText("/Users/sakamoto/.cdc/diff7_8.json", result.ToJson(true));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"No diffidy for you");
            throw;
        }

    }

    private static void ClearCdc(SimpleDac dac, ILogger logger)
    {
        try
        {
            DisableCdcOnDatabase(dac);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Unable to turn CDC off exiting");
            throw;
        }
    }

    private static void Report(SimpleDac dac, ILogger logger)
    {
        try
        {
            var tables = GetTables(dac);
            var noindexes = tables.Where(t => !t.Indexes.Any(i => i.IndexType.Contains("clustered, unique, primary key")));
            File.WriteAllText("/Users/sakamoto/.cdc/noindexes.json", noindexes.ToJson(true));

            var multiprimary = tables.Where(t => t.Indexes.Where(i => i.IndexType.Contains("clustered, unique, primary key")).Count() > 1);
            File.WriteAllText("/Users/sakamoto/.cdc/multiprimary.json", multiprimary.ToJson(true));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"that's an error");
            throw;
        }
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

    private static IDictionary<string, IEnumerable<IDictionary<string, object>>> BuildNetProfile(SimpleDac dac, IEnumerable<SqlTable> tableResult, ILogger logger)
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

    private static void EnableCdcOnDatabase(SimpleDac dac)
    {
        const string cdcOn = "exec sys.sp_cdc_enable_db";
        var cdcOnResult = dac.ExecuteCommand(cdcOn);
    }

    private static void DisableCdcOnDatabase(SimpleDac dac)
    {
        const string cdcOff = "exec sys.sp_cdc_disable_db";
        var cdcOnResult = dac.ExecuteCommand(cdcOff);
    }

    private static void EnableTableCdc(SimpleDac dac, IEnumerable<SqlTable> tableResult, ILogger logger)
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

    private static IEnumerable<SqlTable> GetTables(SimpleDac dac)
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

    private static IEnumerable<SqlIndex> GetIndexes(SimpleDac dac, string schema, string tableName)
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
