using System.ComponentModel.DataAnnotations;

namespace cdc_api.Models;

/// <summary>
/// Request model for starting CDC operations
/// </summary>
public class StartCdcRequest
{
    /// <summary>
    /// Name for the CDC session
    /// </summary>
    [Required]
    public string SessionName { get; set; } = string.Empty;

    /// <summary>
    /// Optional: specific tables to include (e.g., ["dbo.Orders", "dbo.Customers"])
    /// If not provided, all user tables will be included
    /// </summary>
    public List<string>? TablesToInclude { get; set; }

    /// <summary>
    /// Optional: tables to exclude from CDC (e.g., ["dbo.AuditLog", "dbo.TempData"])
    /// </summary>
    public List<string>? TablesToExclude { get; set; }
}

/// <summary>
/// Request model for stopping CDC operations and capturing data
/// </summary>
public class StopCdcRequest
{
    /// <summary>
    /// Session name to save the capture under
    /// </summary>
    [Required]
    public string SessionName { get; set; } = string.Empty;

    /// <summary>
    /// Name for this specific capture
    /// </summary>
    [Required]
    public string CaptureName { get; set; } = string.Empty;

    /// <summary>
    /// Type of capture (Baseline, Replay, Optimized, etc.)
    /// </summary>
    public string CaptureType { get; set; } = "Baseline";
}

/// <summary>
/// Request model for capturing CDC data without stopping CDC
/// </summary>
public class CaptureCdcRequest
{
    /// <summary>
    /// Session name for the capture
    /// </summary>
    [Required]
    public string SessionName { get; set; } = string.Empty;

    /// <summary>
    /// Name for this specific capture
    /// </summary>
    [Required]
    public string CaptureName { get; set; } = string.Empty;

    /// <summary>
    /// Type of capture (Intermediate, Checkpoint, etc.)
    /// </summary>
    public string CaptureType { get; set; } = "Intermediate";
}

/// <summary>
/// Response model for CDC start operations
/// </summary>
public class StartCdcResponse
{
    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Session name that was created/used
    /// </summary>
    public string SessionName { get; set; } = string.Empty;

    /// <summary>
    /// Descriptive message about the operation
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// List of tables that had CDC enabled
    /// </summary>
    public List<string> TablesEnabled { get; set; } = new();

    /// <summary>
    /// List of tables that were skipped (excluded or had errors)
    /// </summary>
    public List<string> TablesSkipped { get; set; } = new();

    /// <summary>
    /// Any errors that occurred during the operation
    /// </summary>
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Response model for CDC stop operations
/// </summary>
public class StopCdcResponse
{
    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Session name that was used
    /// </summary>
    public string SessionName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the capture that was created
    /// </summary>
    public string CaptureName { get; set; } = string.Empty;

    /// <summary>
    /// Descriptive message about the operation
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// List of tables that had changes captured
    /// </summary>
    public List<string> TablesWithChanges { get; set; } = new();

    /// <summary>
    /// Total number of records captured across all tables
    /// </summary>
    public int TotalRecords { get; set; }

    /// <summary>
    /// Unique identifier for the capture in the database
    /// </summary>
    public string? CaptureId { get; set; }

    /// <summary>
    /// Any errors that occurred during the operation
    /// </summary>
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Response model for CDC capture operations
/// </summary>
public class CaptureCdcResponse
{
    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Session name that was used
    /// </summary>
    public string SessionName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the capture that was created
    /// </summary>
    public string CaptureName { get; set; } = string.Empty;

    /// <summary>
    /// Type of capture that was performed
    /// </summary>
    public string CaptureType { get; set; } = string.Empty;

    /// <summary>
    /// Descriptive message about the operation
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// List of tables that had changes captured
    /// </summary>
    public List<string> TablesWithChanges { get; set; } = new();

    /// <summary>
    /// Total number of records captured across all tables
    /// </summary>
    public int TotalRecords { get; set; }

    /// <summary>
    /// Unique identifier for the capture in the database
    /// </summary>
    public string? CaptureId { get; set; }

    /// <summary>
    /// Any errors that occurred during the operation
    /// </summary>
    public List<string> Errors { get; set; } = new();
}