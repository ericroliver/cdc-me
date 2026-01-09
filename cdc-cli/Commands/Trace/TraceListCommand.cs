using System.CommandLine;
using System.CommandLine.Invocation;
using cdc_cli.Configuration;
using cdc_cli.Services;
using Microsoft.Extensions.Logging;

namespace cdc_cli.Commands.Trace;

/// <summary>
/// Command to list all trace sessions
/// </summary>
public class TraceListCommand : ApiCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TraceListCommand"/> class
    /// </summary>
    /// <param name="apiClient">HTTP API client</param>
    /// <param name="jsonHandler">JSON handler</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">CLI configuration</param>
    public TraceListCommand(
        ICdcApiClient apiClient,
        IJsonHandler jsonHandler,
        ILogger<TraceListCommand> logger,
        CliConfiguration configuration)
        : base("list", "List all trace sessions", apiClient, jsonHandler, logger, configuration)
    {
        ConfigureCommand();
    }

    /// <summary>
    /// Configures command options and handler
    /// </summary>
    private void ConfigureCommand()
    {
        this.SetHandler(async (InvocationContext context) =>
        {
            context.ExitCode = await ExecuteAsync();
        });
    }

    /// <summary>
    /// Executes the trace list command
    /// </summary>
    /// <returns>Exit code</returns>
    private async Task<int> ExecuteAsync()
    {
        try
        {
            // Make API call - GET request
            var response = await ExecuteGetAsync<object>("/api/trace/sessions");
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
