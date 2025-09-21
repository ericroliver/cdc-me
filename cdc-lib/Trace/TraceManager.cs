using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Softbase.Cdc.Models;
using System.Text;
using Newtonsoft.Json;

namespace Softbase.Cdc.Trace
{
    public class TraceManager : ITraceManager
    {
        private readonly SimpleDac _testDac;
        private readonly ITraceDataProvider _traceProvider;
        private readonly ILogger _logger;

        public TraceManager(SimpleDac testDac, ITraceDataProvider traceProvider, ILogger logger)
        {
            _testDac = testDac ?? throw new ArgumentNullException(nameof(testDac));
            _traceProvider = traceProvider ?? throw new ArgumentNullException(nameof(traceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Generates the Extended Events session name for a given session ID.
        /// </summary>
        /// <param name="sessionId">The trace session ID.</param>
        /// <returns>The Extended Events session name.</returns>
        private static string GetExtendedEventsSessionName(Guid sessionId)
        {
            return $"CDC_Trace_{sessionId:N}";
        }

        public async Task<TraceSession> StartTraceAsync(TraceConfiguration config)
        {
            _logger.LogInformation("Starting trace session: {SessionName}", config.SessionName);

            try
            {
                // Create session in trace database
                var session = await _traceProvider.CreateSessionAsync(config);
                await _traceProvider.UpdateSessionAsync(session);

                // Create Extended Events session on test database
                var sessionName = $"CDC_Trace_{session.SessionId:N}";
                await CreateExtendedEventsSessionAsync(sessionName, config);

                _logger.LogInformation("Successfully started trace session {SessionId} with Extended Events session {ExtendedEventsSession}",
                    session.SessionId, sessionName);

                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start trace session: {SessionName}", config.SessionName);
                throw;
            }
        }

        public async Task<TraceSession> StopTraceAsync(Guid sessionId)
        {
            _logger.LogInformation("Stopping trace session: {SessionId}", sessionId);

            try
            {
                var session = await _traceProvider.GetSessionAsync(sessionId);
                var sessionName = $"CDC_Trace_{sessionId:N}";

                // Export trace data from Extended Events to trace database BEFORE stopping
                // This is critical because stopping the session clears the ring buffer
                await ExportTraceDataFromRunningSessionAsync(sessionId, sessionName);

                // Stop Extended Events session
                await StopExtendedEventsSessionAsync(sessionName);

                // Update session status
                session.EndTime = DateTime.UtcNow;
                session.Status = "Completed";
                await _traceProvider.UpdateSessionAsync(session);

                // Clean up Extended Events session
                await DropExtendedEventsSessionAsync(sessionName);

                _logger.LogInformation("Successfully stopped trace session {SessionId}", sessionId);
                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop trace session: {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<TraceStatus> GetTraceStatusAsync(Guid sessionId)
        {
            try
            {
                var session = await _traceProvider.GetSessionAsync(sessionId);
                var sessionName = GetExtendedEventsSessionName(sessionId);

                // Check Extended Events session status
                var isRunning = await IsExtendedEventsSessionRunningAsync(sessionName);
                var eventCount = await _traceProvider.GetTraceEventCountAsync(sessionId);

                return new TraceStatus
                {
                    SessionId = sessionId,
                    State = isRunning ? "Running" : (session.Status == "Active" ? "Stopped" : session.Status),
                    StartedAt = session.StartTime,
                    EventCount = eventCount,
                    LastError = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get trace status for session: {SessionId}", sessionId);
                return new TraceStatus
                {
                    SessionId = sessionId,
                    State = "Failed",
                    LastError = ex.Message
                };
            }
        }

        public async Task<IEnumerable<TraceSession>> GetActiveSessionsAsync()
        {
            return await _traceProvider.GetActiveSessionsAsync();
        }

        private async Task CreateExtendedEventsSessionAsync(string sessionName, TraceConfiguration config)
        {
            var createSessionSql = BuildCreateExtendedEventsSessionSql(sessionName, config);
            await _testDac.ExecuteCommandAsync(createSessionSql);

            // Start the session
            var startSessionSql = $"ALTER EVENT SESSION [{sessionName}] ON SERVER STATE = START;";
            await _testDac.ExecuteCommandAsync(startSessionSql);
        }

        private async Task StopExtendedEventsSessionAsync(string sessionName)
        {
            try
            {
                var stopSessionSql = $"ALTER EVENT SESSION [{sessionName}] ON SERVER STATE = STOP;";
                await _testDac.ExecuteCommandAsync(stopSessionSql);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop Extended Events session {SessionName}", sessionName);
            }
        }

        private async Task DropExtendedEventsSessionAsync(string sessionName)
        {
            try
            {
                var dropSessionSql = $"DROP EVENT SESSION [{sessionName}] ON SERVER;";
                await _testDac.ExecuteCommandAsync(dropSessionSql);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to drop Extended Events session {SessionName}", sessionName);
            }
        }

        private async Task<bool> IsExtendedEventsSessionRunningAsync(string sessionName)
        {
            const string checkSessionSql = @"
                SELECT COUNT(1)
                FROM sys.dm_xe_sessions
                WHERE name = @sessionName";

            try
            {
                var count = await _testDac.ExecuteScalarAsync<int>(checkSessionSql, new Dictionary<string, object>
                {
                    ["@sessionName"] = sessionName
                });
                return count > 0;
            }
            catch
            {
                return false;
            }
        }

        private async Task ExportTraceDataFromRunningSessionAsync(Guid sessionId, string sessionName)
        {
            _logger.LogInformation("Exporting trace data from running session {SessionId} ({SessionName})", sessionId, sessionName);

            // Read events from Extended Events ring buffer while session is still running
            var readEventsSql = $@"
                SELECT
                    event_data.value('(@timestamp)[1]', 'datetime2') AS event_time,
                    event_data.value('(@name)[1]', 'varchar(50)') AS event_name,
                    event_data.value('(data[@name=''database_name'']/value)[1]', 'varchar(128)') AS database_name,
                    event_data.value('(action[@name=''username'']/value)[1]', 'varchar(128)') AS login_name,
                    event_data.value('(action[@name=''client_app_name'']/value)[1]', 'varchar(256)') AS application_name,
                    event_data.value('(action[@name=''client_hostname'']/value)[1]', 'varchar(128)') AS host_name,
                    event_data.value('(action[@name=''session_id'']/value)[1]', 'int') AS spid,
                    event_data.value('(data[@name=''duration'']/value)[1]', 'bigint') AS duration,
                    event_data.value('(data[@name=''cpu_time'']/value)[1]', 'bigint') AS cpu_time,
                    event_data.value('(data[@name=''logical_reads'']/value)[1]', 'bigint') AS reads,
                    event_data.value('(data[@name=''writes'']/value)[1]', 'bigint') AS writes,
                    event_data.value('(action[@name=''sql_text'']/value)[1]', 'nvarchar(max)') AS sql_text,
                    event_data.value('(action[@name=''tsql_stack'']/value)[1]', 'nvarchar(max)') AS tsql_stack,
                    event_data.value('(action[@name=''plan_handle'']/value)[1]', 'varbinary(64)') AS plan_handle,
                    event_data.value('(action[@name=''request_id'']/value)[1]', 'int') AS request_id,
                    event_data.value('(action[@name=''client_connection_id'']/value)[1]', 'uniqueidentifier') AS client_connection_id,
                    event_data.value('(action[@name=''transaction_id'']/value)[1]', 'bigint') AS transaction_id,
                    event_data.value('(action[@name=''statement'']/value)[1]', 'nvarchar(max)') AS statement,
                    ROW_NUMBER() OVER (ORDER BY event_data.value('(@timestamp)[1]', 'datetime2')) AS execution_order
                FROM (
                    SELECT CAST(target_data AS XML) AS target_data
                    FROM sys.dm_xe_session_targets st
                    INNER JOIN sys.dm_xe_sessions s ON s.address = st.event_session_address
                    WHERE s.name = @sessionName AND st.target_name = 'ring_buffer'
                ) AS data
                CROSS APPLY target_data.nodes('RingBufferTarget/event') AS XEventData(event_data)
                ORDER BY execution_order";

            try
            {
                var events = await _testDac.ExecuteReaderAsync(readEventsSql, reader =>
                {
                    var eventList = new List<TraceEvent>();
                    while (reader.Read())
                    {
                        var traceEvent = new TraceEvent
                        {
                            SessionId = sessionId,
                            EventTime = reader.IsDBNull(0) ? DateTime.UtcNow : reader.GetDateTime(0),
                            EventName = reader.IsDBNull(1) ? "unknown" : reader.GetString(1),
                            DatabaseName = reader.IsDBNull(2) ? null : reader.GetString(2),
                            LoginName = reader.IsDBNull(3) ? null : reader.GetString(3),
                            ApplicationName = reader.IsDBNull(4) ? null : reader.GetString(4),
                            HostName = reader.IsDBNull(5) ? null : reader.GetString(5),
                            Spid = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                            Duration = reader.IsDBNull(7) ? null : reader.GetInt64(7),
                            CpuTime = reader.IsDBNull(8) ? null : reader.GetInt64(8),
                            Reads = reader.IsDBNull(9) ? null : reader.GetInt64(9),
                            Writes = reader.IsDBNull(10) ? null : reader.GetInt64(10),
                            SqlText = reader.IsDBNull(11) ? null : reader.GetString(11),
                            TsqlStack = reader.IsDBNull(12) ? null : reader.GetString(12),
                            PlanHandle = reader.IsDBNull(13) ? null : (byte[])reader.GetValue(13),
                            RequestId = reader.IsDBNull(14) ? null : reader.GetInt32(14),
                            ClientConnectionId = reader.IsDBNull(15) ? null : reader.GetGuid(15),
                            TransactionId = reader.IsDBNull(16) ? null : reader.GetInt64(16),
                            Statement = reader.IsDBNull(17) ? null : reader.GetString(17),
                            ExecutionOrder = reader.GetInt64(18),
                            IsReplayable = IsStatementReplayable(reader.IsDBNull(11) ? "" : reader.GetString(11))
                        };
                        eventList.Add(traceEvent);
                    }
                    return eventList;
                }, new Dictionary<string, object>
                {
                    ["@sessionName"] = sessionName
                });

                // Save events to trace database
                foreach (var traceEvent in events)
                {
                    await _traceProvider.SaveTraceEventAsync(traceEvent);
                }

                _logger.LogInformation("Successfully exported {EventCount} trace events from running session {SessionId}", events.Count, sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export trace data from running session {SessionId}", sessionId);
                throw;
            }
        }

        /// <summary>
        /// Exports trace data to a file. This method is intended for exporting already-captured trace data
        /// from the trace database to a JSON file. For active sessions, use ExportTraceDataFromRunningSessionAsync
        /// to capture data before stopping the session.
        /// </summary>
        /// <param name="sessionId">The trace session ID</param>
        /// <param name="exportPath">The file path where the trace data should be exported</param>
        /// <returns>The export file path</returns>
        public async Task<string> ExportTraceDataAsync(Guid sessionId, string exportPath)
        {
            _logger.LogInformation("Exporting trace data for session {SessionId} to {ExportPath}", sessionId, exportPath);

            // Get session to find the Extended Events session name
            var session = await _traceProvider.GetSessionAsync(sessionId);
            if (session == null)
            {
                throw new InvalidOperationException($"Session {sessionId} not found");
            }

            var sessionName = GetExtendedEventsSessionName(sessionId);

            // NOTE: This method attempts to read from Extended Events ring buffer, but if the session
            // has been stopped, the ring buffer will be empty. For active sessions, use
            // ExportTraceDataFromRunningSessionAsync instead.
            // Read events from Extended Events ring buffer
            var readEventsSql = $@"
                SELECT
                    event_data.value('(@timestamp)[1]', 'datetime2') AS event_time,
                    event_data.value('(@name)[1]', 'varchar(50)') AS event_name,
                    event_data.value('(data[@name=''database_name'']/value)[1]', 'varchar(128)') AS database_name,
                    event_data.value('(action[@name=''username'']/value)[1]', 'varchar(128)') AS login_name,
                    event_data.value('(action[@name=''client_app_name'']/value)[1]', 'varchar(256)') AS application_name,
                    event_data.value('(action[@name=''client_hostname'']/value)[1]', 'varchar(128)') AS host_name,
                    event_data.value('(action[@name=''session_id'']/value)[1]', 'int') AS spid,
                    event_data.value('(data[@name=''duration'']/value)[1]', 'bigint') AS duration,
                    event_data.value('(data[@name=''cpu_time'']/value)[1]', 'bigint') AS cpu_time,
                    event_data.value('(data[@name=''logical_reads'']/value)[1]', 'bigint') AS reads,
                    event_data.value('(data[@name=''writes'']/value)[1]', 'bigint') AS writes,
                    event_data.value('(action[@name=''sql_text'']/value)[1]', 'nvarchar(max)') AS sql_text,
                    event_data.value('(action[@name=''tsql_stack'']/value)[1]', 'nvarchar(max)') AS tsql_stack,
                    event_data.value('(action[@name=''plan_handle'']/value)[1]', 'varbinary(64)') AS plan_handle,
                    event_data.value('(action[@name=''request_id'']/value)[1]', 'int') AS request_id,
                    event_data.value('(action[@name=''client_connection_id'']/value)[1]', 'uniqueidentifier') AS client_connection_id,
                    event_data.value('(action[@name=''transaction_id'']/value)[1]', 'bigint') AS transaction_id,
                    event_data.value('(action[@name=''statement'']/value)[1]', 'nvarchar(max)') AS statement,
                    ROW_NUMBER() OVER (ORDER BY event_data.value('(@timestamp)[1]', 'datetime2')) AS execution_order
                FROM (
                    SELECT CAST(target_data AS XML) AS target_data
                    FROM sys.dm_xe_session_targets st
                    INNER JOIN sys.dm_xe_sessions s ON s.address = st.event_session_address
                    WHERE s.name = @sessionName AND st.target_name = 'ring_buffer'
                ) AS data
                CROSS APPLY target_data.nodes('RingBufferTarget/event') AS XEventData(event_data)
                ORDER BY execution_order";

            try
            {
                var events = await _testDac.ExecuteReaderAsync(readEventsSql, reader =>
                {
                    var eventList = new List<TraceEvent>();
                    while (reader.Read())
                    {
                        var traceEvent = new TraceEvent
                        {
                            SessionId = sessionId,
                            EventTime = reader.IsDBNull(0) ? DateTime.UtcNow : reader.GetDateTime(0),
                            EventName = reader.IsDBNull(1) ? "unknown" : reader.GetString(1),
                            DatabaseName = reader.IsDBNull(2) ? null : reader.GetString(2),
                            LoginName = reader.IsDBNull(3) ? null : reader.GetString(3),
                            ApplicationName = reader.IsDBNull(4) ? null : reader.GetString(4),
                            HostName = reader.IsDBNull(5) ? null : reader.GetString(5),
                            Spid = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                            Duration = reader.IsDBNull(7) ? null : reader.GetInt64(7),
                            CpuTime = reader.IsDBNull(8) ? null : reader.GetInt64(8),
                            Reads = reader.IsDBNull(9) ? null : reader.GetInt64(9),
                            Writes = reader.IsDBNull(10) ? null : reader.GetInt64(10),
                            SqlText = reader.IsDBNull(11) ? null : reader.GetString(11),
                            TsqlStack = reader.IsDBNull(12) ? null : reader.GetString(12),
                            PlanHandle = reader.IsDBNull(13) ? null : (byte[])reader.GetValue(13),
                            RequestId = reader.IsDBNull(14) ? null : reader.GetInt32(14),
                            ClientConnectionId = reader.IsDBNull(15) ? null : reader.GetGuid(15),
                            TransactionId = reader.IsDBNull(16) ? null : reader.GetInt64(16),
                            Statement = reader.IsDBNull(17) ? null : reader.GetString(17),
                            ExecutionOrder = reader.GetInt64(18),
                            IsReplayable = IsStatementReplayable(reader.IsDBNull(11) ? "" : reader.GetString(11))
                        };
                        eventList.Add(traceEvent);
                    }
                    return eventList;
                }, new Dictionary<string, object>
                {
                    ["@sessionName"] = sessionName
                });

                // Save events to trace database
                foreach (var traceEvent in events)
                {
                    await _traceProvider.SaveTraceEventAsync(traceEvent);
                }

                // Export to file
                var exportData = events.Select(e => new
                {
                    e.EventId,
                    e.EventTime,
                    e.EventName,
                    e.DatabaseName,
                    e.LoginName,
                    e.ApplicationName,
                    e.HostName,
                    e.SqlText,
                    e.ExecutionOrder
                }).ToList();

                var json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
                await File.WriteAllTextAsync(exportPath, json);

                _logger.LogInformation("Exported {EventCount} trace events for session {SessionId} to {ExportPath}", events.Count, sessionId, exportPath);
                return exportPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export trace data for session {SessionId}", sessionId);
                throw;
            }
        }

        private string BuildCreateExtendedEventsSessionSql(string sessionName, TraceConfiguration config)
        {
            var sql = new StringBuilder();

            sql.AppendLine($"CREATE EVENT SESSION [{sessionName}] ON SERVER");

            // Add events based on configuration
            var events = new List<string>();
            foreach (var eventType in config.EventTypes)
            {
                switch (eventType.ToLower())
                {
                    case "sql_batch_completed":
                        events.Add(@"
                            ADD EVENT sqlserver.sql_batch_completed(
                                ACTION(sqlserver.client_app_name, sqlserver.client_hostname, 
                                       sqlserver.database_name, sqlserver.session_id, sqlserver.username, sqlserver.sql_text, sqlserver.tsql_stack, sqlserver.plan_handle, sqlserver.session_id, sqlserver.request_id, sqlserver.client_connection_id, sqlserver.transaction_id)
                                WHERE ([sqlserver].[database_name] = N'" + config.DatabaseName + @"')
                            )");
                        break;
                    case "rpc_completed":
                        events.Add(@"
                            ADD EVENT sqlserver.rpc_completed(
                                ACTION(sqlserver.client_app_name, sqlserver.client_hostname, 
                                       sqlserver.database_name, sqlserver.session_id, sqlserver.username, sqlserver.sql_text, sqlserver.tsql_stack, sqlserver.plan_handle, sqlserver.session_id, sqlserver.request_id, sqlserver.client_connection_id, sqlserver.transaction_id)
                                WHERE ([sqlserver].[database_name] = N'" + config.DatabaseName + @"')
                            )");
                        break;
                    case "sql_statement_completed":
                        events.Add(@"
                            ADD EVENT sqlserver.sql_statement_completed(
                                ACTION(sqlserver.client_app_name, sqlserver.client_hostname, 
                                       sqlserver.database_name, sqlserver.session_id, sqlserver.username, sqlserver.sql_text, sqlserver.tsql_stack, sqlserver.plan_handle, sqlserver.session_id, sqlserver.request_id, sqlserver.client_connection_id, sqlserver.transaction_id)
                                WHERE ([sqlserver].[database_name] = N'" + config.DatabaseName + @"')
                            )");
                        break;
                }
            }

            sql.AppendLine(string.Join(",", events));

            // Add ring buffer target
            sql.AppendLine($@"
                ADD TARGET package0.ring_buffer(
                    SET max_memory = {config.RingBufferSizeMB * 1024}
                )
                WITH (
                    MAX_MEMORY = {config.RingBufferSizeMB}MB,
                    EVENT_RETENTION_MODE = ALLOW_SINGLE_EVENT_LOSS,
                    MAX_DISPATCH_LATENCY = 30 SECONDS,
                    MAX_EVENT_SIZE = 0KB,
                    MEMORY_PARTITION_MODE = NONE,
                    TRACK_CAUSALITY = OFF,
                    STARTUP_STATE = OFF
                );");

            return sql.ToString();
        }

        private bool IsStatementReplayable(string sqlText)
        {
            if (string.IsNullOrWhiteSpace(sqlText))
                return false;

            var upperSql = sqlText.Trim().ToUpperInvariant();

            // Skip SELECT statements (read-only)
            if (upperSql.StartsWith("SELECT"))
                return false;

            // Skip system queries
            if (upperSql.Contains("SYS.") || upperSql.Contains("INFORMATION_SCHEMA"))
                return false;

            // Skip CDC system queries
            if (upperSql.Contains("CDC.") || upperSql.Contains("__$"))
                return false;

            // Skip backup/restore operations
            if (upperSql.Contains("BACKUP") || upperSql.Contains("RESTORE"))
                return false;

            // Skip trace/profiler queries
            if (upperSql.Contains("XE_") || upperSql.Contains("EXTENDED_EVENTS"))
                return false;

            // Allow DML statements
            if (upperSql.StartsWith("INSERT") || upperSql.StartsWith("UPDATE") ||
                upperSql.StartsWith("DELETE") || upperSql.StartsWith("MERGE"))
                return true;

            // Allow DDL statements (with caution)
            if (upperSql.StartsWith("CREATE") || upperSql.StartsWith("ALTER") ||
                upperSql.StartsWith("DROP"))
                return true;

            // Allow stored procedure calls
            if (upperSql.StartsWith("EXEC") || upperSql.StartsWith("EXECUTE"))
                return true;

            return false;
        }

        public async Task<bool> IsTraceRunningAsync(string sessionName)
        {
            try
            {
                var session = await _traceProvider.GetSessionByNameAsync(sessionName);
                if (session == null) return false;

                // Check if Extended Events session is running
                var extendedEventsSessionName = GetExtendedEventsSessionName(session.SessionId);
                var checkSql = @"
                    SELECT COUNT(1)
                    FROM sys.dm_xe_sessions
                    WHERE name = @sessionName";

                var count = await _testDac.ExecuteScalarAsync<int>(checkSql, new Dictionary<string, object>
                {
                    ["@sessionName"] = extendedEventsSessionName
                });

                return count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check if trace is running for session {SessionName}", sessionName);
                return false;
            }
        }

    }
}