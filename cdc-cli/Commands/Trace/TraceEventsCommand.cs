using System.CommandLine;
using System.CommandLine.Invocation;
using cdc_cli.Configuration;
using cdc_cli.Services;
using Microsoft.Extensions.Logging;

namespace cdc_cli.Commands.Trace;

/// <summary>
/// Command to get trace events for a session with pagination support
/// </summary>
public class TraceEventsCommand : ApiCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TraceEventsCommand"/> class
    /// </summary>
    /// <param name="apiClient">HTTP API client</param>
    /// <param name="jsonHandler">JSON handler</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">CLI configuration</param>
    public TraceEventsCommand(
        ICdcApiClient apiClient,
        IJsonHandler jsonHandler,
        ILogger<TraceEventsCommand> logger,
        CliConfiguration configuration)
        : base("events", "Get trace events for a session", apiClient, jsonHandler, logger, configuration)
    {
        ConfigureCommand();
    }

    /// <summary>
    /// Configures command options and handler
    /// </summary>
    private void ConfigureCommand()
    {
        // Add positional argument for session ID
        var sessionIdArgument = new Argument<string>(
            name: "session-id",
            description: "Session ID (GUID)");

        // Add pagination options
        var limitOption = new Option<int>(
            aliases: new[] { "--limit" },
            getDefaultValue: () => 100,
            description: "Maximum number of events to return (default: 100, max: 1000)");

        var offsetOption = new Option<int>(
            aliases: new[] { "--offset" },
            getDefaultValue: () => 0,
            description: "Number of events to skip (default: 0)");

        AddArgument(sessionIdArgument);
        AddOption(limitOption);
        AddOption(offsetOption);

        this.SetHandler(async (InvocationContext context) =>
        {
            var sessionId = context.ParseResult.GetValueForArgument(sessionIdArgument);
            var limit = context.ParseResult.GetValueForOption(limitOption);
            var offset = context.ParseResult.GetValueForOption(offsetOption);

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                Logger.LogError("Session ID is required as a positional argument");
                context.ExitCode = ExitCodeValidationError;
                return;
            }

            // Validate session ID is a GUID
            if (!Guid.TryParse(sessionId, out var sessionGuid))
            {
                Logger.LogError("Session ID must be a valid GUID");
                context.ExitCode = ExitCodeValidationError;
                return;
            }

            // Validate limit
            if (limit < 1 || limit > 1000)
            {
                Logger.LogError("Limit must be between 1 and 1000");
                context.ExitCode = ExitCodeValidationError;
                return;
            }

            // Validate offset
            if (offset < 0)
            {
                Logger.LogError("Offset must be non-negative");
                context.ExitCode = ExitCodeValidationError;
                return;
            }

            context.ExitCode = await ExecuteAsync(sessionGuid, limit, offset);
        });
    }

    /// <summary>
    /// Executes the trace events command
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="limit">Maximum events to return</param>
    /// <param name="offset">Events to skip</param>
    /// <returns>Exit code</returns>
    private async Task<int> ExecuteAsync(Guid sessionId, int limit, int offset)
    {
        try
        {
            // Build URL with query parameters
            var url = $"/api/trace/sessions/{sessionId}/events?limit={limit}&offset={offset}";

            // Make API call - GET request
            var response = await ExecuteGetAsync<object>(url);
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
