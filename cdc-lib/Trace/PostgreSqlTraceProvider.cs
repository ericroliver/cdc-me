using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Newtonsoft.Json;
using Softbase.Cdc.Models;
using System.Data;

namespace Softbase.Cdc.Trace
{
    public class PostgreSqlTraceProvider : ITraceDataProvider
    {
        private readonly string _connectionString;
        private readonly ILogger _logger;
        private readonly TraceStorageConfiguration _config;

        public PostgreSqlTraceProvider(TraceStorageConfiguration config, ILogger logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _connectionString = config.ConnectionString ?? throw new ArgumentNullException(nameof(config.ConnectionString));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TraceSession> CreateSessionAsync(TraceConfiguration config)
        {
            const string insertSql = @"
                INSERT INTO trace_sessions (session_name, test_database, snapshot_name, description, configuration)
                VALUES (@sessionName, @testDatabase, @snapshotName, @description, @configuration::jsonb)
                RETURNING session_id, start_time, status, created_by";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(insertSql, connection);
            command.Parameters.AddWithValue("@sessionName", config.SessionName);
            command.Parameters.AddWithValue("@testDatabase", config.DatabaseName);
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
                SELECT session_id, session_name, test_database, snapshot_name,
                       start_time, end_time, status, created_by, description, configuration
                FROM trace_sessions
                WHERE session_id = @sessionId";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(selectSql, connection);
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
                SELECT session_id, session_name, test_database, snapshot_name,
                       start_time, end_time, status, created_by, description, configuration
                FROM trace_sessions
                WHERE session_name = @sessionName";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(selectSql, connection);
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
                SELECT session_id, session_name, test_database, snapshot_name, 
                       start_time, end_time, status, created_by, description, configuration
                FROM trace_sessions 
                --WHERE status = 'Active'
                ORDER BY start_time DESC";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(selectSql, connection);
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
                UPDATE trace_sessions
                SET snapshot_name = @snapshotName,
                    end_time = @endTime,
                    status = @status,
                    description = @description,
                    configuration = @configuration::jsonb
                WHERE session_id = @sessionId";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(updateSql, connection);
            command.Parameters.AddWithValue("@sessionId", session.SessionId);
            command.Parameters.AddWithValue("@snapshotName", (object?)session.SnapshotName ?? DBNull.Value);
            command.Parameters.AddWithValue("@endTime", (object?)session.EndTime ?? DBNull.Value);
            command.Parameters.AddWithValue("@status", session.Status);
            command.Parameters.AddWithValue("@description", session.Description ?? "");
            command.Parameters.AddWithValue("@configuration", JsonConvert.SerializeObject(session.Configuration));

            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteSessionAsync(Guid sessionId)
        {
            const string deleteSql = "DELETE FROM trace_sessions WHERE session_id = @sessionId";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(deleteSql, connection);
            command.Parameters.AddWithValue("@sessionId", sessionId);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<long> SaveTraceEventAsync(TraceEvent traceEvent)
        {
            const string insertSql = @"
                INSERT INTO trace_events (session_id, event_time, event_name, database_name, login_name, 
                                        application_name, host_name, spid, duration, cpu_time, reads, writes, 
                                        sql_text, execution_order, is_replayable)
                VALUES (@sessionId, @eventTime, @eventName, @databaseName, @loginName, @applicationName, 
                        @hostName, @spid, @duration, @cpuTime, @reads, @writes, @sqlText, @executionOrder, @isReplayable)
                RETURNING event_id";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(insertSql, connection);
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
                SELECT event_id, session_id, event_time, event_name, database_name, login_name, 
                       application_name, host_name, spid, duration, cpu_time, reads, writes, 
                       sql_text, execution_order, is_replayable
                FROM trace_events 
                WHERE session_id = @sessionId 
                ORDER BY execution_order";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(selectSql, connection);
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
                SELECT event_id, session_id, event_time, event_name, database_name, login_name, 
                       application_name, host_name, spid, duration, cpu_time, reads, writes, 
                       sql_text, execution_order, is_replayable
                FROM trace_events 
                WHERE session_id = @sessionId 
                ORDER BY execution_order
                OFFSET @skip LIMIT @take";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(selectSql, connection);
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
            const string countSql = "SELECT COUNT(*) FROM trace_events WHERE session_id = @sessionId";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(countSql, connection);
            command.Parameters.AddWithValue("@sessionId", sessionId);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<Guid> SaveCdcCaptureAsync(CdcCapture capture)
        {
            const string insertSql = @"
                INSERT INTO cdc_captures (session_id, capture_type, table_name, capture_data, record_count, data_hash)
                VALUES (@sessionId, @captureType, @tableName, @captureData::jsonb, @recordCount, @dataHash)
                RETURNING capture_id";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(insertSql, connection);
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
                SELECT capture_id, session_id, capture_type, capture_time, table_name, capture_data, record_count, data_hash
                FROM cdc_captures 
                WHERE capture_id = @captureId";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(selectSql, connection);
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
                SELECT capture_id, session_id, capture_type, capture_time, table_name, capture_data, record_count, data_hash
                FROM cdc_captures 
                WHERE session_id = @sessionId 
                ORDER BY capture_time";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(selectSql, connection);
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
                SELECT capture_id, session_id, capture_type, capture_time, table_name, capture_data, record_count, data_hash
                FROM cdc_captures 
                WHERE session_id = @sessionId AND capture_type = @captureType
                ORDER BY capture_time";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(selectSql, connection);
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
                INSERT INTO comparison_results (session_id, left_capture_id, right_capture_id, table_name, 
                                              is_match, difference_count, difference_data, comparison_notes)
                VALUES (@sessionId, @leftCaptureId, @rightCaptureId, @tableName, @isMatch, @differenceCount, 
                        @differenceData::jsonb, @comparisonNotes)
                RETURNING comparison_id";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(insertSql, connection);
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
                SELECT comparison_id, session_id, left_capture_id, right_capture_id, comparison_time, 
                       table_name, is_match, difference_count, difference_data, comparison_notes
                FROM comparison_results 
                WHERE comparison_id = @comparisonId";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(selectSql, connection);
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
                SELECT comparison_id, session_id, left_capture_id, right_capture_id, comparison_time, 
                       table_name, is_match, difference_count, difference_data, comparison_notes
                FROM comparison_results 
                WHERE session_id = @sessionId 
                ORDER BY comparison_time DESC";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(selectSql, connection);
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
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to test PostgreSQL connection");
                return false;
            }
        }

        public async Task InitializeSchemaAsync()
        {
            if (!_config.AutoCreateSchema)
                return;

            // Schema initialization would be done via the SQL script
            // This method could run the schema creation script if needed
            _logger.LogInformation("PostgreSQL schema initialization - assuming schema exists");
            await Task.CompletedTask;
        }

        public async Task<string> GetProviderInfoAsync()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand("SELECT version()", connection);
            var version = await command.ExecuteScalarAsync();

            return $"PostgreSQL Provider - {version}";
        }

        private TraceSession MapTraceSession(IDataReader reader)
        {
            var configJson = reader.IsDBNull(9) ? null : reader.GetString(9);
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
                SnapshotName = reader.IsDBNull(3) ? null : reader.GetString(3),
                StartTime = reader.GetDateTime(4),
                EndTime = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                Status = reader.GetString(6),
                CreatedBy = reader.GetString(7),
                Description = reader.IsDBNull(8) ? null : reader.GetString(8),
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