using System.CommandLine;
using System.CommandLine.Invocation;
using cdc_cli.Configuration;
using cdc_cli.Services;
using Microsoft.Extensions.Logging;

namespace cdc_cli.Commands.Trace;

/// <summary>
/// Command to get trace session status
/// </summary>
public class TraceStatusCommand : ApiCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TraceStatusCommand"/> class
    /// </summary>
    /// <param name="apiClient">HTTP API client</param>
    /// <param name="jsonHandler">JSON handler</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">CLI configuration</param>
    public TraceStatusCommand(
        ICdcApiClient apiClient,
        IJsonHandler jsonHandler,
        ILogger<TraceStatusCommand> logger,
        CliConfiguration configuration)
        : base("status", "Get trace session status", apiClient, jsonHandler, logger, configuration)
    {
        ConfigureCommand();
    }

    /// <summary>
    /// Configures command options and handler
    /// </summary>
    private void ConfigureCommand()
    {
        // Add positional argument
        var sessionArgument = new Argument<string>(
            name: "session",
            description: "Session name");

        // Add option (alternative to positional)
        var sessionOption = new Option<string?>(
            aliases: new[] { "--session", "-s" },
            description: "Session name (alternative to positional argument)");

        AddArgument(sessionArgument);
        AddOption(sessionOption);

        this.SetHandler(async (InvocationContext context) =>
        {
            // Prefer positional argument, fall back to option
            var sessionArg = context.ParseResult.GetValueForArgument(sessionArgument);
            var sessionOpt = context.ParseResult.GetValueForOption(sessionOption);
            var session = !string.IsNullOrWhiteSpace(sessionArg) ? sessionArg : sessionOpt;

            if (string.IsNullOrWhiteSpace(session))
            {
                Logger.LogError("Session name is required. Provide it as a positional argument or use --session option");
                context.ExitCode = ExitCodeValidationError;
                return;
            }

            context.ExitCode = await ExecuteAsync(session);
        });
    }

    /// <summary>
    /// Executes the trace status command
    /// </summary>
    /// <param name="session">Session name</param>
    /// <returns>Exit code</returns>
    private async Task<int> ExecuteAsync(string session)
    {
        try
        {
            // Make API call - GET request
            var response = await ExecuteGetAsync<object>($"/api/trace/status/{Uri.EscapeDataString(session)}");
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
