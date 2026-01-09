using System.CommandLine;
using System.CommandLine.Invocation;
using cdc_cli.Configuration;
using cdc_cli.Services;
using Microsoft.Extensions.Logging;

namespace cdc_cli.Commands.Workflow;

/// <summary>
/// Command to execute a complete test workflow
/// </summary>
public class WorkflowExecuteCommand : ApiCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowExecuteCommand"/> class
    /// </summary>
    /// <param name="apiClient">HTTP API client</param>
    /// <param name="jsonHandler">JSON handler</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">CLI configuration</param>
    public WorkflowExecuteCommand(
        ICdcApiClient apiClient,
        IJsonHandler jsonHandler,
        ILogger<WorkflowExecuteCommand> logger,
        CliConfiguration configuration)
        : base("execute", "Execute a complete test workflow", apiClient, jsonHandler, logger, configuration)
    {
        ConfigureCommand();
    }

    /// <summary>
    /// Configures command options and handler
    /// </summary>
    private void ConfigureCommand()
    {
        // Add options
        var fileOption = CreateFileOption();
        var dataOption = CreateDataOption();

        var asyncOption = new Option<bool>(
            aliases: new[] { "--async" },
            description: "Return immediately with workflow ID (async mode, default: false)")
        {
            IsRequired = false
        };

        var pollIntervalOption = new Option<int>(
            aliases: new[] { "--poll-interval" },
            description: "Status polling interval in seconds for synchronous mode (default: 5)",
            getDefaultValue: () => 5);

        AddOption(fileOption);
        AddOption(dataOption);
        AddOption(asyncOption);
        AddOption(pollIntervalOption);

        this.SetHandler(async (InvocationContext context) =>
        {
            var file = context.ParseResult.GetValueForOption(fileOption);
            var data = context.ParseResult.GetValueForOption(dataOption);
            var asyncMode = context.ParseResult.GetValueForOption(asyncOption);
            var pollInterval = context.ParseResult.GetValueForOption(pollIntervalOption);

            context.ExitCode = await ExecuteAsync(file, data, asyncMode, pollInterval);
        });
    }

    /// <summary>
    /// Executes the workflow execute command
    /// </summary>
    /// <param name="file">Path to workflow JSON file</param>
    /// <param name="data">Inline JSON data</param>
    /// <param name="asyncMode">Whether to run in async mode</param>
    /// <param name="pollInterval">Polling interval in seconds</param>
    /// <returns>Exit code</returns>
    private async Task<int> ExecuteAsync(
        string? file,
        string? data,
        bool asyncMode,
        int pollInterval)
    {
        try
        {
            // Get workflow request from file, data, or stdin
            var request = await JsonHandler.GetInputAsync<object>(data, file, new { }, CancellationToken.None);
            
            if (request == null)
            {
                await Console.Error.WriteLineAsync("Error: Workflow configuration required (use --file, --data, or stdin)");
                return ExitCodeValidationError;
            }

            // Log recommendation for complex workflows
            if (file == null && !Configuration.Quiet)
            {
                Logger.LogWarning("Workflow configurations are complex. Using --file is strongly recommended.");
            }

            // Make API call to execute workflow
            var response = await ExecuteApiCallAsync<object, WorkflowExecutionResult>("/api/testworkflow/execute", request);
            if (response == null)
            {
                return ExitCodeApiError; // Error already handled
            }

            // In async mode, just return the workflow ID
            if (asyncMode)
            {
                if (!Configuration.Quiet)
                {
                    await Console.Error.WriteLineAsync($"Workflow started: {response.WorkflowId}");
                    await Console.Error.WriteLineAsync($"Check status with: cdc-cli workflow status {response.WorkflowId}");
                }
                
                // Output just the essential info for scripting
                var asyncResult = new { response.WorkflowId, Status = "Running" };
                await WriteResponseAsync(asyncResult);
                return ExitCodeSuccess;
            }

            // Synchronous mode: the API call already waited for completion
            // The response contains the full result
            if (!Configuration.Quiet)
            {
                if (response.Success)
                {
                    await Console.Error.WriteLineAsync($"Workflow {response.WorkflowId} completed successfully");
                }
                else
                {
                    await Console.Error.WriteLineAsync($"Workflow {response.WorkflowId} failed: {response.ErrorMessage}");
                }
            }

            // Output the full result
            await WriteResponseAsync(response);
            
            // Return appropriate exit code
            return response.Success ? ExitCodeSuccess : ExitCodeApiError;
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
            return ExitCodeApiError;
        }
    }
}

/// <summary>
/// Workflow execution result
/// </summary>
public class WorkflowExecutionResult
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
    /// Gets or sets the start time
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the duration
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Gets or sets whether the workflow succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the workflow steps
    /// </summary>
    public List<WorkflowStepResult> Steps { get; set; } = new();

    /// <summary>
    /// Gets or sets the trace session ID
    /// </summary>
    public Guid? TraceSessionId { get; set; }
}

/// <summary>
/// Workflow step result
/// </summary>
public class WorkflowStepResult
{
    /// <summary>
    /// Gets or sets the step name
    /// </summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the start time
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the duration
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Gets or sets whether the step succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the step message
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
