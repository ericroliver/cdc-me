using System.CommandLine;
using System.CommandLine.Invocation;
using cdc_cli.Configuration;
using cdc_cli.Services;
using Microsoft.Extensions.Logging;

namespace cdc_cli.Commands.Trace;

/// <summary>
/// Command to export trace data to trace database
/// </summary>
public class TraceExportCommand : ApiCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TraceExportCommand"/> class
    /// </summary>
    /// <param name="apiClient">HTTP API client</param>
    /// <param name="jsonHandler">JSON handler</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">CLI configuration</param>
    public TraceExportCommand(
        ICdcApiClient apiClient,
        IJsonHandler jsonHandler,
        ILogger<TraceExportCommand> logger,
        CliConfiguration configuration)
        : base("export", "Export trace data to trace database", apiClient, jsonHandler, logger, configuration)
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

        AddOption(sessionOption);

        this.SetHandler(async (InvocationContext context) =>
        {
            var session = context.ParseResult.GetValueForOption(sessionOption);

            context.ExitCode = await ExecuteAsync(session!);
        });
    }

    /// <summary>
    /// Executes the trace export command
    /// </summary>
    /// <param name="session">Session name</param>
    /// <returns>Exit code</returns>
    private async Task<int> ExecuteAsync(string session)
    {
        try
        {
            // Build request object
            var request = new
            {
                SessionName = session
            };

            // Make API call
            var response = await ExecuteApiCallAsync<object, object>("/api/trace/export", request);
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
