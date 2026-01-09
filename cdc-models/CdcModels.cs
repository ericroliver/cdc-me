using System.ComponentModel.DataAnnotations;

namespace CdcModels;

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

/// <summary>
/// Request model for comparing two CDC captures
/// </summary>
public class CompareCapturesRequest
{
    /// <summary>
    /// Name of the baseline/expected capture
    /// </summary>
    [Required]
    public string BaselineCaptureName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the test capture to compare against baseline
    /// </summary>
    [Required]
    public string TestCaptureName { get; set; } = string.Empty;

    /// <summary>
    /// Optional list of field names to ignore during comparison (e.g., timestamps)
    /// </summary>
    public List<string>? FieldsToIgnore { get; set; }

    /// <summary>
    /// Whether to ignore LSN differences (default: true)
    /// </summary>
    public bool IgnoreLsnDifferences { get; set; } = true;
}

/// <summary>
/// Response model for capture comparison results
/// </summary>
public class CompareCapturesResponse
{
    /// <summary>
    /// Whether the captures match exactly (no failures)
    /// </summary>
    public bool IsMatch { get; set; }

    /// <summary>
    /// List of all comparison failures found
    /// </summary>
    public List<CaptureComparisonFailure> Failures { get; set; } = new();

    /// <summary>
    /// Summary statistics of the comparison
    /// </summary>
    public ComparisonSummary Summary { get; set; } = new();

    /// <summary>
    /// Any errors that occurred during comparison
    /// </summary>
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Detailed information about a specific comparison failure
/// </summary>
public class CaptureComparisonFailure
{
    /// <summary>
    /// Name of the table where the failure occurred
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// Type of failure (MissingTable, MissingRecord, FieldMismatch, etc.)
    /// </summary>
    public string FailureType { get; set; } = string.Empty;

    /// <summary>
    /// Primary key value of the affected record (if applicable)
    /// </summary>
    public object? PrimaryKey { get; set; }

    /// <summary>
    /// Name of the field that differs (if applicable)
    /// </summary>
    public string? FieldName { get; set; }

    /// <summary>
    /// Value from the baseline capture
    /// </summary>
    public object? BaselineValue { get; set; }

    /// <summary>
    /// Value from the test capture
    /// </summary>
    public object? TestValue { get; set; }

    /// <summary>
    /// Human-readable description of the failure
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Summary statistics for a capture comparison
/// </summary>
public class ComparisonSummary
{
    /// <summary>
    /// Number of tables compared
    /// </summary>
    public int TablesCompared { get; set; }

    /// <summary>
    /// Number of records compared across all tables
    /// </summary>
    public int RecordsCompared { get; set; }

    /// <summary>
    /// Number of fields compared across all records
    /// </summary>
    public int FieldsCompared { get; set; }

    /// <summary>
    /// Total number of failures found
    /// </summary>
    public int TotalFailures { get; set; }

    /// <summary>
    /// Number of tables that had failures
    /// </summary>
    public int TablesWithFailures { get; set; }

    /// <summary>
    /// Time taken to perform the comparison
    /// </summary>
    public TimeSpan ComparisonDuration { get; set; }
}
