# SQL Tracing and Replicatable Testing - Implementation Plan

## Overview

This document provides detailed implementation guidance for building the SQL tracing and replicatable testing environment. It includes specific code examples, technical specifications, and step-by-step implementation instructions.

## Database Platform Support

The system supports:

- **Test Database**: SQL Server (where CDC and snapshots are managed)
- **Trace Database**: PostgreSQL or SQL Server (configurable, stores trace data and CDC captures)

This separation allows for isolation of trace data from test environment and cross-platform compatibility.

## Phase 1: Core Infrastructure Implementation

### 1.1 Database Schema Setup

#### PostgreSQL Trace Database Initialization Script

Create `scripts/create-trace-database-postgresql.sql`:

```sql
-- Create trace database and schema
CREATE DATABASE cdc_tracedb;

\c cdc_tracedb;

-- Create tables
CREATE TABLE trace_sessions (
    session_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_name VARCHAR(255) NOT NULL UNIQUE,
    test_database VARCHAR(128) NOT NULL,
    snapshot_name VARCHAR(128),
    start_time TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    end_time TIMESTAMP WITH TIME ZONE,
    status VARCHAR(50) NOT NULL DEFAULT 'Active',
    created_by VARCHAR(128) NOT NULL DEFAULT current_user,
    description TEXT,
    configuration JSONB -- JSON configuration
);

CREATE TABLE trace_events (
    event_id BIGSERIAL PRIMARY KEY,
    session_id UUID NOT NULL REFERENCES trace_sessions(session_id) ON DELETE CASCADE,
    event_time TIMESTAMP WITH TIME ZONE NOT NULL,
    event_name VARCHAR(128) NOT NULL,
    database_name VARCHAR(128),
    login_name VARCHAR(128),
    application_name VARCHAR(256),
    host_name VARCHAR(128),
    spid INTEGER,
    duration BIGINT,
    cpu_time BIGINT,
    reads BIGINT,
    writes BIGINT,
    sql_text TEXT,
    execution_order BIGINT NOT NULL,
    is_replayable BOOLEAN NOT NULL DEFAULT true
);

CREATE INDEX idx_trace_events_session_execution ON trace_events(session_id, execution_order);
CREATE INDEX idx_trace_events_event_time ON trace_events(event_time);

CREATE TABLE cdc_capture_headers (
    capture_header_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL REFERENCES trace_sessions(session_id) ON DELETE CASCADE,
    capture_name VARCHAR(255) NOT NULL,
    capture_type VARCHAR(50) NOT NULL,
    capture_time TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    tables_to_include JSONB,
    tables_to_exclude JSONB,
    tables_enabled JSONB NOT NULL,
    tables_skipped JSONB,
    total_records INTEGER NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'Completed',
    error_messages JSONB,
    created_by VARCHAR(128) NOT NULL DEFAULT current_user,
    description TEXT
);

CREATE INDEX idx_cdc_capture_headers_session ON cdc_capture_headers(session_id);
CREATE INDEX idx_cdc_capture_headers_capture_type ON cdc_capture_headers(capture_type);

CREATE TABLE cdc_captures (
    capture_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    capture_header_id UUID NOT NULL REFERENCES cdc_capture_headers(capture_header_id) ON DELETE CASCADE,
    table_name VARCHAR(256) NOT NULL,
    capture_data JSONB NOT NULL,
    record_count INTEGER NOT NULL,
    data_hash VARCHAR(64)
);

CREATE INDEX idx_cdc_captures_header ON cdc_captures(capture_header_id);
CREATE INDEX idx_cdc_captures_table_name ON cdc_captures(table_name);

CREATE TABLE comparison_results (
    comparison_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL REFERENCES trace_sessions(session_id) ON DELETE CASCADE,
    left_capture_id UUID NOT NULL REFERENCES cdc_captures(capture_id),
    right_capture_id UUID NOT NULL REFERENCES cdc_captures(capture_id),
    comparison_time TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    table_name VARCHAR(256) NOT NULL,
    is_match BOOLEAN NOT NULL,
    difference_count INTEGER NOT NULL,
    difference_data JSONB, -- JSON diff data
    comparison_notes TEXT
);
```

#### SQL Server Trace Database Initialization Script

