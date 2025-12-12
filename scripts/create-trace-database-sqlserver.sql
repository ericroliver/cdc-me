-- Create trace database and schema for SQL Server
--
-- IMPORTANT: This script requires a connection string environment variable.
-- Create a .env file in the project root with:
-- SQLSERVER_CONNECTION_STRING=Server=your-host;Database=master;User Id=your-username;Password=your-password;TrustServerCertificate=true;
--
-- Use the SQLSERVER_CONNECTION_STRING environment variable to connect to your SQL Server.

-- Create trace database if it doesn't exist
IF NOT EXISTS (SELECT name
FROM sys.databases
WHERE name = 'CDC_TraceDB')
BEGIN
    CREATE DATABASE [CDC_TraceDB];
END
GO

USE [CDC_TraceDB];
GO

-- TraceSessions table
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[TraceSessions]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[TraceSessions]
    (
        [SessionId] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [SessionName] NVARCHAR(255) NOT NULL UNIQUE,
        [TestDatabase] NVARCHAR(128) NOT NULL,
        [SnapshotName] NVARCHAR(128) NULL,
        [StartTime] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        [EndTime] DATETIME2(7) NULL,
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Active',
        [CreatedBy] NVARCHAR(128) NOT NULL DEFAULT SUSER_NAME(),
        [Description] NVARCHAR(MAX) NULL,
        [Configuration] NVARCHAR(MAX) NULL
        -- JSON configuration
    );
END
GO

-- TraceEvents table
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[TraceEvents]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[TraceEvents]
    (
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
        [TsqlStack] NVARCHAR(MAX) NULL,
        [PlanHandle] VARBINARY(64) NULL,
        [RequestId] INT NULL,
        [ClientConnectionId] UNIQUEIDENTIFIER NULL,
        [TransactionId] BIGINT NULL,
        [Statement] NVARCHAR(MAX) NULL,
        [ExecutionOrder] BIGINT NOT NULL,
        [IsReplayable] BIT NOT NULL DEFAULT 1,
        FOREIGN KEY ([SessionId]) REFERENCES [TraceSessions]([SessionId]) ON DELETE CASCADE
    );

    CREATE INDEX IX_TraceEvents_SessionId_ExecutionOrder ON [dbo].[TraceEvents] ([SessionId], [ExecutionOrder]);
    CREATE INDEX IX_TraceEvents_EventTime ON [dbo].[TraceEvents] ([EventTime]);
END
GO

-- Add new columns to existing TraceEvents table if they don't exist
IF NOT EXISTS (SELECT *
FROM sys.columns
WHERE object_id = OBJECT_ID(N'[dbo].[TraceEvents]') AND name = 'TsqlStack')
BEGIN
    ALTER TABLE [dbo].[TraceEvents] ADD [TsqlStack] NVARCHAR(MAX) NULL;
END

IF NOT EXISTS (SELECT *
FROM sys.columns
WHERE object_id = OBJECT_ID(N'[dbo].[TraceEvents]') AND name = 'PlanHandle')
BEGIN
    ALTER TABLE [dbo].[TraceEvents] ADD [PlanHandle] VARBINARY(64) NULL;
END

IF NOT EXISTS (SELECT *
FROM sys.columns
WHERE object_id = OBJECT_ID(N'[dbo].[TraceEvents]') AND name = 'RequestId')
BEGIN
    ALTER TABLE [dbo].[TraceEvents] ADD [RequestId] INT NULL;
END

IF NOT EXISTS (SELECT *
FROM sys.columns
WHERE object_id = OBJECT_ID(N'[dbo].[TraceEvents]') AND name = 'ClientConnectionId')
BEGIN
    ALTER TABLE [dbo].[TraceEvents] ADD [ClientConnectionId] UNIQUEIDENTIFIER NULL;
END

IF NOT EXISTS (SELECT *
FROM sys.columns
WHERE object_id = OBJECT_ID(N'[dbo].[TraceEvents]') AND name = 'TransactionId')
BEGIN
    ALTER TABLE [dbo].[TraceEvents] ADD [TransactionId] BIGINT NULL;
END

IF NOT EXISTS (SELECT *
FROM sys.columns
WHERE object_id = OBJECT_ID(N'[dbo].[TraceEvents]') AND name = 'Statement')
BEGIN
    ALTER TABLE [dbo].[TraceEvents] ADD [Statement] NVARCHAR(MAX) NULL;
END
GO

-- CdcCaptures table
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[CdcCaptures]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CdcCaptures]
    (
        [CaptureId] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [SessionId] UNIQUEIDENTIFIER NOT NULL,
        [CaptureType] NVARCHAR(50) NOT NULL,
        -- Baseline, Replay, Optimized
        [CaptureTime] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        [TableName] NVARCHAR(256) NOT NULL,
        [CaptureData] NVARCHAR(MAX) NOT NULL,
        -- JSON data
        [RecordCount] INT NOT NULL,
        [DataHash] NVARCHAR(64) NULL,
        -- SHA256 hash for quick comparison
        FOREIGN KEY ([SessionId]) REFERENCES [TraceSessions]([SessionId]) ON DELETE CASCADE
    );

    CREATE INDEX IX_CdcCaptures_SessionId_CaptureType ON [dbo].[CdcCaptures] ([SessionId], [CaptureType]);
END
GO

-- ComparisonResults table
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[ComparisonResults]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ComparisonResults]
    (
        [ComparisonId] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [SessionId] UNIQUEIDENTIFIER NOT NULL,
        [LeftCaptureId] UNIQUEIDENTIFIER NOT NULL,
        [RightCaptureId] UNIQUEIDENTIFIER NOT NULL,
        [ComparisonTime] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        [TableName] NVARCHAR(256) NOT NULL,
        [IsMatch] BIT NOT NULL,
        [DifferenceCount] INT NOT NULL,
        [DifferenceData] NVARCHAR(MAX) NULL,
        -- JSON diff data
        [ComparisonNotes] NVARCHAR(MAX) NULL,
        FOREIGN KEY ([SessionId]) REFERENCES [TraceSessions]([SessionId]) ON DELETE CASCADE,
        FOREIGN KEY ([LeftCaptureId]) REFERENCES [CdcCaptures]([CaptureId]),
        FOREIGN KEY ([RightCaptureId]) REFERENCES [CdcCaptures]([CaptureId])
    );
END
GO

-- Display success message
SELECT 'CDC Trace Database schema created successfully!' as Result;
GO