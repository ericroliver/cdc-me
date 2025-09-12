using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.IO;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Softbase;
using CdcProto.Commands;


class Program
{

    public static async Task<int> Main(string[] args)
    {
        var serviceProvider = BuildServiceProvider();
        var parser = BuildParser(serviceProvider);

        return await parser.InvokeAsync(args).ConfigureAwait(false);
    }

    private static Parser BuildParser(ServiceProvider serviceProvider)
    {
        var commandLineBuilder = new CommandLineBuilder();

        foreach (var command in serviceProvider.GetServices<Command>())
        {
            commandLineBuilder.Command.AddCommand(command);
        }

        return commandLineBuilder.UseDefaults().Build();
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        var connectionString = "Server=192.168.1.76,5433;Database=sbcrm;User Id=sa;Password=A123_Z321!;";
        var config = new ConfigurationBuilder()
            //.AddJsonFile("appsettings.json")
            .Build();
        services.AddLogging(c => c.AddConsole().AddDebug());

        services.AddSingleton<IConfiguration>(config);
        services.AddScoped<SimpleDac>(sp =>
        {
            var lf = sp.GetService<ILoggerFactory>();
            var logger = lf?.CreateLogger("cdc");
            return new SimpleDac(connectionString, logger);
        });

        services.AddCliCommands();

        return services.BuildServiceProvider();
    }
    //static async Task Main(string[] args) => await BuildCommandLine()
    //    .UseHost(_ => Host.CreateDefaultBuilder(),
    //        host =>
    //        {
    //            host.ConfigureServices(services =>
    //            {
    //                services.AddSingleton<ICommand, InitCommand>();
    //            });
    //        })
    //    .UseDefaults()
    //    .Build()
    //    .InvokeAsync(args).ConfigureAwait(false);

    //private static CommandLineBuilder BuildCommandLine()
    //{
    //    var commandLineBuilder = new CommandLineBuilder();

    //    foreach (var command in serviceProvider.GetServices<Command>())
    //    {
    //        commandLineBuilder.AddCommand(command);
    //    }

    //    return commandLineBuilder.UseDefaults().Build();

    //    var root = new RootCommand("nan - needs a name");
    //    root.Handler = CommandHandler.Create<CommandOptions, IHost>(Run);

    //    var initCommand = new Command("init", "initialize cdc.");
    //    root.AddCommand(initCommand);


    //    return new CommandLineBuilder(root);
    //}

    //private static void Run(CommandOptions options, IHost host)
    //{
    //    var serviceProvider = host.Services;
    //    //var command = serviceProvider.GetRequiredServices<ICommand>();

    //    var commands = serviceProvider.GetServices<ICommand>();
    //    var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
    //    var logger = loggerFactory.CreateLogger(typeof(Program));

    //    var command = commands.Where(c => c.N)
    //    var name = options.Name;
    //    logger.LogDebug($"Greeting was requested for: {name}");
    //    command.Run();
    //}
}

public static class SystemCommandLineExtensions
{
    public static IServiceCollection AddCliCommands(this IServiceCollection services)
    {
        Type commandType = typeof(InitCommand);
        Type baseCommandType = typeof(Command);

        IEnumerable<Type> commands = commandType
            .Assembly
            .GetExportedTypes()
            .Where(x => x.Namespace == commandType.Namespace && baseCommandType.IsAssignableFrom(x));

        foreach (Type command in commands)
        {
            services.AddSingleton(baseCommandType, command);
        }

        // Add new trace commands
        services.AddSingleton<Command>(SnapshotCommand.CreateCommand());
        services.AddSingleton<Command>(TraceCommand.CreateCommand());
        services.AddSingleton<Command>(ReplayCommand.CreateCommand());

        return services;
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
        // 192.168.1.76
        var connectionString = "Server= 192.168.1.76,5443;Database=sbtest;User Id=sa;Password=A123_Z321!;";
        //var connectionString = "Server= 192.168.1.125,5443;Database=sbtest;User Id=sa;Password=A123_Z321!;";
        var dac = new SimpleDac(connectionString, logger);


        //if (args.Any(a => a == "--test"))
        //{
        //    try
        //    {
        //        logger.LogDebug("test command..");
        //        Diff(dac, logger);
        //    }
        //    catch (Exception)
        //    {
        //        return;
        //    }
        //}

        //if (args.Any(a => a == "--bteardown"))
        //{
        //    try
        //    {
        //        logger.LogDebug("pre-run teardown..");
        //        ClearCdc(dac, logger);
        //    }
        //    catch (Exception)
        //    {
        //        return;
        //    }
        //}

        //if (args.Any(a => a == "--init"))
        //{
        //    try
        //    {
        //        logger.LogDebug("initialize..");
        //        Init(dac, logger);
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.LogError(ex, "Init failed");
        //        return;
        //    }
        //}
        //if (args.Any(a => a == "--report"))
        //{
        //    try
        //    {
        //        logger.LogDebug("report..");
        //        Report(dac, logger);
        //    }
        //    catch (Exception)
        //    {
        //        return;
        //    }
        //}
        //if (args.Any(a => a == "--advance"))
        //{
        //    try
        //    {
        //        logger.LogDebug("advance pointers..");
        //        Init(dac, logger);
        //    }
        //    catch (Exception)
        //    {
        //        return;
        //    }
        //}

        //if (args.Any(a => a == "--profile"))
        //{
        //    try
        //    {
        //        logger.LogDebug("generate full profile..");
        //        Profile(dac, logger);
        //    }
        //    catch (Exception)
        //    {
        //        return;
        //    }
        //}

        //if (args.Any(a => a == "--netprofile"))
        //{
        //    try
        //    {
        //        logger.LogDebug("generate net profile..");
        //        NetProfile(dac, logger);
        //    }
        //    catch (Exception)
        //    {
        //        return;
        //    }
        //}

        //if (args.Any(a => a == "--diff"))
        //{
        //    try
        //    {
        //        logger.LogDebug("diff profiles..");
        //        Diff(dac, logger);
        //    }
        //    catch (Exception)
        //    {
        //        return;
        //    }
        //}

        //if (args.Any(a => a == "--ateardown"))
        //{
        //    try
        //    {
        //        logger.LogDebug("post-run teardown..");
        //        ClearCdc(dac, logger);
        //    }
        //    catch (Exception)
        //    {
        //        return;
        //    }
        //}
    }