Create `scripts/create-trace-database-sqlserver.sql`:

```sql
-- Create trace database if it doesn't exist
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'CDC_TraceDB')
BEGIN
    CREATE DATABASE [CDC_TraceDB];
END
GO

USE [CDC_TraceDB];
GO

-- TraceSessions table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TraceSessions]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[TraceSessions] (
        [SessionId] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [SessionName] NVARCHAR(255) NOT NULL UNIQUE,
        [TestDatabase] NVARCHAR(128) NOT NULL,
        [TestConnectionString] NVARCHAR(1000) NOT NULL,
        [SnapshotName] NVARCHAR(128) NULL,
        [StartTime] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        [EndTime] DATETIME2(7) NULL,
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Active',
        [CreatedBy] NVARCHAR(128) NOT NULL DEFAULT SUSER_NAME(),
        [Description] NVARCHAR(MAX) NULL,
        [Configuration] NVARCHAR(MAX) NULL -- JSON configuration
    );
END
GO

-- TraceEvents table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TraceEvents]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[TraceEvents] (
        [EventId] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [SessionId] UNIQUEIDENTIFIER NOT NULL,
        [EventTime] DATETIME2(7) NOT NULL,
        [EventName] NVARCHAR(128) NOT NULL,
        [DatabaseName] NVARCHAR(128) NULL,
        [LoginName] NVARCHAR(128) NULL,
        [ApplicationName] NVARCHAR(256) NULL,
        [HostName] NVARCHAR(128) NULL,
        [SPID] INT NULL,
        [Duration] BIGINT NULL,
        [CpuTime] BIGINT NULL,
        [Reads] BIGINT NULL,
        [Writes] BIGINT NULL,
        [SqlText] NVARCHAR(MAX) NULL,
        [ExecutionOrder] BIGINT NOT NULL,
        [IsReplayable] BIT NOT NULL DEFAULT 1,
        FOREIGN KEY ([SessionId]) REFERENCES [TraceSessions]([SessionId]) ON DELETE CASCADE
    );

    CREATE INDEX IX_TraceEvents_SessionId_ExecutionOrder ON [dbo].[TraceEvents] ([SessionId], [ExecutionOrder]);
    CREATE INDEX IX_TraceEvents_EventTime ON [dbo].[TraceEvents] ([EventTime]);
END
GO

-- CdcCaptures table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CdcCaptures]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CdcCaptures] (
        [CaptureId] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [SessionId] UNIQUEIDENTIFIER NOT NULL,
        [CaptureType] NVARCHAR(50) NOT NULL, -- Baseline, Replay, Optimized
        [CaptureTime] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        [TableName] NVARCHAR(256) NOT NULL,
        [CaptureData] NVARCHAR(MAX) NOT NULL, -- JSON data
        [RecordCount] INT NOT NULL,
        [DataHash] NVARCHAR(64) NULL, -- SHA256 hash for quick comparison
        FOREIGN KEY ([SessionId]) REFERENCES [TraceSessions]([SessionId]) ON DELETE CASCADE
    );

    CREATE INDEX IX_CdcCaptures_SessionId_CaptureType ON [dbo].[CdcCaptures] ([SessionId], [CaptureType]);
END
GO

-- ComparisonResults table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ComparisonResults]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ComparisonResults] (
        [ComparisonId] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [SessionId] UNIQUEIDENTIFIER NOT NULL,
        [LeftCaptureId] UNIQUEIDENTIFIER NOT NULL,
        [RightCaptureId] UNIQUEIDENTIFIER NOT NULL,
        [ComparisonTime] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        [TableName] NVARCHAR(256) NOT NULL,
        [IsMatch] BIT NOT NULL,
        [DifferenceCount] INT NOT NULL,
        [DifferenceData] NVARCHAR(MAX) NULL, -- JSON diff data
        [ComparisonNotes] NVARCHAR(MAX) NULL,
        FOREIGN KEY ([SessionId]) REFERENCES [TraceSessions]([SessionId]) ON DELETE CASCADE,
        FOREIGN KEY ([LeftCaptureId]) REFERENCES [CdcCaptures]([CaptureId]),
        FOREIGN KEY ([RightCaptureId]) REFERENCES [CdcCaptures]([CaptureId])
    );
END
GO
```

