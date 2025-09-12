using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Data.SqlClient;
using Newtonsoft.Json;
using Softbase.Cdc.Models;
using System.Data;

namespace Softbase.Cdc.Trace
{
    public class SqlServerTraceProvider : ITraceDataProvider
    {
        private readonly string _connectionString;
        private readonly ILogger _logger;
        private readonly TraceStorageConfiguration _config;

        public SqlServerTraceProvider(TraceStorageConfiguration config, ILogger logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _connectionString = config.ConnectionString ?? throw new ArgumentNullException(nameof(config.ConnectionString));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TraceSession> CreateSessionAsync(TraceConfiguration config)
        {
            const string insertSql = @"
                INSERT INTO [TraceSessions] ([SessionName], [TestDatabase], [TestConnectionString], [SnapshotName], [Description], [Configuration])
                OUTPUT INSERTED.[SessionId], INSERTED.[StartTime], INSERTED.[Status], INSERTED.[CreatedBy]
                VALUES (@sessionName, @testDatabase, @testConnectionString, @snapshotName, @description, @configuration)";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(insertSql, connection);
            command.Parameters.AddWithValue("@sessionName", config.SessionName);
            command.Parameters.AddWithValue("@testDatabase", config.DatabaseName);
            command.Parameters.AddWithValue("@testConnectionString", ""); // Will be set by caller
            command.Parameters.AddWithValue("@snapshotName", (object?)null ?? DBNull.Value);
            command.Parameters.AddWithValue("@description", config.Description ?? "");
            command.Parameters.AddWithValue("@configuration", JsonConvert.SerializeObject(config));

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new TraceSession
                {
                    SessionId = reader.GetGuid(0),
                    SessionName = config.SessionName,
                    TestDatabase = config.DatabaseName,
                    TestConnectionString = "",
                    SnapshotName = null,
                    StartTime = reader.GetDateTime(1),
                    EndTime = null,
                    Status = reader.GetString(2),
                    CreatedBy = reader.GetString(3),
                    Description = config.Description,
                    Configuration = config
                };
            }

            throw new InvalidOperationException("Failed to create trace session");
        }

        public async Task<TraceSession> GetSessionAsync(Guid sessionId)
        {
            const string selectSql = @"
                SELECT [SessionId], [SessionName], [TestDatabase], [TestConnectionString], [SnapshotName], 
                       [StartTime], [EndTime], [Status], [CreatedBy], [Description], [Configuration]
                FROM [TraceSessions] 
                WHERE [SessionId] = @sessionId";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(selectSql, connection);
            command.Parameters.AddWithValue("@sessionId", sessionId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapTraceSession(reader);
            }

            throw new InvalidOperationException($"Trace session {sessionId} not found");
        }

        public async Task<TraceSession> GetSessionByNameAsync(string sessionName)
        {
            const string selectSql = @"
                SELECT [SessionId], [SessionName], [TestDatabase], [TestConnectionString], [SnapshotName], 
                       [StartTime], [EndTime], [Status], [CreatedBy], [Description], [Configuration]
                FROM [TraceSessions] 
                WHERE [SessionName] = @sessionName";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(selectSql, connection);
            command.Parameters.AddWithValue("@sessionName", sessionName);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapTraceSession(reader);
            }

            throw new InvalidOperationException($"Trace session '{sessionName}' not found");
        }

        public async Task<IEnumerable<TraceSession>> GetActiveSessionsAsync()
        {
            const string selectSql = @"
                SELECT [SessionId], [SessionName], [TestDatabase], [TestConnectionString], [SnapshotName], 
                       [StartTime], [EndTime], [Status], [CreatedBy], [Description], [Configuration]
                FROM [TraceSessions] 
                WHERE [Status] = 'Active'
                ORDER BY [StartTime] DESC";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(selectSql, connection);
            using var reader = await command.ExecuteReaderAsync();

            var sessions = new List<TraceSession>();
            while (await reader.ReadAsync())
            {
                sessions.Add(MapTraceSession(reader));
            }

            return sessions;
        }