    //private static void Profile(SimpleDac dac, ILogger logger)
    //{
    //    var tableResult = default(IEnumerable<SqlTable>);
    //    try
    //    {
    //        tableResult = GetTables(dac);
    //        var profile = BuildProfile(dac, tableResult, logger);
    //        File.WriteAllText("/Users/sakamoto/.cdc/profile1.json", profile.ToJson(true));
    //    }
    //    catch (Exception ex)
    //    {
    //        logger.LogError(ex, $"err");
    //        throw;
    //    }


    //}


    //private static void Advance(SimpleDac dac, ILogger logger)
    //{
    //    var tableResult = default(IEnumerable<SqlTable>);
    //    try
    //    {
    //        tableResult = GetTables(dac);
    //    }
    //    catch (Exception ex)
    //    {
    //        logger.LogError(ex, $"Unable to retrieve the list of tables");
    //        throw;
    //    }

    //    foreach (var table in tableResult)
    //    {

    //    }

    //}

    //private static void Diff(SimpleDac dac, ILogger logger)
    //{
    //    try
    //    {
    //        var rollup1 = File.ReadAllText("/Users/sakamoto/.cdc/netprofile.7.json").FromJson<IDictionary<string, IEnumerable<IDictionary<string, object>>>>();
    //        var rollup2 = File.ReadAllText("/Users/sakamoto/.cdc/netprofile.8.json").FromJson<IDictionary<string, IEnumerable<IDictionary<string, object>>>>();
    //        var tables = GetTables(dac);

    //        var differ = new ProfileDiffer();
    //        var result = differ.Diff(tables, rollup1, rollup2);
    //        File.WriteAllText("/Users/sakamoto/.cdc/diff7_8.json", result.ToJson(true));
    //    }
    //    catch (Exception ex)
    //    {
    //        logger.LogError(ex, $"No diffidy for you");
    //        throw;
    //    }

    //}

    //private static void ClearCdc(SimpleDac dac, ILogger logger)
    //{
    //    try
    //    {
    //        DisableCdcOnDatabase(dac);
    //    }
    //    catch (Exception ex)
    //    {
    //        logger.LogError(ex, $"Unable to turn CDC off exiting");
    //        throw;
    //    }
    //}

    //private static void Report(SimpleDac dac, ILogger logger)
    //{
    //    try
    //    {
    //        var tables = GetTables(dac);
    //        var noindexes = tables.Where(t => !t.Indexes.Any(i => i.IndexType.Contains("clustered, unique, primary key")));
    //        File.WriteAllText("/Users/sakamoto/.cdc/noindexes.json", noindexes.ToJson(true));

    //        var multiprimary = tables.Where(t => t.Indexes.Where(i => i.IndexType.Contains("clustered, unique, primary key")).Count() > 1);
    //        File.WriteAllText("/Users/sakamoto/.cdc/multiprimary.json", multiprimary.ToJson(true));
    //    }
    //    catch (Exception ex)
    //    {
    //        logger.LogError(ex, $"that's an error");
    //        throw;
    //    }
    //}


}
