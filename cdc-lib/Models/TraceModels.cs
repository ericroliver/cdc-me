using System;
using System.Collections.Generic;

namespace Softbase.Cdc.Models
{
    public class TraceConfiguration
    {
        public string DatabaseName { get; set; } = string.Empty;
        public string SessionName { get; set; } = string.Empty;
        public string[] EventTypes { get; set; } = { "sql_batch_completed", "rpc_completed" };
        public string[] ExcludePatterns { get; set; } = { "SELECT%", "sys.%", "INFORMATION_SCHEMA%" };
        public int RingBufferSizeMB { get; set; } = 64;
        public bool CaptureStatementText { get; set; } = true;
        public bool CapturePerformanceMetrics { get; set; } = true;
        public string Description { get; set; } = string.Empty;

        // API compatibility properties
        public string ConnectionString { get; set; } = string.Empty;
        public int MaxFileSize { get; set; } = 100;
        public int MaxFiles { get; set; } = 5;
        public List<string> EventsToCapture { get; set; } = new() { "sql_statement_completed" };
        public Dictionary<string, object> FilterCriteria { get; set; } = new();
    }

    public class ComparisonConfiguration
    {
        public string[] ExcludedColumns { get; set; } =
        {
            "__$start_lsn", "__$end_lsn", "__$seqval", "__$update_mask",
            "LastModified", "CreatedDate", "Timestamp", "ModifiedDate"
        };

        public TimeSpan DateTimeToleranceWindow { get; set; } = TimeSpan.FromHours(24);
        public bool IgnoreIdentityColumns { get; set; } = true;
        public bool IgnoreComputedColumns { get; set; } = true;
        public string[] CustomExcludePatterns { get; set; } = Array.Empty<string>();
    }

    public class TraceStorageConfiguration
    {
        public string Provider { get; set; } = "PostgreSQL"; // PostgreSQL | SqlServer
        public string ConnectionString { get; set; } = string.Empty;
        public bool AutoCreateSchema { get; set; } = true;
        public int CommandTimeout { get; set; } = 30;
        public string SchemaName { get; set; } = "public"; // PostgreSQL schema or SQL Server schema
    }

    public class SnapshotInfo
    {
        public string SnapshotName { get; set; } = string.Empty;
        public string SourceDatabase { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; }
        public long SizeInBytes { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class SnapshotResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? SnapshotName { get; set; }
        public string? ErrorDetails { get; set; }
    }

    public class DatabaseFileInfo
    {
        public string LogicalName { get; set; } = string.Empty;
        public string PhysicalName { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
    }

    public class TraceSession
    {
        public Guid SessionId { get; set; }
        public string SessionName { get; set; } = string.Empty;
        public string TestDatabase { get; set; } = string.Empty;
        public string TestConnectionString { get; set; } = string.Empty;
        public string? SnapshotName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public string? Description { get; set; }
        public TraceConfiguration? Configuration { get; set; }

        // API compatibility property
        public string DatabaseName { get; set; } = string.Empty;
    }

    public class TraceEvent
    {
        public long EventId { get; set; }
        public Guid SessionId { get; set; }
        public DateTime EventTime { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string? DatabaseName { get; set; }
        public string? LoginName { get; set; }
        public string? ApplicationName { get; set; }
        public string? HostName { get; set; }
        public int? Spid { get; set; }
        public long? Duration { get; set; }
        public long? CpuTime { get; set; }
        public long? Reads { get; set; }
        public long? Writes { get; set; }
        public string? SqlText { get; set; }
        public long ExecutionOrder { get; set; }
        public bool IsReplayable { get; set; } = true;
    }

    public class TraceStatus
    {
        public Guid SessionId { get; set; }
        public string State { get; set; } = "Unknown"; // Running | Stopped | NotFound | Failed
        public string? LastError { get; set; }
        public DateTime? StartedAt { get; set; }
        public int EventCount { get; set; }

        // API compatibility constants
        public const string Running = "Running";
        public const string Stopped = "Stopped";
        public const string NotFound = "NotFound";
        public const string Failed = "Failed";
    }

    public class CdcCapture
    {
        public Guid CaptureId { get; set; }
        public Guid SessionId { get; set; }
        public string CaptureType { get; set; } = string.Empty; // Baseline, Replay, Optimized
        public DateTime CaptureTime { get; set; }
        public string TableName { get; set; } = string.Empty;
        public string CaptureData { get; set; } = string.Empty; // JSON data
        public int RecordCount { get; set; }
        public string? DataHash { get; set; } // SHA256 hash for quick comparison
    }

