using System.CommandLine;
using System.CommandLine.Invocation;
using cdc_cli.Configuration;
using cdc_cli.Services;
using Microsoft.Extensions.Logging;

namespace cdc_cli.Commands.Trace;

/// <summary>
/// Command to delete a trace session and its data
/// </summary>
public class TraceDeleteCommand : ApiCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TraceDeleteCommand"/> class
    /// </summary>
    /// <param name="apiClient">HTTP API client</param>
    /// <param name="jsonHandler">JSON handler</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">CLI configuration</param>
    public TraceDeleteCommand(
        ICdcApiClient apiClient,
        IJsonHandler jsonHandler,
        ILogger<TraceDeleteCommand> logger,
        CliConfiguration configuration)
        : base("delete", "Delete a trace session and its data", apiClient, jsonHandler, logger, configuration)
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

        var forceOption = new Option<bool>(
            aliases: new[] { "--force", "-f" },
            description: "Skip confirmation prompt",
            getDefaultValue: () => false);

        AddArgument(sessionIdArgument);
        AddOption(forceOption);

        this.SetHandler(async (InvocationContext context) =>
        {
            var sessionId = context.ParseResult.GetValueForArgument(sessionIdArgument);
            var force = context.ParseResult.GetValueForOption(forceOption);

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

            context.ExitCode = await ExecuteAsync(sessionGuid, force);
        });
    }

    /// <summary>
    /// Executes the delete trace session command
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="force">Whether to skip confirmation</param>
    /// <returns>Exit code</returns>
    private async Task<int> ExecuteAsync(Guid sessionId, bool force)
    {
        try
        {
            // Interactive confirmation if force flag not provided
            if (!force && !Configuration.Quiet)
            {
                Console.WriteLine($"WARNING: You are about to permanently delete trace session '{sessionId}' and all its data.");
                Console.Write("Are you sure you want to continue? (y/N): ");
                var userResponse = Console.ReadLine()?.Trim().ToLowerInvariant();

                if (userResponse != "y" && userResponse != "yes")
                {
                    Console.WriteLine("Delete cancelled.");
                    return ExitCodeSuccess; // User cancelled, not an error
                }
            }

            // Make API call
            var response = await ExecuteDeleteAsync<object>($"/api/trace/sessions/{sessionId}");
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