        public async Task UpdateSessionAsync(TraceSession session)
        {
            const string updateSql = @"
                UPDATE [TraceSessions] 
                SET [TestConnectionString] = @testConnectionString,
                    [SnapshotName] = @snapshotName,
                    [EndTime] = @endTime,
                    [Status] = @status,
                    [Description] = @description,
                    [Configuration] = @configuration
                WHERE [SessionId] = @sessionId";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(updateSql, connection);
            command.Parameters.AddWithValue("@sessionId", session.SessionId);
            command.Parameters.AddWithValue("@testConnectionString", session.TestConnectionString ?? "");
            command.Parameters.AddWithValue("@snapshotName", (object?)session.SnapshotName ?? DBNull.Value);
            command.Parameters.AddWithValue("@endTime", (object?)session.EndTime ?? DBNull.Value);
            command.Parameters.AddWithValue("@status", session.Status);
            command.Parameters.AddWithValue("@description", session.Description ?? "");
            command.Parameters.AddWithValue("@configuration", JsonConvert.SerializeObject(session.Configuration));

            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteSessionAsync(Guid sessionId)
        {
            const string deleteSql = "DELETE FROM [TraceSessions] WHERE [SessionId] = @sessionId";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(deleteSql, connection);
            command.Parameters.AddWithValue("@sessionId", sessionId);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<long> SaveTraceEventAsync(TraceEvent traceEvent)
        {
            const string insertSql = @"
                INSERT INTO [TraceEvents] ([SessionId], [EventTime], [EventName], [DatabaseName], [LoginName], 
                                         [ApplicationName], [HostName], [SPID], [Duration], [CpuTime], [Reads], [Writes], 
                                         [SqlText], [ExecutionOrder], [IsReplayable])
                OUTPUT INSERTED.[EventId]
                VALUES (@sessionId, @eventTime, @eventName, @databaseName, @loginName, @applicationName, 
                        @hostName, @spid, @duration, @cpuTime, @reads, @writes, @sqlText, @executionOrder, @isReplayable)";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(insertSql, connection);
            command.Parameters.AddWithValue("@sessionId", traceEvent.SessionId);
            command.Parameters.AddWithValue("@eventTime", traceEvent.EventTime);
            command.Parameters.AddWithValue("@eventName", traceEvent.EventName);
            command.Parameters.AddWithValue("@databaseName", (object?)traceEvent.DatabaseName ?? DBNull.Value);
            command.Parameters.AddWithValue("@loginName", (object?)traceEvent.LoginName ?? DBNull.Value);
            command.Parameters.AddWithValue("@applicationName", (object?)traceEvent.ApplicationName ?? DBNull.Value);
            command.Parameters.AddWithValue("@hostName", (object?)traceEvent.HostName ?? DBNull.Value);
            command.Parameters.AddWithValue("@spid", (object?)traceEvent.Spid ?? DBNull.Value);
            command.Parameters.AddWithValue("@duration", (object?)traceEvent.Duration ?? DBNull.Value);
            command.Parameters.AddWithValue("@cpuTime", (object?)traceEvent.CpuTime ?? DBNull.Value);
            command.Parameters.AddWithValue("@reads", (object?)traceEvent.Reads ?? DBNull.Value);
            command.Parameters.AddWithValue("@writes", (object?)traceEvent.Writes ?? DBNull.Value);
            command.Parameters.AddWithValue("@sqlText", (object?)traceEvent.SqlText ?? DBNull.Value);
            command.Parameters.AddWithValue("@executionOrder", traceEvent.ExecutionOrder);
            command.Parameters.AddWithValue("@isReplayable", traceEvent.IsReplayable);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt64(result);
        }

        public async Task<IEnumerable<TraceEvent>> GetTraceEventsAsync(Guid sessionId)
        {
            const string selectSql = @"
                SELECT [EventId], [SessionId], [EventTime], [EventName], [DatabaseName], [LoginName], 
                       [ApplicationName], [HostName], [SPID], [Duration], [CpuTime], [Reads], [Writes], 
                       [SqlText], [ExecutionOrder], [IsReplayable]
                FROM [TraceEvents] 
                WHERE [SessionId] = @sessionId 
                ORDER BY [ExecutionOrder]";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(selectSql, connection);
            command.Parameters.AddWithValue("@sessionId", sessionId);

            using var reader = await command.ExecuteReaderAsync();
            var events = new List<TraceEvent>();

            while (await reader.ReadAsync())
            {
                events.Add(MapTraceEvent(reader));
            }

            return events;
        }

        public async Task<IEnumerable<TraceEvent>> GetTraceEventsAsync(Guid sessionId, int skip, int take)
        {
            const string selectSql = @"
                SELECT [EventId], [SessionId], [EventTime], [EventName], [DatabaseName], [LoginName], 
                       [ApplicationName], [HostName], [SPID], [Duration], [CpuTime], [Reads], [Writes], 
                       [SqlText], [ExecutionOrder], [IsReplayable]
                FROM [TraceEvents] 
                WHERE [SessionId] = @sessionId 
                ORDER BY [ExecutionOrder]
                OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(selectSql, connection);
            command.Parameters.AddWithValue("@sessionId", sessionId);
            command.Parameters.AddWithValue("@skip", skip);
            command.Parameters.AddWithValue("@take", take);

            using var reader = await command.ExecuteReaderAsync();
            var events = new List<TraceEvent>();

            while (await reader.ReadAsync())
            {
                events.Add(MapTraceEvent(reader));
            }

            return events;
        }

        public async Task<int> GetTraceEventCountAsync(Guid sessionId)
        {
            const string countSql = "SELECT COUNT(*) FROM [TraceEvents] WHERE [SessionId] = @sessionId";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(countSql, connection);
            command.Parameters.AddWithValue("@sessionId", sessionId);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<Guid> SaveCdcCaptureAsync(CdcCapture capture)
        {
            const string insertSql = @"
                INSERT INTO [CdcCaptures] ([SessionId], [CaptureType], [TableName], [CaptureData], [RecordCount], [DataHash])
                OUTPUT INSERTED.[CaptureId]
                VALUES (@sessionId, @captureType, @tableName, @captureData, @recordCount, @dataHash)";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(insertSql, connection);
            command.Parameters.AddWithValue("@sessionId", capture.SessionId);
            command.Parameters.AddWithValue("@captureType", capture.CaptureType);
            command.Parameters.AddWithValue("@tableName", capture.TableName);
            command.Parameters.AddWithValue("@captureData", capture.CaptureData);
            command.Parameters.AddWithValue("@recordCount", capture.RecordCount);
            command.Parameters.AddWithValue("@dataHash", (object?)capture.DataHash ?? DBNull.Value);

            var result = await command.ExecuteScalarAsync();
            return (Guid)result;
        }

        public async Task<CdcCapture> GetCdcCaptureAsync(Guid captureId)
        {
            const string selectSql = @"
                SELECT [CaptureId], [SessionId], [CaptureType], [CaptureTime], [TableName], [CaptureData], [RecordCount], [DataHash]
                FROM [CdcCaptures] 
                WHERE [CaptureId] = @captureId";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(selectSql, connection);
            command.Parameters.AddWithValue("@captureId", captureId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapCdcCapture(reader);
            }

            throw new InvalidOperationException($"CDC capture {captureId} not found");
        }

        public async Task<IEnumerable<CdcCapture>> GetCdcCapturesAsync(Guid sessionId)
        {
            const string selectSql = @"
                SELECT [CaptureId], [SessionId], [CaptureType], [CaptureTime], [TableName], [CaptureData], [RecordCount], [DataHash]
                FROM [CdcCaptures] 
                WHERE [SessionId] = @sessionId 
                ORDER BY [CaptureTime]";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(selectSql, connection);
            command.Parameters.AddWithValue("@sessionId", sessionId);

            using var reader = await command.ExecuteReaderAsync();
            var captures = new List<CdcCapture>();

            while (await reader.ReadAsync())
            {
                captures.Add(MapCdcCapture(reader));
            }

            return captures;
        }

        public async Task<IEnumerable<CdcCapture>> GetCdcCapturesByTypeAsync(Guid sessionId, string captureType)
        {
            const string selectSql = @"
                SELECT [CaptureId], [SessionId], [CaptureType], [CaptureTime], [TableName], [CaptureData], [RecordCount], [DataHash]
                FROM [CdcCaptures] 
                WHERE [SessionId] = @sessionId AND [CaptureType] = @captureType
                ORDER BY [CaptureTime]";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(selectSql, connection);
            command.Parameters.AddWithValue("@sessionId", sessionId);
            command.Parameters.AddWithValue("@captureType", captureType);

            using var reader = await command.ExecuteReaderAsync();
            var captures = new List<CdcCapture>();

            while (await reader.ReadAsync())
            {
                captures.Add(MapCdcCapture(reader));
            }

            return captures;
        }

        public async Task<Guid> SaveComparisonResultAsync(ComparisonResult result)
        {
            const string insertSql = @"
                INSERT INTO [ComparisonResults] ([SessionId], [LeftCaptureId], [RightCaptureId], [TableName], 
                                                [IsMatch], [DifferenceCount], [DifferenceData], [ComparisonNotes])
                OUTPUT INSERTED.[ComparisonId]
                VALUES (@sessionId, @leftCaptureId, @rightCaptureId, @tableName, @isMatch, @differenceCount, 
                        @differenceData, @comparisonNotes)";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(insertSql, connection);
            command.Parameters.AddWithValue("@sessionId", result.SessionId);
            command.Parameters.AddWithValue("@leftCaptureId", result.LeftCaptureId);
            command.Parameters.AddWithValue("@rightCaptureId", result.RightCaptureId);
            command.Parameters.AddWithValue("@tableName", ""); // Will be populated from table comparisons
            command.Parameters.AddWithValue("@isMatch", result.OverallMatch);
            command.Parameters.AddWithValue("@differenceCount", result.TotalDifferences);
            command.Parameters.AddWithValue("@differenceData", JsonConvert.SerializeObject(result.TableComparisons));
            command.Parameters.AddWithValue("@comparisonNotes", (object?)result.ComparisonNotes ?? DBNull.Value);

            var comparisonId = (Guid)await command.ExecuteScalarAsync();
            result.ComparisonId = comparisonId;
            return comparisonId;
        }

        public async Task<ComparisonResult> GetComparisonResultAsync(Guid comparisonId)
        {
            const string selectSql = @"
                SELECT [ComparisonId], [SessionId], [LeftCaptureId], [RightCaptureId], [ComparisonTime], 
                       [TableName], [IsMatch], [DifferenceCount], [DifferenceData], [ComparisonNotes]
                FROM [ComparisonResults] 
                WHERE [ComparisonId] = @comparisonId";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(selectSql, connection);
            command.Parameters.AddWithValue("@comparisonId", comparisonId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapComparisonResult(reader);
            }

            throw new InvalidOperationException($"Comparison result {comparisonId} not found");
        }

        public async Task<IEnumerable<ComparisonResult>> GetComparisonResultsAsync(Guid sessionId)
        {
            const string selectSql = @"
                SELECT [ComparisonId], [SessionId], [LeftCaptureId], [RightCaptureId], [ComparisonTime], 
                       [TableName], [IsMatch], [DifferenceCount], [DifferenceData], [ComparisonNotes]
                FROM [ComparisonResults] 
                WHERE [SessionId] = @sessionId 
                ORDER BY [ComparisonTime] DESC";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(selectSql, connection);
            command.Parameters.AddWithValue("@sessionId", sessionId);

            using var reader = await command.ExecuteReaderAsync();
            var results = new List<ComparisonResult>();

            while (await reader.ReadAsync())
            {
                results.Add(MapComparisonResult(reader));
            }

            return results;
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to test SQL Server connection");
                return false;
            }
        }

        public async Task InitializeSchemaAsync()
        {
            if (!_config.AutoCreateSchema)
                return;

            // Schema initialization would be done via the SQL script
            // This method could run the schema creation script if needed
            _logger.LogInformation("SQL Server schema initialization - assuming schema exists");
            await Task.CompletedTask;
        }

        public async Task<string> GetProviderInfoAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("SELECT @@VERSION", connection);
            var version = await command.ExecuteScalarAsync();

            return $"SQL Server Provider - {version}";
        }

        private TraceSession MapTraceSession(IDataReader reader)
        {
            var configJson = reader.IsDBNull(10) ? null : reader.GetString(10);
            TraceConfiguration config = null;
            if (!string.IsNullOrEmpty(configJson))
            {
                try
                {
                    config = JsonConvert.DeserializeObject<TraceConfiguration>(configJson);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize trace configuration");
                }
            }

            return new TraceSession
            {
                SessionId = reader.GetGuid(0),
                SessionName = reader.GetString(1),
                TestDatabase = reader.GetString(2),
                TestConnectionString = reader.GetString(3),
                SnapshotName = reader.IsDBNull(4) ? null : reader.GetString(4),
                StartTime = reader.GetDateTime(5),
                EndTime = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                Status = reader.GetString(7),
                CreatedBy = reader.GetString(8),
                Description = reader.IsDBNull(9) ? null : reader.GetString(9),
                Configuration = config
            };
        }

        private TraceEvent MapTraceEvent(IDataReader reader)
        {
            return new TraceEvent
            {
                EventId = reader.GetInt64(0),
                SessionId = reader.GetGuid(1),
                EventTime = reader.GetDateTime(2),
                EventName = reader.GetString(3),
                DatabaseName = reader.IsDBNull(4) ? null : reader.GetString(4),
                LoginName = reader.IsDBNull(5) ? null : reader.GetString(5),
                ApplicationName = reader.IsDBNull(6) ? null : reader.GetString(6),
                HostName = reader.IsDBNull(7) ? null : reader.GetString(7),
                Spid = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                Duration = reader.IsDBNull(9) ? null : reader.GetInt64(9),
                CpuTime = reader.IsDBNull(10) ? null : reader.GetInt64(10),
                Reads = reader.IsDBNull(11) ? null : reader.GetInt64(11),
                Writes = reader.IsDBNull(12) ? null : reader.GetInt64(12),
                SqlText = reader.IsDBNull(13) ? null : reader.GetString(13),
                ExecutionOrder = reader.GetInt64(14),
                IsReplayable = reader.GetBoolean(15)
            };
        }

        private CdcCapture MapCdcCapture(IDataReader reader)
        {
            return new CdcCapture
            {
                CaptureId = reader.GetGuid(0),
                SessionId = reader.GetGuid(1),
                CaptureType = reader.GetString(2),
                CaptureTime = reader.GetDateTime(3),
                TableName = reader.GetString(4),
                CaptureData = reader.GetString(5),
                RecordCount = reader.GetInt32(6),
                DataHash = reader.IsDBNull(7) ? null : reader.GetString(7)
            };
        }

        private ComparisonResult MapComparisonResult(IDataReader reader)
        {
            var differenceDataJson = reader.IsDBNull(8) ? null : reader.GetString(8);
            var tableComparisons = new Dictionary<string, TableComparison>();

            if (!string.IsNullOrEmpty(differenceDataJson))
            {
                try
                {
                    tableComparisons = JsonConvert.DeserializeObject<Dictionary<string, TableComparison>>(differenceDataJson)
                                     ?? new Dictionary<string, TableComparison>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize table comparisons");
                }
            }

            return new ComparisonResult
            {
                ComparisonId = reader.GetGuid(0),
                SessionId = reader.GetGuid(1),
                LeftCaptureId = reader.GetGuid(2),
                RightCaptureId = reader.GetGuid(3),
                ComparisonTime = reader.GetDateTime(4),
                OverallMatch = reader.GetBoolean(6),
                TotalDifferences = reader.GetInt32(7),
                TableComparisons = tableComparisons,
                ComparisonNotes = reader.IsDBNull(9) ? null : reader.GetString(9)
            };
        }

        // API compatibility methods - delegate to existing methods
        public async Task CreateTraceSessionAsync(TraceSession session)
        {
            await UpdateSessionAsync(session);
        }

        public async Task<TraceSession> GetTraceSessionAsync(Guid sessionId)
        {
            return await GetSessionAsync(sessionId);
        }

        public async Task<TraceSession> GetTraceSessionByNameAsync(string sessionName)
        {
            return await GetSessionByNameAsync(sessionName);
        }

        public async Task<IEnumerable<TraceSession>> GetTraceSessionsAsync()
        {
            return await GetActiveSessionsAsync();
        }

        public async Task UpdateTraceSessionAsync(TraceSession session)
        {
            await UpdateSessionAsync(session);
        }

        public async Task DeleteTraceSessionAsync(Guid sessionId)
        {
            await DeleteSessionAsync(sessionId);
        }
    }
}