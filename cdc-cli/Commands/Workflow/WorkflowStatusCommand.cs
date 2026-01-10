using System.CommandLine;
using System.CommandLine.Invocation;
using cdc_cli.Configuration;
using cdc_cli.Services;
using Microsoft.Extensions.Logging;

namespace cdc_cli.Commands.Workflow;

/// <summary>
/// Command to get workflow execution status
/// </summary>
public class WorkflowStatusCommand : ApiCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowStatusCommand"/> class
    /// </summary>
    /// <param name="apiClient">HTTP API client</param>
    /// <param name="jsonHandler">JSON handler</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">CLI configuration</param>
    public WorkflowStatusCommand(
        ICdcApiClient apiClient,
        IJsonHandler jsonHandler,
        ILogger<WorkflowStatusCommand> logger,
        CliConfiguration configuration)
        : base("status", "Get workflow execution status", apiClient, jsonHandler, logger, configuration)
    {
        ConfigureCommand();
    }

    /// <summary>
    /// Configures command options and handler
    /// </summary>
    private void ConfigureCommand()
    {
        // Add positional argument for workflow ID
        var workflowIdArgument = new Argument<string>(
            name: "workflow-id",
            description: "Workflow ID to check status");

        // Add option variant
        var workflowOption = new Option<string?>(
            aliases: new[] { "--workflow", "--id" },
            description: "Workflow ID (alternative to positional argument)");

        var watchOption = new Option<bool>(
            aliases: new[] { "--watch", "-w" },
            description: "Continuously poll and display updates until workflow completes",
            getDefaultValue: () => false);

        var intervalOption = new Option<int>(
            aliases: new[] { "--interval" },
            description: "Polling interval in seconds for watch mode (default: 5)",
            getDefaultValue: () => 5);

        AddArgument(workflowIdArgument);
        AddOption(workflowOption);
        AddOption(watchOption);
        AddOption(intervalOption);

        this.SetHandler(async (InvocationContext context) =>
        {
            var workflowIdArg = context.ParseResult.GetValueForArgument(workflowIdArgument);
            var workflowOpt = context.ParseResult.GetValueForOption(workflowOption);
            var watch = context.ParseResult.GetValueForOption(watchOption);
            var interval = context.ParseResult.GetValueForOption(intervalOption);

            // Use option if provided, otherwise use argument
            var workflowId = workflowOpt ?? workflowIdArg;

            if (string.IsNullOrWhiteSpace(workflowId))
            {
                await Console.Error.WriteLineAsync("Error: Workflow ID is required");
                context.ExitCode = ExitCodeValidationError;
                return;
            }

            context.ExitCode = await ExecuteAsync(workflowId, watch, interval);
        });
    }

    /// <summary>
    /// Executes the workflow status command
    /// </summary>
    /// <param name="workflowId">Workflow ID</param>
    /// <param name="watch">Whether to watch for updates</param>
    /// <param name="interval">Polling interval in seconds</param>
    /// <returns>Exit code</returns>
    private async Task<int> ExecuteAsync(string workflowId, bool watch, int interval)
    {
        try
        {
            // Parse workflow ID
            if (!Guid.TryParse(workflowId, out var workflowGuid))
            {
                await Console.Error.WriteLineAsync($"Error: Invalid workflow ID format: {workflowId}");
                return ExitCodeValidationError;
            }

            return watch
                ? await WatchWorkflowStatusAsync(workflowGuid, interval)
                : await GetWorkflowStatusOnceAsync(workflowGuid);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
            return ExitCodeApiError;
        }
    }

    /// <summary>
    /// Gets workflow status once
    /// </summary>
    /// <param name="workflowId">Workflow ID</param>
    /// <returns>Exit code</returns>
    private async Task<int> GetWorkflowStatusOnceAsync(Guid workflowId)
    {
        var response = await ExecuteGetAsync<WorkflowStatusResponse>(
            $"/api/testworkflow/status/{workflowId}");

        if (response == null)
        {
            return ExitCodeApiError; // Error already handled
        }

        await WriteResponseAsync(response);
        return ExitCodeSuccess;
    }

    /// <summary>
    /// Watches workflow status continuously until completion
    /// </summary>
    /// <param name="workflowId">Workflow ID</param>
    /// <param name="interval">Polling interval in seconds</param>
    /// <returns>Exit code</returns>
    private async Task<int> WatchWorkflowStatusAsync(Guid workflowId, int interval)
    {
        var startTime = DateTime.UtcNow;
        var lastStatus = string.Empty;

        // Set up Ctrl+C handler
        using var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true; // Prevent immediate termination
            cancellationTokenSource.Cancel();
        };

        try
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                var response = await ExecuteGetAsync<WorkflowStatusResponse>(
                    $"/api/testworkflow/status/{workflowId}");

                if (response == null)
                {
                    return ExitCodeApiError;
                }

                // Display update if status changed or in text mode
                if (Configuration.OutputFormat == OutputFormat.Text)
                {
                    // Clear console for clean updates (optional, could be distracting)
                    // For now, just print a separator if status changed
                    if (response.Status != lastStatus)
                    {
                        if (!string.IsNullOrEmpty(lastStatus))
                        {
                            await Console.Out.WriteLineAsync(new string('-', 50));
                        }

                        await DisplayTextStatusAsync(response, DateTime.UtcNow - startTime);
                        lastStatus = response.Status;
                    }
                    else
                    {
                        // Show progress indicator
                        await Console.Out.WriteAsync(".");
                    }
                }
                else
                {
                    // In JSON mode, output each status update
                    await WriteResponseAsync(response);
                }

                // Check if workflow is in terminal state
                if (IsTerminalState(response.Status))
                {
                    if (Configuration.OutputFormat == OutputFormat.Text)
                    {
                        await Console.Out.WriteLineAsync();
                        await Console.Out.WriteLineAsync($"Workflow completed with status: {response.Status}");
                    }

                    // Return success only if workflow completed successfully
                    return response.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
                        ? ExitCodeSuccess
                        : ExitCodeApiError;
                }

                // Wait before next poll
                await Task.Delay(TimeSpan.FromSeconds(interval), cancellationTokenSource.Token);
            }

            // Ctrl+C was pressed
            if (!Configuration.Quiet)
            {
                await Console.Error.WriteLineAsync();
                await Console.Error.WriteLineAsync("Watch cancelled. Workflow continues to run on server.");
                await Console.Error.WriteLineAsync($"Check status with: cdc-cli workflow status {workflowId}");
            }
            return ExitCodeSuccess; // User cancelled watch, not an error
        }
        catch (OperationCanceledException)
        {
            // Graceful cancellation
            if (!Configuration.Quiet)
            {
                await Console.Error.WriteLineAsync();
                await Console.Error.WriteLineAsync("Watch cancelled by user.");
            }
            return ExitCodeSuccess;
        }
    }

    /// <summary>
    /// Displays workflow status in text format
    /// </summary>
    /// <param name="status">Workflow status</param>
    /// <param name="watchDuration">Duration of watch</param>
    /// <returns>Task</returns>
    private async Task DisplayTextStatusAsync(WorkflowStatusResponse status, TimeSpan watchDuration)
    {
        await Console.Out.WriteLineAsync($"Workflow ID: {status.WorkflowId}");
        await Console.Out.WriteLineAsync($"Name: {status.Name}");
        await Console.Out.WriteLineAsync($"Status: {GetColoredStatus(status.Status)}");

        if (!string.IsNullOrEmpty(status.CurrentPhase))
        {
            await Console.Out.WriteLineAsync($"Current Phase: {status.CurrentPhase}");
        }

        if (status.Progress.HasValue)
        {
            await Console.Out.WriteLineAsync($"Progress: {status.Progress:F1}%");
        }

        await Console.Out.WriteLineAsync($"Start Time: {status.StartTime:yyyy-MM-dd HH:mm:ss} UTC");

        if (status.Duration.HasValue)
        {
            await Console.Out.WriteLineAsync($"Duration: {FormatDuration(status.Duration.Value)}");
        }
        else
        {
            await Console.Out.WriteLineAsync($"Watch Duration: {FormatDuration(watchDuration)}");
        }

        if (status.EstimatedCompletion.HasValue)
        {
            var remaining = status.EstimatedCompletion.Value - DateTime.UtcNow;
            if (remaining.TotalSeconds > 0)
            {
                await Console.Out.WriteLineAsync($"Estimated Completion: {FormatDuration(remaining)}");
            }
        }

        if (status.Errors?.Any() == true)
        {
            await Console.Out.WriteLineAsync($"Errors: {status.Errors.Count}");
            foreach (var error in status.Errors.Take(3))
            {
                await Console.Out.WriteLineAsync($"  - {error}");
            }
        }
    }

    /// <summary>
    /// Gets colored status text for terminal output
    /// </summary>
    /// <param name="status">Status string</param>
    /// <returns>Colored status text</returns>
    private static string GetColoredStatus(string status)
    {
        // Note: Console colors might not work in all terminals
        return status.ToLowerInvariant() switch
        {
            "completed" => $"✓ {status}",
            "failed" => $"✗ {status}",
            "cancelled" => $"⊗ {status}",
            "running" => $"⟳ {status}",
            _ => status
        };
    }

    /// <summary>
    /// Checks if status is terminal (won't change)
    /// </summary>
    /// <param name="status">Status string</param>
    /// <returns>True if terminal state</returns>
    private static bool IsTerminalState(string status)
    {
        return status.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("Failed", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Formats duration for display
    /// </summary>
    /// <param name="duration">Duration to format</param>
    /// <returns>Formatted string</returns>
    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{duration.Hours}h {duration.Minutes}m {duration.Seconds}s";
        }
        else if (duration.TotalMinutes >= 1)
        {
            return $"{duration.Minutes}m {duration.Seconds}s";
        }
        else
        {
            return $"{duration.Seconds}s";
        }
    }
}

/// <summary>
/// Workflow status response
/// </summary>
public class WorkflowStatusResponse
{
    /// <summary>
    /// Gets or sets the workflow ID
    /// </summary>
    public Guid WorkflowId { get; set; }

    /// <summary>
    /// Gets or sets the workflow name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current phase
    /// </summary>
    public string? CurrentPhase { get; set; }

    /// <summary>
    /// Gets or sets the progress percentage (0-100)
    /// </summary>
    public double? Progress { get; set; }

    /// <summary>
    /// Gets or sets the start time
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the duration
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Gets or sets the estimated completion time
    /// </summary>
    public DateTime? EstimatedCompletion { get; set; }

    /// <summary>
    /// Gets or sets the errors
    /// </summary>
    public List<string>? Errors { get; set; }

    /// <summary>
    /// Gets or sets the message
    /// </summary>
    public string? Message { get; set; }
}