### 1.2 Core Library Extensions

#### Configuration Models (`cdc-lib/Models/TraceModels.cs`)

```csharp
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

    public class SnapshotInfo
    {
        public string SnapshotName { get; set; } = string.Empty;
        public string SourceDatabase { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; }
        public long SizeInBytes { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class TraceSession
    {
        public Guid SessionId { get; set; }
        public string SessionName { get; set; } = string.Empty;
        public string TestDatabase { get; set; } = string.Empty;
        public string? SnapshotName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public string? Description { get; set; }
        public TraceConfiguration? Configuration { get; set; }
    }

    public class TraceStatus
    {
        public Guid SessionId { get; set; }
        public string State { get; set; } = "Unknown"; // Running | Stopped | NotFound | Failed
        public string? LastError { get; set; }
        public DateTime? StartedAt { get; set; }
        public int EventCount { get; set; }
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
}
```

#### Snapshot Manager (`cdc-lib/Trace/SnapshotManager.cs`)

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Softbase.Cdc.Models;

namespace Softbase.Cdc.Trace
{
    public class SnapshotManager
    {
        private readonly SimpleDac _dac;
        private readonly ILogger _logger;

        public SnapshotManager(SimpleDac dac, ILogger logger)
        {
            _dac = dac ?? throw new ArgumentNullException(nameof(dac));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> CreateSnapshotAsync(string databaseName, string snapshotName)
        {
            _logger.LogInformation("Creating snapshot {SnapshotName} for database {DatabaseName}", snapshotName, databaseName);

            // Check if snapshot already exists
            if (await SnapshotExistsAsync(snapshotName))
            {
                throw new InvalidOperationException($"Snapshot '{snapshotName}' already exists. Only one snapshot is allowed.");
            }

            // Get database file paths
            var dataFiles = await GetDatabaseFilesAsync(databaseName);

            // Build CREATE DATABASE AS SNAPSHOT statement
            var snapshotFiles = new List<string>();
            foreach (var file in dataFiles)
            {
                var snapshotFileName = $"{file.LogicalName}_snapshot.ss";
                var snapshotFilePath = Path.Combine(Path.GetDirectoryName(file.PhysicalName) ?? "", snapshotFileName);
                snapshotFiles.Add($"(NAME = '{file.LogicalName}', FILENAME = '{snapshotFilePath}')");
            }

            var createSnapshotSql = $@"
                CREATE DATABASE [{snapshotName}] ON
                {string.Join(",\n", snapshotFiles)}
                AS SNAPSHOT OF [{databaseName}];";

            try
            {
                await _dac.ExecuteCommandAsync(createSnapshotSql);
                _logger.LogInformation("Successfully created snapshot {SnapshotName}", snapshotName);
                return snapshotName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create snapshot {SnapshotName}", snapshotName);
                throw;
            }
        }

        public async Task<bool> SnapshotExistsAsync(string snapshotName)
        {
            const string checkSnapshotSql = @"
                SELECT COUNT(1)
                FROM sys.databases
                WHERE name = @snapshotName AND source_database_id IS NOT NULL";

            var count = await _dac.ExecuteScalarAsync<int>(checkSnapshotSql, new Dictionary<string, object>
            {
                ["@snapshotName"] = snapshotName
            });

            return count > 0;
        }

        public async Task RestoreFromSnapshotAsync(string databaseName, string snapshotName)
        {
            _logger.LogInformation("Restoring database {DatabaseName} from snapshot {SnapshotName}", databaseName, snapshotName);

            if (!await SnapshotExistsAsync(snapshotName))
            {
                throw new InvalidOperationException($"Snapshot '{snapshotName}' does not exist.");
            }

            // Set database to single user mode
            var setSingleUserSql = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;";

            // Restore from snapshot
            var restoreSql = $"RESTORE DATABASE [{databaseName}] FROM DATABASE_SNAPSHOT = '{snapshotName}';";

            // Set back to multi user mode
            var setMultiUserSql = $"ALTER DATABASE [{databaseName}] SET MULTI_USER;";

            try
            {
                await _dac.ExecuteCommandAsync(setSingleUserSql);
                await _dac.ExecuteCommandAsync(restoreSql);
                await _dac.ExecuteCommandAsync(setMultiUserSql);

                _logger.LogInformation("Successfully restored database {DatabaseName} from snapshot {SnapshotName}", databaseName, snapshotName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore database {DatabaseName} from snapshot {SnapshotName}", databaseName, snapshotName);

                // Try to set back to multi user mode if restore failed
                try
                {
                    await _dac.ExecuteCommandAsync(setMultiUserSql);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogError(cleanupEx, "Failed to reset database to multi-user mode after restore failure");
                }

                throw;
            }
        }

        public async Task DropSnapshotAsync(string snapshotName)
        {
            _logger.LogInformation("Dropping snapshot {SnapshotName}", snapshotName);

            if (!await SnapshotExistsAsync(snapshotName))
            {
                _logger.LogWarning("Snapshot {SnapshotName} does not exist, nothing to drop", snapshotName);
                return;
            }

            var dropSnapshotSql = $"DROP DATABASE [{snapshotName}];";

            try
            {
                await _dac.ExecuteCommandAsync(dropSnapshotSql);
                _logger.LogInformation("Successfully dropped snapshot {SnapshotName}", snapshotName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to drop snapshot {SnapshotName}", snapshotName);
                throw;
            }
        }

        public async Task<SnapshotInfo> GetSnapshotInfoAsync(string snapshotName)
        {
            const string getSnapshotInfoSql = @"
                SELECT
                    d.name AS SnapshotName,
                    sd.name AS SourceDatabase,
                    d.create_date AS CreatedTime,
                    ISNULL(SUM(mf.size * 8 * 1024), 0) AS SizeInBytes,
                    d.state_desc AS Status
                FROM sys.databases d
                LEFT JOIN sys.databases sd ON d.source_database_id = sd.database_id
                LEFT JOIN sys.master_files mf ON d.database_id = mf.database_id
                WHERE d.name = @snapshotName AND d.source_database_id IS NOT NULL
                GROUP BY d.name, sd.name, d.create_date, d.state_desc";

            return await _dac.ExecuteReaderAsync(getSnapshotInfoSql, reader =>
            {
                if (reader.Read())
                {
                    return new SnapshotInfo
                    {
                        SnapshotName = reader.GetString("SnapshotName"),
                        SourceDatabase = reader.GetString("SourceDatabase"),
                        CreatedTime = reader.GetDateTime("CreatedTime"),
                        SizeInBytes = reader.GetInt64("SizeInBytes"),
                        Status = reader.GetString("Status")
                    };
                }
                throw new InvalidOperationException($"Snapshot '{snapshotName}' not found.");
            }, new Dictionary<string, object> { ["@snapshotName"] = snapshotName });
        }

        private async Task<List<DatabaseFile>> GetDatabaseFilesAsync(string databaseName)
        {
            const string getFilesSql = @"
                SELECT
                    name AS LogicalName,
                    physical_name AS PhysicalName,
                    type_desc AS FileType
                FROM sys.master_files
                WHERE database_id = DB_ID(@databaseName) AND type = 0"; // Only data files for snapshots

            return await _dac.ExecuteReaderAsync(getFilesSql, reader =>
            {
                var files = new List<DatabaseFile>();
                while (reader.Read())
                {
                    files.Add(new DatabaseFile
                    {
                        LogicalName = reader.GetString("LogicalName"),
                        PhysicalName = reader.GetString("PhysicalName"),
                        FileType = reader.GetString("FileType")
                    });
                }
                return files;
            }, new Dictionary<string, object> { ["@databaseName"] = databaseName });
        }

        private class DatabaseFile
        {
            public string LogicalName { get; set; } = string.Empty;
            public string PhysicalName { get; set; } = string.Empty;
            public string FileType { get; set; } = string.Empty;
        }
    }
}
```

## Phase 2: CLI Integration

### 2.1 Snapshot CLI Commands

#### Snapshot Command (`cdc-proto/Commands/SnapshotCommand.cs`)

```csharp
using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.Logging;
using Softbase.Cdc.Trace;

namespace Softbase.Commands
{
    public class SnapshotCommand : Command
    {
        private readonly SimpleDac _dac;
        private readonly ILogger _logger;

        public SnapshotCommand(SimpleDac dac, ILoggerFactory factory)
            : base("snapshot", "Manage database snapshots")
        {
            _dac = dac;
            _logger = factory.CreateLogger<SnapshotCommand>();

            // Create subcommand
            var createCommand = new Command("create", "Create a database snapshot");
            createCommand.AddOption(new Option<string>("--database", "Source database name") { IsRequired = true });
            createCommand.AddOption(new Option<string>("--name", "Snapshot name") { IsRequired = true });
            createCommand.Handler = CommandHandler.Create<string, string>(CreateSnapshot);
            this.AddCommand(createCommand);

            // Restore subcommand
            var restoreCommand = new Command("restore", "Restore database from snapshot");
            restoreCommand.AddOption(new Option<string>("--database", "Target database name") { IsRequired = true });
            restoreCommand.AddOption(new Option<string>("--snapshot", "Snapshot name") { IsRequired = true });
            restoreCommand.Handler = CommandHandler.Create<string, string>(RestoreSnapshot);
            this.AddCommand(restoreCommand);

            // List subcommand
            var listCommand = new Command("list", "List all snapshots");
            listCommand.Handler = CommandHandler.Create(ListSnapshots);
            this.AddCommand(listCommand);

            // Drop subcommand
            var dropCommand = new Command("drop", "Drop a snapshot");
            dropCommand.AddOption(new Option<string>("--name", "Snapshot name") { IsRequired = true });
            dropCommand.Handler = CommandHandler.Create<string>(DropSnapshot);
            this.AddCommand(dropCommand);
        }

        private async Task<int> CreateSnapshot(string database, string name)
        {
            try
            {
                var snapshotManager = new SnapshotManager(_dac, _logger);
                var snapshotName = await snapshotManager.CreateSnapshotAsync(database, name);
                Console.WriteLine($"Successfully created snapshot: {snapshotName}");
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create snapshot");
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        private async Task<int> RestoreSnapshot(string database, string snapshot)
        {
            try
            {
                var snapshotManager = new SnapshotManager(_dac, _logger);
                await snapshotManager.RestoreFromSnapshotAsync(database, snapshot);
                Console.WriteLine($"Successfully restored database {database} from snapshot {snapshot}");
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore snapshot");
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        private async Task<int> ListSnapshots()
        {
            try
            {
                // Implementation for listing snapshots
                Console.WriteLine("Listing snapshots...");
                // TODO: Implement snapshot listing
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list snapshots");
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        private async Task<int> DropSnapshot(string name)
        {
            try
            {
                var snapshotManager = new SnapshotManager(_dac, _logger);
                await snapshotManager.DropSnapshotAsync(name);
                Console.WriteLine($"Successfully dropped snapshot: {name}");
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to drop snapshot");
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }
    }
}
```

## Implementation Timeline

### Week 1-2: Core Infrastructure

- Set up trace database schema
- Implement SnapshotManager class
- Create basic configuration models
- Add async support to SimpleDac

### Week 3-4: Trace Management

- Implement TraceManager class with Extended Events
- Create trace data export functionality
- Add trace filtering and processing logic
- Implement basic CLI snapshot commands

### Week 5-6: Replay Engine

- Implement ReplayEngine class
- Add SQL statement filtering and preparation
- Create replay execution with error handling
- Add CLI trace commands

### Week 7-8: CDC Comparison

- Implement CdcComparator class
- Add data normalization logic
- Create comparison algorithms
- Add CLI replay and comparison commands

### Week 9-10: Web API Integration

- Create API controllers for all operations
- Add comprehensive error handling
- Implement request/response models
- Add API documentation

### Week 11-12: Testing and Documentation

- Create integration tests
- Add unit tests for core components
- Update all documentation
- Create usage examples and guides

## Next Steps

1. **Review and Approve Design** - Ensure the design meets all requirements
2. **Set Up Development Environment** - Prepare development database and tools
3. **Begin Phase 1 Implementation** - Start with database schema and core models
4. **Iterative Development** - Implement and test each component incrementally
5. **Integration Testing** - Test complete workflows as components are completed

This implementation plan provides a solid foundation for building the SQL tracing and replicatable testing environment while maintaining consistency with the existing CDC framework.
