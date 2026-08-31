using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Softbase.Cdc.Models;
using Softbase.Cdc.Trace;

namespace cdc_api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TraceController : ControllerBase
{
    private readonly ILogger<TraceController> _logger;
    private readonly ITraceManager _traceManager;
    private readonly ITraceDataProvider _traceDataProvider;

    public TraceController(
        ILogger<TraceController> logger,
        ITraceManager traceManager,
        ITraceDataProvider traceDataProvider)
    {
        _logger = logger;
        _traceManager = traceManager;
        _traceDataProvider = traceDataProvider;
    }

    /// <summary>
    /// Start a new trace session
    /// </summary>
    /// <param name="request">Trace start request</param>
    /// <returns>Trace session result</returns>
    [HttpPost("start")]
    public async Task<ActionResult<TraceApiResult>> StartTrace([FromBody] StartTraceRequest request)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.SessionName) ||
            string.IsNullOrWhiteSpace(request.DatabaseName))
        {
            return BadRequest(new { error = "Required fields are missing: SessionName and DatabaseName are required." });
        }

        _logger.LogInformation("Starting trace session {SessionName}", request.SessionName);

        // Create trace configuration
        var config = new TraceConfiguration
        {
            SessionName = request.SessionName,
            DatabaseName = request.DatabaseName,
            MaxFileSize = request.MaxFileSize ?? 100,
            MaxFiles = request.MaxFiles ?? 5,
            EventsToCapture = request.EventsToCapture ?? new List<string> { "sql_statement_completed" },
            FilterCriteria = request.FilterCriteria ?? new Dictionary<string, object>()
        };

        // Start the trace — let exceptions propagate to GlobalExceptionHandlerMiddleware
        // for a consistent error envelope across all Trace endpoints
        var session = await _traceManager.StartTraceAsync(config);

        if (session != null)
        {
            return Ok(new TraceApiResult
            {
                Success = true,
                Message = "Trace session started successfully",
                SessionId = session.SessionId,
                SessionName = session.SessionName,
                Status = new TraceStatus { State = TraceStatus.Running },
                StartedAt = session.StartTime
            });
        }

        return BadRequest(new { error = "Failed to start trace session." });
    }

    /// <summary>
    /// Stop a trace session
    /// </summary>
    /// <param name="request">Trace stop request</param>
    /// <returns>Trace session result</returns>
    [HttpPost("stop")]
    public async Task<ActionResult<TraceApiResult>> StopTrace([FromBody] StopTraceRequest request)
    {
        // Get session by name first to get the SessionId
        var session = await _traceDataProvider.GetTraceSessionByNameAsync(request.SessionName);
        if (session == null)
        {
            return NotFound($"Trace session '{request.SessionName}' not found");
        }

        var stoppedSession = await _traceManager.StopTraceAsync(session.SessionId);

        if (stoppedSession != null)
        {
            return Ok(new TraceApiResult
            {
                Success = true,
                Message = "Trace session stopped successfully",
                SessionId = stoppedSession.SessionId,
                SessionName = stoppedSession.SessionName,
                Status = new TraceStatus { State = TraceStatus.Stopped },
                StoppedAt = stoppedSession.EndTime ?? DateTime.UtcNow
            });
        }

        return BadRequest(new TraceApiResult
        {
            Success = false,
            Message = "Failed to stop trace session",
            SessionName = request.SessionName
        });
    }

    /// <summary>
    /// Get trace session status
    /// </summary>
    /// <param name="sessionName">Session name</param>
    /// <returns>Trace session status</returns>
    [HttpGet("status/{sessionName}")]
    public async Task<ActionResult<TraceSessionStatus>> GetTraceStatus(string sessionName)
    {
        _logger.LogInformation("Getting status for trace session {SessionName}", sessionName);

        TraceSession? session;
        try
        {
            session = await _traceDataProvider.GetTraceSessionByNameAsync(sessionName);
        }
        catch (NpgsqlException ex)
        {
            // If the trace table or session doesn't exist, treat as not found
            _logger.LogWarning(ex, "Database error getting trace session {SessionName}", sessionName);
            return NotFound(new { error = $"Trace session '{sessionName}' not found" });
        }

        if (session == null)
        {
            return NotFound(new { error = $"Trace session '{sessionName}' not found" });
        }

        // Use TraceManager's comprehensive status checking
        var traceStatus = await _traceManager.GetTraceStatusAsync(session.SessionId);

        return Ok(new TraceSessionStatus
        {
            SessionId = session.SessionId,
            SessionName = session.SessionName,
            DatabaseName = session.DatabaseName,
            Status = new TraceStatus { State = traceStatus.State },
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            EventCount = traceStatus.EventCount,
            Configuration = session.Configuration
        });
    }

    /// <summary>
    /// List all trace sessions
    /// </summary>
    /// <returns>List of trace sessions</returns>
    [HttpGet("sessions")]
    public async Task<ActionResult<List<TraceSessionSummary>>> ListTraceSessions()
    {
        _logger.LogInformation("Listing all trace sessions");

        var sessions = await _traceDataProvider.GetTraceSessionsAsync();

        var summaries = sessions.Select(s => new TraceSessionSummary
        {
            SessionId = s.SessionId,
            SessionName = s.SessionName,
            DatabaseName = s.DatabaseName,
            Status = new TraceStatus { State = s.Status },
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            EventCount = 0 // Will be populated separately if needed
        }).ToList();

        return Ok(summaries);
    }

    /// <summary>
    /// Export trace data to trace database
    /// </summary>
    /// <param name="request">Export trace request</param>
    /// <returns>Export result</returns>
    [HttpPost("export")]
    public async Task<ActionResult<TraceApiResult>> ExportTrace([FromBody] ExportTraceRequest request)
    {
        _logger.LogInformation("Exporting trace data for session {SessionName}", request.SessionName);

        // Get session by name to get SessionId
        var session = await _traceDataProvider.GetTraceSessionByNameAsync(request.SessionName);
        if (session == null)
        {
            return NotFound($"Trace session '{request.SessionName}' not found");
        }

        var exportPath = await _traceManager.ExportTraceDataAsync(session.SessionId, $"trace_export_{session.SessionId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");

        return Ok(new TraceApiResult
        {
            Success = true,
            Message = $"Trace data exported to {exportPath}",
            SessionName = request.SessionName,
            ExportedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get trace events for a session
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="limit">Maximum number of events to return</param>
    /// <param name="offset">Number of events to skip</param>
    /// <returns>List of trace events</returns>
    [HttpGet("sessions/{sessionId}/events")]
    public async Task<ActionResult<List<TraceEvent>>> GetTraceEvents(
        Guid sessionId,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0)
    {
        _logger.LogInformation("Getting trace events for session {SessionId}", sessionId);

        var events = await _traceDataProvider.GetTraceEventsAsync(sessionId, limit, offset);

        return Ok(events);
    }

    /// <summary>
    /// Delete a trace session and its data
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>Deletion result</returns>
    [HttpDelete("sessions/{sessionId}")]
    public async Task<ActionResult<TraceApiResult>> DeleteTraceSession(Guid sessionId)
    {
        _logger.LogInformation("Deleting trace session {SessionId}", sessionId);

        var session = await _traceDataProvider.GetTraceSessionAsync(sessionId);
        if (session == null)
        {
            return NotFound(new { error = $"Trace session '{sessionId}' not found" });
        }

        // Stop trace if running
        if (session.Status == "Active")
        {
            await _traceManager.StopTraceAsync(sessionId);
        }

        // Delete session and related data
        await _traceDataProvider.DeleteTraceSessionAsync(sessionId);

        return Ok(new TraceApiResult
        {
            Success = true,
            Message = "Trace session deleted successfully",
            SessionId = sessionId,
            SessionName = session.SessionName,
            DeletedAt = DateTime.UtcNow
        });
    }
}

// API Request/Response Models
public class StartTraceRequest
{
    public string SessionName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public int? MaxFileSize { get; set; }
    public int? MaxFiles { get; set; }
    public List<string>? EventsToCapture { get; set; }
    public Dictionary<string, object>? FilterCriteria { get; set; }
}

public class StopTraceRequest
{
    public string SessionName { get; set; } = string.Empty;
}

public class ExportTraceRequest
{
    public string SessionName { get; set; } = string.Empty;
}

public class TraceApiResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public TraceStatus? Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? StoppedAt { get; set; }
    public DateTime? ExportedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public class TraceSessionStatus
{
    public Guid SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public TraceStatus Status { get; set; } = new TraceStatus();
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int EventCount { get; set; }
    public TraceConfiguration? Configuration { get; set; }
}

public class TraceSessionSummary
{
    public Guid SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public TraceStatus Status { get; set; } = new TraceStatus();
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int EventCount { get; set; }
}
