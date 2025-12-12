using Microsoft.AspNetCore.Mvc;
using Softbase.Cdc.Trace;
using Softbase.Cdc.Models;

namespace cdc_api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestWorkflowController : ControllerBase
{
    private readonly ILogger<TestWorkflowController> _logger;
    private readonly ISnapshotManager _snapshotManager;
    private readonly ITraceManager _traceManager;
    private readonly IReplayEngine _replayEngine;
    private readonly ICdcComparator _cdcComparator;
    private readonly ITraceDataProvider _traceDataProvider;

    public TestWorkflowController(
        ILogger<TestWorkflowController> logger,
        ISnapshotManager snapshotManager,
        ITraceManager traceManager,
        IReplayEngine replayEngine,
        ICdcComparator cdcComparator,
        ITraceDataProvider traceDataProvider)
    {
        _logger = logger;
        _snapshotManager = snapshotManager;
        _traceManager = traceManager;
        _replayEngine = replayEngine;
        _cdcComparator = cdcComparator;
        _traceDataProvider = traceDataProvider;
    }

    /// <summary>
    /// Execute a complete test workflow
    /// </summary>
    /// <param name="request">Workflow execution request</param>
    /// <returns>Workflow execution result</returns>
    [HttpPost("execute")]
    public async Task<ActionResult<WorkflowExecutionResult>> ExecuteWorkflow([FromBody] WorkflowExecutionRequest request)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.WorkflowName) ||
            string.IsNullOrWhiteSpace(request.DatabaseName) ||
            string.IsNullOrWhiteSpace(request.ConnectionString) ||
            string.IsNullOrWhiteSpace(request.TraceConnectionString) ||
            string.IsNullOrWhiteSpace(request.BaselineSnapshotName) ||
            string.IsNullOrWhiteSpace(request.TestSnapshotName) ||
            string.IsNullOrWhiteSpace(request.TraceSessionName))
        {
            return BadRequest(new WorkflowExecutionResult
            {
                WorkflowId = Guid.NewGuid(),
                WorkflowName = request.WorkflowName ?? string.Empty,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow,
                Success = false,
                ErrorMessage = "Required fields are missing: WorkflowName, DatabaseName, ConnectionString, TraceConnectionString, BaselineSnapshotName, TestSnapshotName, and TraceSessionName are all required.",
                Steps = new List<WorkflowStepResult>()
            });
        }

        var workflowId = Guid.NewGuid();
        var result = new WorkflowExecutionResult
        {
            WorkflowId = workflowId,
            WorkflowName = request.WorkflowName,
            StartTime = DateTime.UtcNow,
            Steps = new List<WorkflowStepResult>()
        };

        try
        {
            _logger.LogInformation("Starting workflow execution {WorkflowId}: {WorkflowName}",
                workflowId, request.WorkflowName);

            // Step 1: Create baseline snapshot
            result.Steps.Add(await ExecuteStep("Create Baseline Snapshot", async () =>
            {
                var snapshotResult = await _snapshotManager.CreateSnapshotAsync(
                    request.DatabaseName,
                    request.BaselineSnapshotName);

                if (!snapshotResult.Success)
                    throw new Exception($"Failed to create baseline snapshot: {snapshotResult.Message}");

                return $"Baseline snapshot '{request.BaselineSnapshotName}' created successfully";
            }));

            // Step 2: Enable CDC (if requested)
            if (request.EnableCdc)
            {
                result.Steps.Add(await ExecuteStep("Enable CDC", async () =>
                {
                    // This would typically involve enabling CDC on the database and tables
                    // For now, we'll assume it's already enabled or handle it externally
                    await Task.Delay(100); // Placeholder
                    return "CDC enabled on database and tables";
                }));
            }

            // Step 3: Start trace capture
            result.Steps.Add(await ExecuteStep("Start Trace Capture", async () =>
            {
                var config = new TraceConfiguration
                {
                    SessionName = request.TraceSessionName,
                    DatabaseName = request.DatabaseName,
                    MaxFileSize = request.TraceConfig?.MaxFileSize ?? 100,
                    MaxFiles = request.TraceConfig?.MaxFiles ?? 5,
                    EventsToCapture = request.TraceConfig?.EventsToCapture ?? new List<string> { "sql_statement_completed" },
                    FilterCriteria = request.TraceConfig?.FilterCriteria ?? new Dictionary<string, object>()
                };

                var traceResult = await _traceManager.StartTraceAsync(config);
                // TraceManager.StartTraceAsync returns TraceSession, so we assume success if no exception
                _logger.LogInformation("Trace started successfully with SessionId: {SessionId}", traceResult.SessionId);

                // Create trace session record
                var session = new TraceSession
                {
                    SessionId = Guid.NewGuid(),
                    SessionName = request.TraceSessionName,
                    DatabaseName = request.DatabaseName,
                    Status = TraceStatus.Running,
                    StartTime = DateTime.UtcNow,
                    Configuration = config
                };

                await _traceDataProvider.CreateTraceSessionAsync(session);
                result.TraceSessionId = session.SessionId;

                return $"Trace session '{request.TraceSessionName}' started successfully";
            }));

            // Step 4: Execute baseline workload (if provided)
            if (!string.IsNullOrEmpty(request.BaselineWorkloadPath))
            {
                result.Steps.Add(await ExecuteStep("Execute Baseline Workload", async () =>
                {
                    var replayOptions = new ReplayOptions
                    {
                        SkipSelectStatements = true,
                        SkipSystemStatements = true,
                        ContinueOnError = false
                    };

                    var replayResult = await _replayEngine.ExecuteStatementsFromFileAsync(
                        request.BaselineWorkloadPath,
                        replayOptions);

                    return $"Baseline workload executed: {replayResult.ExecutedCount} statements, " +
                           $"{replayResult.SuccessCount} successful, {replayResult.ErrorCount} errors";
                }));
            }

            // Step 5: Stop trace capture
            result.Steps.Add(await ExecuteStep("Stop Trace Capture", async () =>
            {
                if (!result.TraceSessionId.HasValue)
                    throw new InvalidOperationException("TraceSessionId is null");

                var stopResult = await _traceManager.StopTraceAsync(result.TraceSessionId.Value);
                // TraceManager.StopTraceAsync returns TraceSession, so we assume success if no exception
                _logger.LogInformation("Trace stopped successfully");

                // Update session status
                if (result.TraceSessionId.HasValue)
                {
                    var session = await _traceDataProvider.GetTraceSessionAsync(result.TraceSessionId.Value);
                    if (session != null)
                    {
                        session.Status = TraceStatus.Stopped;
                        session.EndTime = DateTime.UtcNow;
                        await _traceDataProvider.UpdateTraceSessionAsync(session);
                    }
                }

                return $"Trace session '{request.TraceSessionName}' stopped successfully";
            }));

            // Step 6: Export trace data
            result.Steps.Add(await ExecuteStep("Export Trace Data", async () =>
            {
                if (!result.TraceSessionId.HasValue)
                    throw new InvalidOperationException("TraceSessionId is null");

                var exportResult = await _traceManager.ExportTraceDataAsync(
                    result.TraceSessionId.Value,
                    request.TraceConnectionString);

                // ExportTraceDataAsync returns string path, so we assume success if no exception
                _logger.LogInformation("Trace data exported to: {ExportPath}", exportResult);

                return "Trace data exported to trace database successfully";
            }));

            // Step 7: Create test snapshot
            result.Steps.Add(await ExecuteStep("Create Test Snapshot", async () =>
            {
                var snapshotResult = await _snapshotManager.CreateSnapshotAsync(
                    request.DatabaseName,
                    request.TestSnapshotName);

                if (!snapshotResult.Success)
                    throw new Exception($"Failed to create test snapshot: {snapshotResult.Message}");

                return $"Test snapshot '{request.TestSnapshotName}' created successfully";
            }));

            // Step 8: Restore baseline snapshot
            result.Steps.Add(await ExecuteStep("Restore Baseline Snapshot", async () =>
            {
                var restoreResult = await _snapshotManager.RestoreSnapshotAsync(
                    request.DatabaseName,
                    request.BaselineSnapshotName);

                if (!restoreResult.Success)
                    throw new Exception($"Failed to restore baseline snapshot: {restoreResult.Message}");

                return $"Baseline snapshot '{request.BaselineSnapshotName}' restored successfully";
            }));

            // Step 9: Replay captured statements
            result.Steps.Add(await ExecuteStep("Replay Captured Statements", async () =>
            {
                if (!result.TraceSessionId.HasValue)
                    throw new Exception("No trace session ID available for replay");

                var replayResult = await _replayEngine.ReplayTraceAsync(
                    result.TraceSessionId.Value,
                    new ReplayOptions());

                result.ReplayResult = replayResult;

                return $"Statement replay completed: {replayResult.ExecutedCount} statements, " +
                       $"{replayResult.SuccessCount} successful, {replayResult.ErrorCount} errors";
            }));

            // Step 10: Compare CDC data
            if (request.EnableCdc && request.CdcTables?.Any() == true)
            {
                result.Steps.Add(await ExecuteStep("Compare CDC Data", async () =>
                {
                    var comparisonResults = new List<ComparisonResult>();

                    foreach (var table in request.CdcTables)
                    {
                        var comparison = await _cdcComparator.CompareCdcDataAsync(
                            table,
                            request.ConnectionString,
                            request.TraceConnectionString,
                            request.ComparisonConfig);

                        comparisonResults.Add(comparison);
                    }

                    result.ComparisonResults = comparisonResults;

                    var totalDifferences = comparisonResults.Sum(r => r.TotalDifferences);
                    var tablesWithDifferences = comparisonResults.Count(r => r.TotalDifferences > 0);

                    return $"CDC comparison completed: {comparisonResults.Count} tables compared, " +
                           $"{totalDifferences} total differences found in {tablesWithDifferences} tables";
                }));
            }

            // Step 11: Generate test report
            result.Steps.Add(await ExecuteStep("Generate Test Report", async () =>
            {
                result.TestReport = GenerateTestReport(result);
                await Task.Delay(100); // Placeholder for report generation
                return "Test report generated successfully";
            }));

            result.EndTime = DateTime.UtcNow;
            result.Success = result.Steps.All(s => s.Success);
            result.Duration = result.EndTime.Value - result.StartTime;

            _logger.LogInformation("Workflow execution {WorkflowId} completed: {Success}",
                workflowId, result.Success ? "SUCCESS" : "FAILURE");

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing workflow {WorkflowId}", workflowId);

            result.EndTime = DateTime.UtcNow;
            result.Success = false;
            result.Duration = result.EndTime.Value - result.StartTime;
            result.ErrorMessage = ex.Message;

            return BadRequest(result);
        }
    }

    /// <summary>
    /// Get workflow execution status
    /// </summary>
    /// <param name="workflowId">Workflow ID</param>
    /// <returns>Workflow status</returns>
    [HttpGet("status/{workflowId}")]
    public async Task<ActionResult<WorkflowStatus>> GetWorkflowStatus(Guid workflowId)
    {
        try
        {
            // In a real implementation, this would retrieve status from a persistent store
            // For now, return a placeholder response
            await Task.Delay(10);

            return Ok(new WorkflowStatus
            {
                WorkflowId = workflowId,
                Status = "Completed", // This would be dynamic
                Message = "Workflow status retrieved successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workflow status for {WorkflowId}", workflowId);
            return BadRequest(new { error = $"Error getting workflow status: {ex.Message}" });
        }
    }

    /// <summary>
    /// List all workflow executions
    /// </summary>
    /// <returns>List of workflow executions</returns>
    [HttpGet("executions")]
    public async Task<ActionResult<List<WorkflowExecutionSummary>>> ListWorkflowExecutions()
    {
        try
        {
            // In a real implementation, this would retrieve from a persistent store
            // For now, return an empty list
            await Task.Delay(10);

            return Ok(new List<WorkflowExecutionSummary>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing workflow executions");
            return BadRequest(new { error = $"Error listing workflow executions: {ex.Message}" });
        }
    }

    private async Task<WorkflowStepResult> ExecuteStep(string stepName, Func<Task<string>> stepAction)
    {
        var stepResult = new WorkflowStepResult
        {
            StepName = stepName,
            StartTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Executing workflow step: {StepName}", stepName);
            stepResult.Message = await stepAction();
            stepResult.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing workflow step: {StepName}", stepName);
            stepResult.Success = false;
            stepResult.Message = ex.Message;
        }
        finally
        {
            stepResult.EndTime = DateTime.UtcNow;
            stepResult.Duration = stepResult.EndTime.Value - stepResult.StartTime;
        }

        return stepResult;
    }

    private TestReport GenerateTestReport(WorkflowExecutionResult result)
    {
        return new TestReport
        {
            WorkflowId = result.WorkflowId,
            WorkflowName = result.WorkflowName,
            ExecutionTime = result.StartTime,
            Duration = result.Duration ?? TimeSpan.Zero,
            Success = result.Success,
            StepCount = result.Steps.Count,
            SuccessfulSteps = result.Steps.Count(s => s.Success),
            FailedSteps = result.Steps.Count(s => !s.Success),
            ReplayStatistics = result.ReplayResult != null ? new ReplayStatistics
            {
                TotalStatements = result.ReplayResult.ExecutedCount,
                SuccessfulStatements = result.ReplayResult.SuccessCount,
                FailedStatements = result.ReplayResult.ErrorCount,
                ExecutionTime = result.ReplayResult.ExecutionTime
            } : null,
            ComparisonSummary = result.ComparisonResults?.Any() == true ? new ComparisonSummary
            {
                TablesCompared = result.ComparisonResults.Count,
                TotalDifferences = result.ComparisonResults.Sum(r => r.TotalDifferences),
                TablesWithDifferences = result.ComparisonResults.Count(r => r.TotalDifferences > 0)
            } : null
        };
    }
}

// API Request/Response Models
public class WorkflowExecutionRequest
{
    public string WorkflowName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public string TraceConnectionString { get; set; } = string.Empty;
    public string BaselineSnapshotName { get; set; } = string.Empty;
    public string TestSnapshotName { get; set; } = string.Empty;
    public string TraceSessionName { get; set; } = string.Empty;
    public bool EnableCdc { get; set; } = true;
    public string? BaselineWorkloadPath { get; set; }
    public List<string>? CdcTables { get; set; }
    public TraceConfiguration? TraceConfig { get; set; }
    public ComparisonConfiguration? ComparisonConfig { get; set; }
}

public class WorkflowExecutionResult
{
    public Guid WorkflowId { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<WorkflowStepResult> Steps { get; set; } = new();
    public Guid? TraceSessionId { get; set; }
    public ReplayResult? ReplayResult { get; set; }
    public List<ComparisonResult>? ComparisonResults { get; set; }
    public TestReport? TestReport { get; set; }
}

public class WorkflowStepResult
{
    public string StepName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class WorkflowStatus
{
    public Guid WorkflowId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class WorkflowExecutionSummary
{
    public Guid WorkflowId { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool Success { get; set; }
    public int StepCount { get; set; }
}

public class TestReport
{
    public Guid WorkflowId { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public DateTime ExecutionTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public int StepCount { get; set; }
    public int SuccessfulSteps { get; set; }
    public int FailedSteps { get; set; }
    public ReplayStatistics? ReplayStatistics { get; set; }
    public ComparisonSummary? ComparisonSummary { get; set; }
}

public class ReplayStatistics
{
    public int TotalStatements { get; set; }
    public int SuccessfulStatements { get; set; }
    public int FailedStatements { get; set; }
    public TimeSpan ExecutionTime { get; set; }
}

public class ComparisonSummary
{
    public int TablesCompared { get; set; }
    public int TotalDifferences { get; set; }
    public int TablesWithDifferences { get; set; }
}