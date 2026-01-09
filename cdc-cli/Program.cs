using System.CommandLine;
using cdc_cli.Commands.Cdc;
using cdc_cli.Commands.Snapshot;
using cdc_cli.Commands.Trace;
using cdc_cli.Commands.Workflow;
using cdc_cli.Configuration;
using cdc_cli.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace cdc_cli;

/// <summary>
/// Main program entry point for cdc-cli
/// </summary>
public class Program
{
    /// <summary>
    /// Main entry point
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Exit code</returns>
    public static async Task<int> Main(string[] args)
    {
        // Load configuration from environment
        var configuration = CliConfiguration.LoadFromEnvironment();

        // Build service provider
        var serviceProvider = ConfigureServices(configuration);

        // Create root command
        var rootCommand = new RootCommand("CDC CLI - Command-line interface for CDC Testing Framework API");

        // Add global options
        var baseUrlOption = new Option<string?>(
            aliases: new[] { "--base-url", "-u" },
            description: "Base URL for the CDC API (overrides CDC_API_URL environment variable)",
            getDefaultValue: () => configuration.BaseUrl);

        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Output format: json, json-pretty, or text",
            getDefaultValue: () => "json");

        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "Enable verbose logging",
            getDefaultValue: () => false);

        var quietOption = new Option<bool>(
            aliases: new[] { "--quiet", "-q" },
            description: "Suppress non-essential output",
            getDefaultValue: () => false);

        rootCommand.AddGlobalOption(baseUrlOption);
        rootCommand.AddGlobalOption(outputOption);
        rootCommand.AddGlobalOption(verboseOption);
        rootCommand.AddGlobalOption(quietOption);

        // Resolve dependencies from service provider
        var apiClient = serviceProvider.GetRequiredService<ICdcApiClient>();
        var jsonHandler = serviceProvider.GetRequiredService<IJsonHandler>();

        // Create CDC command group
        var cdcCommand = new Command("cdc", "CDC lifecycle management commands");
        cdcCommand.AddCommand(new CdcStartCommand(
            apiClient,
            jsonHandler,
            serviceProvider.GetRequiredService<ILogger<CdcStartCommand>>(),
            configuration));
        cdcCommand.AddCommand(new CdcStopCommand(
            apiClient,
            jsonHandler,
            serviceProvider.GetRequiredService<ILogger<CdcStopCommand>>(),
            configuration));
        cdcCommand.AddCommand(new CdcCaptureCommand(
            apiClient,
            jsonHandler,
            serviceProvider.GetRequiredService<ILogger<CdcCaptureCommand>>(),
            configuration));
        cdcCommand.AddCommand(new CdcCompareCommand(
            apiClient,
            jsonHandler,
            serviceProvider.GetRequiredService<ILogger<CdcCompareCommand>>(),
            configuration));
        rootCommand.AddCommand(cdcCommand);

        // Create Snapshot command group
        var snapshotCommand = new Command("snapshot", "Database snapshot management commands");
        snapshotCommand.AddCommand(new SnapshotCreateCommand(
            apiClient,
            jsonHandler,
            serviceProvider.GetRequiredService<ILogger<SnapshotCreateCommand>>(),
            configuration));
        snapshotCommand.AddCommand(new SnapshotRestoreCommand(
            apiClient,
            jsonHandler,
            serviceProvider.GetRequiredService<ILogger<SnapshotRestoreCommand>>(),
            configuration));
        snapshotCommand.AddCommand(new SnapshotListCommand(
            apiClient,
            jsonHandler,
            serviceProvider.GetRequiredService<ILogger<SnapshotListCommand>>(),
            configuration));
        snapshotCommand.AddCommand(new SnapshotInfoCommand(
            apiClient,
            jsonHandler,
            serviceProvider.GetRequiredService<ILogger<SnapshotInfoCommand>>(),
            configuration));
        snapshotCommand.AddCommand(new SnapshotDeleteCommand(
            apiClient,
            jsonHandler,
            serviceProvider.GetRequiredService<ILogger<SnapshotDeleteCommand>>(),
            configuration));
        rootCommand.AddCommand(snapshotCommand);

        // Create Trace command group
        var traceCommand = new Command("trace", "SQL trace session management commands");
        traceCommand.AddCommand(new TraceStartCommand(
            apiClient,
            jsonHandler,
            serviceProvider.GetRequiredService<ILogger<TraceStartCommand>>(),
            configuration));
        traceCommand.AddCommand(new TraceStopCommand(
            apiClient,
            jsonHandler,
            serviceProvider.GetRequiredService<ILogger<TraceStopCommand>>(),
            configuration));
        traceCommand.AddCommand(new TraceStatusCommand(
            apiClient,
            jsonHandler,
            serviceProvider.GetRequiredService<ILogger<TraceStatusCommand>>(),
            configuration));
        traceCommand.AddCommand(new TraceListCommand(
            apiClient,
            jsonHandler,
            serviceProvider.GetRequiredService<ILogger<TraceListCommand>>(),
            configuration));
        traceCommand.AddCommand(new TraceEventsCommand(
            apiClient,
            jsonHandler,
            serviceProvider.GetRequiredService<ILogger<TraceEventsCommand>>(),
            configuration));
        traceCommand.AddCommand(new TraceExportCommand(
            apiClient,
            jsonHandler,
            serviceProvider.GetRequiredService<ILogger<TraceExportCommand>>(),
            configuration));
        traceCommand.AddCommand(new TraceDeleteCommand(
            apiClient,
            jsonHandler,
            serviceProvider.GetRequiredService<ILogger<TraceDeleteCommand>>(),
            configuration));
        rootCommand.AddCommand(traceCommand);

        // Create Workflow command group
        var workflowCommand = new Command("workflow", "Test workflow orchestration commands");
        workflowCommand.AddCommand(new WorkflowExecuteCommand(
            apiClient,
            jsonHandler,
            serviceProvider.GetRequiredService<ILogger<WorkflowExecuteCommand>>(),
            configuration));
        workflowCommand.AddCommand(new WorkflowStatusCommand(
            apiClient,
            jsonHandler,
            serviceProvider.GetRequiredService<ILogger<WorkflowStatusCommand>>(),
            configuration));
        workflowCommand.AddCommand(new WorkflowListCommand(
            apiClient,
            jsonHandler,
            serviceProvider.GetRequiredService<ILogger<WorkflowListCommand>>(),
            configuration));
        rootCommand.AddCommand(workflowCommand);

        // Set handler to apply global options
        rootCommand.SetHandler((baseUrl, output, verbose, quiet) =>
        {
            // Apply base URL if provided
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                configuration.BaseUrl = baseUrl;
            }

            // Apply output format
            configuration.OutputFormat = output?.ToLowerInvariant() switch
            {
                "json" => OutputFormat.Json,
                "json-pretty" => OutputFormat.JsonPretty,
                "text" => OutputFormat.Text,
                _ => OutputFormat.Json
            };

            // Apply verbose and quiet flags
            configuration.Verbose = verbose;
            configuration.Quiet = quiet;

            // Validate configuration
            try
            {
                configuration.Validate();
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"Configuration error: {ex.Message}");
                Environment.ExitCode = 3; // Validation error
            }
        }, baseUrlOption, outputOption, verboseOption, quietOption);

        // Parse and invoke command
        try
        {
            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Unhandled exception occurred");
            await Console.Error.WriteLineAsync($"Fatal error: {ex.Message}");
            return 1;
        }
        finally
        {
            // Dispose of service provider
            if (serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    /// <summary>
    /// Configures dependency injection services
    /// </summary>
    /// <param name="configuration">CLI configuration</param>
    /// <returns>Configured service provider</returns>
    private static ServiceProvider ConfigureServices(CliConfiguration configuration)
    {
        var services = new ServiceCollection();

        // Register configuration as singleton
        services.AddSingleton(configuration);

        // Configure logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(configuration.Verbose ? LogLevel.Debug : LogLevel.Warning);
            
            // Suppress HTTP client logging unless verbose mode is enabled
            if (!configuration.Verbose)
            {
                builder.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
            }
        });

        // Register HTTP client factory and CDC API client
        services.AddHttpClient<ICdcApiClient, CdcApiClient>();

        // Register JSON handler
        services.AddSingleton<IJsonHandler, JsonHandler>();

        // Build and return service provider
        return services.BuildServiceProvider();
    }
}
