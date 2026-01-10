using System.CommandLine;
using System.CommandLine.Invocation;
using cdc_cli.Configuration;
using cdc_cli.Services;
using Microsoft.Extensions.Logging;

namespace cdc_cli.Commands.Trace;

/// <summary>
/// Command to stop a trace session
/// </summary>
public class TraceStopCommand : ApiCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TraceStopCommand"/> class
    /// </summary>
    /// <param name="apiClient">HTTP API client</param>
    /// <param name="jsonHandler">JSON handler</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">CLI configuration</param>
    public TraceStopCommand(
        ICdcApiClient apiClient,
        IJsonHandler jsonHandler,
        ILogger<TraceStopCommand> logger,
        CliConfiguration configuration)
        : base("stop", "Stop a trace session", apiClient, jsonHandler, logger, configuration)
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

        var dataOption = CreateDataOption();
        var fileOption = CreateFileOption();

        AddOption(sessionOption);
        AddOption(dataOption);
        AddOption(fileOption);

        this.SetHandler(async (InvocationContext context) =>
        {
            var session = context.ParseResult.GetValueForOption(sessionOption);
            var data = context.ParseResult.GetValueForOption(dataOption);
            var file = context.ParseResult.GetValueForOption(fileOption);

            context.ExitCode = await ExecuteAsync(session!, data, file);
        });
    }

    /// <summary>
    /// Executes the trace stop command
    /// </summary>
    /// <param name="session">Session name</param>
    /// <param name="data">Inline JSON data</param>
    /// <param name="file">Path to JSON file</param>
    /// <returns>Exit code</returns>
    private async Task<int> ExecuteAsync(
        string session,
        string? data,
        string? file)
    {
        try
        {
            // Build request object from CLI params
            var requestFromParams = new
            {
                SessionName = session
            };

            // Get final request using input precedence (data, file, stdin, params)
            var request = await JsonHandler.GetInputAsync(data, file, requestFromParams, CancellationToken.None);

            // Make API call
            var response = await ExecuteApiCallAsync<object, object>("/api/trace/stop", request);
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