    public class ReplayOptions
    {
        public bool SkipSelectStatements { get; set; } = true;
        public bool SkipSystemStatements { get; set; } = true;
        public bool ContinueOnError { get; set; } = false;
        public int MaxConcurrentConnections { get; set; } = 1;
        public TimeSpan StatementTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public string[] AdditionalExcludePatterns { get; set; } = Array.Empty<string>();
    }

    public class ReplayStatement
    {
        public long EventId { get; set; }
        public string SqlText { get; set; } = string.Empty;
        public DateTime OriginalEventTime { get; set; }
        public long ExecutionOrder { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class StatementResult
    {
        public long EventId { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public int RowsAffected { get; set; }
    }

    public class ReplayResult
    {
        public Guid SessionId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int TotalStatements { get; set; }
        public int SuccessfulStatements { get; set; }
        public int FailedStatements { get; set; }
        public int SkippedStatements { get; set; }
        public List<ReplayError> Errors { get; set; } = new();

        // API compatibility properties
        public int ExecutedCount => TotalStatements;
        public int SuccessCount => SuccessfulStatements;
        public int ErrorCount => FailedStatements;
        public TimeSpan ExecutionTime => EndTime - StartTime;
    }

    public class ReplayError
    {
        public long EventId { get; set; }
        public string SqlText { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime ErrorTime { get; set; }
    }

    public class ComparisonResult
    {
        public Guid ComparisonId { get; set; }
        public Guid SessionId { get; set; }
        public Guid LeftCaptureId { get; set; }
        public Guid RightCaptureId { get; set; }
        public DateTime ComparisonTime { get; set; }
        public Dictionary<string, TableComparison> TableComparisons { get; set; } = new();
        public bool OverallMatch { get; set; }
        public int TotalDifferences { get; set; }
        public string? ComparisonNotes { get; set; }
    }

    public class TableComparison
    {
        public string TableName { get; set; } = string.Empty;
        public bool IsMatch { get; set; }
        public int DifferenceCount { get; set; }
        public List<RowDifference> Differences { get; set; } = new();
    }

    public class RowDifference
    {
        public string Key { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // New, Changed, Deleted
        public Dictionary<string, object> LeftValues { get; set; } = new();
        public Dictionary<string, object> RightValues { get; set; } = new();
        public Dictionary<string, FieldDifference> FieldDifferences { get; set; } = new();
    }

    public class FieldDifference
    {
        public object? LeftValue { get; set; }
        public object? RightValue { get; set; }
        public string DifferenceType { get; set; } = string.Empty;
    }

    public class DifferenceReport
    {
        public Guid ComparisonId { get; set; }
        public DateTime GeneratedTime { get; set; }
        public string Summary { get; set; } = string.Empty;
        public Dictionary<string, TableDifferenceReport> TableReports { get; set; } = new();
    }

    public class TableDifferenceReport
    {
        public string TableName { get; set; } = string.Empty;
        public int TotalRows { get; set; }
        public int ChangedRows { get; set; }
        public int NewRows { get; set; }
        public int DeletedRows { get; set; }
        public List<string> AffectedColumns { get; set; } = new();
    }

    public class WorkflowConfiguration
    {
        public string DatabaseName { get; set; } = string.Empty;
        public string SessionName { get; set; } = string.Empty;
        public string SnapshotName { get; set; } = string.Empty;
        public TraceConfiguration TraceConfig { get; set; } = new();
        public ComparisonConfiguration ComparisonConfig { get; set; } = new();
        public ReplayOptions ReplayOptions { get; set; } = new();
        public string Description { get; set; } = string.Empty;
    }

    public class WorkflowResult
    {
        public Guid SessionId { get; set; }
        public string SessionName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; } = string.Empty; // Success, Failed, PartialSuccess
        public List<string> Steps { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public ComparisonResult? ComparisonResult { get; set; }
        public ReplayResult? ReplayResult { get; set; }
    }

    public class ComparisonRequest
    {
        public Guid LeftCaptureId { get; set; }
        public Guid RightCaptureId { get; set; }
        public ComparisonConfiguration? Configuration { get; set; }
        public string? Notes { get; set; }
    }
}