using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Softbase.Cdc;

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

/// <summary>
/// Types of comparison failures
/// </summary>
public static class FailureTypes
{
    public const string MissingTable = "MissingTable";
    public const string ExtraTable = "ExtraTable";
    public const string MissingRecord = "MissingRecord";
    public const string ExtraRecord = "ExtraRecord";
    public const string FieldMismatch = "FieldMismatch";
    public const string OperationMismatch = "OperationMismatch";
    public const string RecordCountMismatch = "RecordCountMismatch";
}

/// <summary>
/// Internal model representing a capture's data for comparison
/// </summary>
internal class CaptureData
{
    public string CaptureName { get; set; } = string.Empty;
    public Dictionary<string, List<Dictionary<string, object>>> TableData { get; set; } = new();
}
