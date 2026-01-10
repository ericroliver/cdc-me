using System.CommandLine;
using System.CommandLine.Invocation;
using cdc_cli.Configuration;
using cdc_cli.Services;
using Microsoft.Extensions.Logging;

namespace cdc_cli.Commands.Trace;

/// <summary>
/// Command to start a trace session to capture SQL statements
/// </summary>
public class TraceStartCommand : ApiCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TraceStartCommand"/> class
    /// </summary>
    /// <param name="apiClient">HTTP API client</param>
    /// <param name="jsonHandler">JSON handler</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">CLI configuration</param>
    public TraceStartCommand(
        ICdcApiClient apiClient,
        IJsonHandler jsonHandler,
        ILogger<TraceStartCommand> logger,
        CliConfiguration configuration)
        : base("start", "Start a trace session to capture SQL statements", apiClient, jsonHandler, logger, configuration)
    {
        ConfigureCommand();
    }

    /// <summary>
    /// Configures command options and handler
    /// </summary>
    private void ConfigureCommand()
    {
        // Add options
        var sessionOption = new Option<string>(
            aliases: new[] { "--session", "-s" },
            description: "Session name (required)")
        {
            IsRequired = true
        };

        var databaseOption = new Option<string>(
            aliases: new[] { "--database", "-d" },
            description: "Database name (required)")
        {
            IsRequired = true
        };

        var maxFileSizeOption = new Option<int?>(
            aliases: new[] { "--max-file-size" },
            description: "Max file size in MB (optional, default: 100)");

        var maxFilesOption = new Option<int?>(
            aliases: new[] { "--max-files" },
            description: "Max number of files (optional, default: 5)");

        var eventsOption = new Option<string?>(
            aliases: new[] { "--events" },
            description: "Comma-separated events to capture (optional, default: sql_statement_completed)");

        var dataOption = CreateDataOption();
        var fileOption = CreateFileOption();

        AddOption(sessionOption);
        AddOption(databaseOption);
        AddOption(maxFileSizeOption);
        AddOption(maxFilesOption);
        AddOption(eventsOption);
        AddOption(dataOption);
        AddOption(fileOption);

        this.SetHandler(async (InvocationContext context) =>
        {
            var session = context.ParseResult.GetValueForOption(sessionOption);
            var database = context.ParseResult.GetValueForOption(databaseOption);
            var maxFileSize = context.ParseResult.GetValueForOption(maxFileSizeOption);
            var maxFiles = context.ParseResult.GetValueForOption(maxFilesOption);
            var events = context.ParseResult.GetValueForOption(eventsOption);
            var data = context.ParseResult.GetValueForOption(dataOption);
            var file = context.ParseResult.GetValueForOption(fileOption);

            context.ExitCode = await ExecuteAsync(session!, database!, maxFileSize, maxFiles, events, data, file);
        });
    }

    /// <summary>
    /// Executes the trace start command
    /// </summary>
    /// <param name="session">Session name</param>
    /// <param name="database">Database name</param>
    /// <param name="maxFileSize">Max file size in MB</param>
    /// <param name="maxFiles">Max number of files</param>
    /// <param name="events">Comma-separated event list</param>
    /// <param name="data">Inline JSON data</param>
    /// <param name="file">Path to JSON file</param>
    /// <returns>Exit code</returns>
    private async Task<int> ExecuteAsync(
        string session,
        string database,
        int? maxFileSize,
        int? maxFiles,
        string? events,
        string? data,
        string? file)
    {
        try
        {
            // Parse events list if provided
            List<string>? eventsToCapture = null;
            if (!string.IsNullOrWhiteSpace(events))
            {
                eventsToCapture = events.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            }

            // Build request object from CLI params
            var requestFromParams = new
            {
                SessionName = session,
                DatabaseName = database,
                MaxFileSize = maxFileSize,
                MaxFiles = maxFiles,
                EventsToCapture = eventsToCapture,
                FilterCriteria = new Dictionary<string, object>()
            };

            // Get final request using input precedence (data, file, stdin, params)
            var request = await JsonHandler.GetInputAsync(data, file, requestFromParams, CancellationToken.None);

            // Make API call
            var response = await ExecuteApiCallAsync<object, object>("/api/trace/start", request);
            if (response == null)
            {
                return ExitCodeApiError; // Error already handled
            }

            // Output response
            await WriteResponseAsync(response);
            return ExitCodeSuccess;
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
            return ExitCodeApiError;
        }
    }
}
