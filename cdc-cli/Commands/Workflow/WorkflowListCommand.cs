using System.CommandLine;
using System.CommandLine.Invocation;
using cdc_cli.Configuration;
using cdc_cli.Services;
using Microsoft.Extensions.Logging;

namespace cdc_cli.Commands.Workflow;

/// <summary>
/// Command to list workflow executions
/// </summary>
public class WorkflowListCommand : ApiCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowListCommand"/> class
    /// </summary>
    /// <param name="apiClient">HTTP API client</param>
    /// <param name="jsonHandler">JSON handler</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">CLI configuration</param>
    public WorkflowListCommand(
        ICdcApiClient apiClient,
        IJsonHandler jsonHandler,
        ILogger<WorkflowListCommand> logger,
        CliConfiguration configuration)
        : base("list", "List workflow executions", apiClient, jsonHandler, logger, configuration)
    {
        ConfigureCommand();
    }

    /// <summary>
    /// Configures command options and handler
    /// </summary>
    private void ConfigureCommand()
    {
        var statusOption = new Option<string?>(
            aliases: new[] { "--status", "-s" },
            description: "Filter by status (pending, running, completed, failed, cancelled)");

        var limitOption = new Option<int>(
            aliases: new[] { "--limit", "-l" },
            description: "Maximum number of results (default: 50, max: 500)",
            getDefaultValue: () => 50);

        var sinceOption = new Option<string?>(
            aliases: new[] { "--since" },
            description: "Show only workflows since date (ISO 8601 format: 2024-01-01 or 2024-01-01T12:00:00Z)");

        AddOption(statusOption);
        AddOption(limitOption);
        AddOption(sinceOption);

        this.SetHandler(async (InvocationContext context) =>
        {
            var status = context.ParseResult.GetValueForOption(statusOption);
            var limit = context.ParseResult.GetValueForOption(limitOption);
            var since = context.ParseResult.GetValueForOption(sinceOption);

            context.ExitCode = await ExecuteAsync(status, limit, since);
        });
    }

    /// <summary>
    /// Executes the workflow list command
    /// </summary>
    /// <param name="status">Status filter</param>
    /// <param name="limit">Result limit</param>
    /// <param name="since">Date filter</param>
    /// <returns>Exit code</returns>
    private async Task<int> ExecuteAsync(string? status, int limit, string? since)
    {
        try
        {
            // Validate limit
            if (limit < 1 || limit > 500)
            {
                await Console.Error.WriteLineAsync("Error: Limit must be between 1 and 500");
                return ExitCodeValidationError;
            }

            // Validate status if provided
            if (!string.IsNullOrWhiteSpace(status))
            {
                var validStatuses = new[] { "pending", "running", "completed", "failed", "cancelled" };
                if (!validStatuses.Contains(status.ToLowerInvariant()))
                {
                    await Console.Error.WriteLineAsync($"Error: Invalid status. Valid values: {string.Join(", ", validStatuses)}");
                    return ExitCodeValidationError;
                }
            }

            // Validate and parse since date if provided
            DateTime? sinceDate = null;
            if (!string.IsNullOrWhiteSpace(since))
            {
                if (!DateTime.TryParse(since, out var parsedDate))
                {
                    await Console.Error.WriteLineAsync($"Error: Invalid date format: {since}");
                    return ExitCodeValidationError;
                }
                sinceDate = parsedDate;
            }

            // Build query string
            var queryParams = new List<string>();
            if (!string.IsNullOrWhiteSpace(status))
            {
                queryParams.Add($"status={Uri.EscapeDataString(status)}");
            }
            queryParams.Add($"limit={limit}");
            if (sinceDate.HasValue)
            {
                queryParams.Add($"since={Uri.EscapeDataString(sinceDate.Value.ToString("o"))}");
            }

            var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;

            // Make API call
            var response = await ExecuteGetAsync<List<WorkflowExecutionSummary>>(
                $"/api/testworkflow/executions{queryString}");

            if (response == null)
            {
                return ExitCodeApiError; // Error already handled
            }

            // Handle empty results
            if (!response.Any())
            {
                if (Configuration.OutputFormat == OutputFormat.Text)
                {
                    await Console.Out.WriteLineAsync("No workflows found.");
                }
                else
                {
                    await WriteResponseAsync(response);
                }
                return ExitCodeSuccess;
            }

            // Output based on format
            if (Configuration.OutputFormat == OutputFormat.Text)
            {
                await DisplayTextListAsync(response);
            }
            else
            {
                await WriteResponseAsync(response);
            }

            return ExitCodeSuccess;
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
            return ExitCodeApiError;
        }
    }

    /// <summary>
    /// Displays workflow list in text table format
    /// </summary>
    /// <param name="workflows">List of workflow summaries</param>
    /// <returns>Task</returns>
    private async Task DisplayTextListAsync(List<WorkflowExecutionSummary> workflows)
    {
        // Calculate column widths
        const int idWidth = 36;
        const int nameWidth = 30;
        const int statusWidth = 12;
        const int startWidth = 20;
        const int durationWidth = 12;

        // Print header
        await Console.Out.WriteLineAsync(new string('=', idWidth + nameWidth + statusWidth + startWidth + durationWidth + 16));
        await Console.Out.WriteLineAsync(
            $"{"ID",-idWidth} | {"Name",-nameWidth} | {"Status",-statusWidth} | {"Start Time",-startWidth} | {"Duration",-durationWidth}");
        await Console.Out.WriteLineAsync(new string('-', idWidth + nameWidth + statusWidth + startWidth + durationWidth + 16));

        // Print rows
        foreach (var workflow in workflows)
        {
            var id = workflow.WorkflowId.ToString();
            var name = TruncateString(workflow.WorkflowName, nameWidth);
            var status = GetStatusIndicator(workflow.Status, workflow.Success);
            var startTime = workflow.StartTime.ToString("yyyy-MM-dd HH:mm:ss");
            var duration = workflow.EndTime.HasValue
                ? FormatDuration(workflow.EndTime.Value - workflow.StartTime)
                : "Running...";

            await Console.Out.WriteLineAsync(
                $"{id,-idWidth} | {name,-nameWidth} | {status,-statusWidth} | {startTime,-startWidth} | {duration,-durationWidth}");
        }

        await Console.Out.WriteLineAsync(new string('=', idWidth + nameWidth + statusWidth + startWidth + durationWidth + 16));
        await Console.Out.WriteLineAsync($"Total: {workflows.Count} workflow(s)");
    }

    /// <summary>
    /// Gets status indicator with symbol
    /// </summary>
    /// <param name="status">Status text</param>
    /// <param name="success">Whether workflow succeeded</param>
    /// <returns>Status indicator string</returns>
    private static string GetStatusIndicator(string status, bool success)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return success ? "✓ Completed" : "✗ Failed";
        }

        return status.ToLowerInvariant() switch
        {
            "completed" => "✓ Completed",
            "failed" => "✗ Failed",
            "cancelled" => "⊗ Cancelled",
            "running" => "⟳ Running",
            "pending" => "○ Pending",
            _ => status
        };
    }

    /// <summary>
    /// Truncates string to specified length
    /// </summary>
    /// <param name="value">String to truncate</param>
    /// <param name="maxLength">Maximum length</param>
    /// <returns>Truncated string</returns>
    private static string TruncateString(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength - 3) + "...";
    }

    /// <summary>
    /// Formats duration for display
    /// </summary>
    /// <param name="duration">Duration to format</param>
    /// <returns>Formatted string</returns>
    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
        {
            return $"{(int)duration.TotalDays}d {duration.Hours}h";
        }
        else if (duration.TotalHours >= 1)
        {
            return $"{duration.Hours}h {duration.Minutes}m";
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
/// Workflow execution summary
/// </summary>
public class WorkflowExecutionSummary
{
    /// <summary>
    /// Gets or sets the workflow ID
    /// </summary>
    public Guid WorkflowId { get; set; }

    /// <summary>
    /// Gets or sets the workflow name
    /// </summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the start time
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Gets or sets whether the workflow succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the step count
    /// </summary>
    public int StepCount { get; set; }

    /// <summary>
    /// Gets or sets the progress percentage (0-100)
    /// </summary>
    public double? Progress { get; set; }
}
