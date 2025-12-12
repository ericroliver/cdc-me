## Questions

## Answers

1. Don't worry about this one for now. We will be implementing full auth as a separate stream of work
2. We are no tin a prod scenario so the breaking change is OK
3. the new behavior is appropriate.
4. what do you recommend?

## Feedback

cdc-api/appsettings.json
Comment on lines +11 to +12
"TEST_DB_CONNECTION": "Server=blue.local;Database=cdctest;User Id=sa;Password=A123_Z321!;TrustServerCertificate=true;",
"CDCME_DB_CONNECTION": "Host=blue.local;Database=cdcme;Username=postgres;Password=A123_Z321!"
Copilot AI
23 minutes ago
Hard-coded production-like credentials (including cleartext passwords) are committed. Replace with environment variables or secret management (e.g. ASP.NET configuration providers) and remove secrets from version control.

Suggested change
"TEST_DB_CONNECTION": "Server=blue.local;Database=cdctest;User Id=sa;Password=A123_Z321!;TrustServerCertificate=true;",
"CDCME_DB_CONNECTION": "Host=blue.local;Database=cdcme;Username=postgres;Password=A123_Z321!"
"TEST_DB_CONNECTION": "",
"CDCME_DB_CONNECTION": ""
Copilot uses AI. Check for mistakes.

@ericroliver Reply...
cdc-lib/Trace/SqlServerTraceProvider.cs
Comment on lines 508 to 513
SessionId = reader.GetGuid(0),
SessionName = reader.GetString(1),
TestDatabase = reader.GetString(2),
TestConnectionString = reader.GetString(3),
SnapshotName = reader.IsDBNull(4) ? null : reader.GetString(4),
StartTime = reader.GetDateTime(5),
EndTime = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
Copilot AI
23 minutes ago
Column indexes are off by one after removing TestConnectionString from the SELECT list; SnapshotName is now at ordinal 3 (not 4), StartTime at 4, etc. This will throw or map incorrect data. Adjust indices to match the new 10-column projection.

Copilot uses AI. Check for mistakes.

@ericroliver Reply...
cdc-lib/Trace/TraceManager.cs
Comment on lines 419 to 424
events.Add(@"
ADD EVENT sqlserver.sql_batch_completed(
ACTION(sqlserver.client_app_name, sqlserver.client_hostname,
sqlserver.database_name, sqlserver.session_id, sqlserver.username)
sqlserver.database_name, sqlserver.session_id, sqlserver.username, sqlserver.sql_text, sqlserver.tsql_stack, sqlserver.plan_handle, sqlserver.session_id, sqlserver.request_id, sqlserver.client_connection_id, sqlserver.transaction_id)
WHERE ([sqlserver].[database_name] = N'" + config.DatabaseName + @"')
)");
Copilot AI
23 minutes ago
Duplicate inclusion of sqlserver.session_id in ACTION list and possible unsupported/duplicate actions may cause Extended Events session creation failure. Remove duplicates and verify each action is valid for the selected events.

Copilot uses AI. Check for mistakes.

@ericroliver Reply...
cdc-lib/Trace/SnapshotManager.cs
Comment on lines +96 to +108
// Check if target database exists
var databaseExistsSql = $"SELECT COUNT(1) FROM sys.databases WHERE name = '{targetDatabaseName}'";
var databaseExists = await \_dac.ExecuteScalarAsync<int>(databaseExistsSql) > 0;

                if (!databaseExists)
                {
                    return new SnapshotResult
                    {
                        Success = false,
                        Message = $"Target database '{targetDatabaseName}' does not exist. Cannot restore snapshot to non-existent database.",
                        SnapshotName = snapshotName
                    };
                }

Copilot AI
23 minutes ago
Behavior change: restore now fails if target DB does not exist instead of recreating it (previous implementation dropped/recreated). This is a breaking change—document it or reintroduce optional create semantics to avoid unexpected failures.

Copilot uses AI. Check for mistakes.

@ericroliver Reply...
cdc-lib/Trace/SnapshotManager.cs
{
Success = false,
Message = $"Failed to drop snapshot: {ex.Message}",
Message = $"Failed to restore snapshot: {ex.Message}",
Copilot AI
23 minutes ago
Error message in DropSnapshotAsync incorrectly says 'Failed to restore snapshot' instead of 'drop snapshot'. Update wording to reflect the operation for clearer diagnostics.

Suggested change
Message = $"Failed to restore snapshot: {ex.Message}",
Message = $"Failed to drop snapshot: {ex.Message}",
Copilot uses AI. Check for mistakes.

@ericroliver Reply...
cdc-api/Controllers/CdcController.cs
Comment on lines +329 to +332
["sessionName"] = sessionName,
["testDatabase"] = "TestDatabase", // Could be made configurable
["description"] = $"CDC session created via API",
["configuration"] = JsonSerializer.Serialize(configuration)
Copilot AI
23 minutes ago
Hard-coded testDatabase value will store incorrect database name in trace_sessions. Use the actual database name from configuration/connection factory.

Copilot uses AI. Check for mistakes.

@ericroliver Reply...
cdc-api/Controllers/CdcController.cs
Comment on lines +350 to +357
private async Task<string> SaveCdcCaptureAsync(
SimpleDac cdcMeDac,
string sessionName,
string captureName,
string captureType,
IDictionary<string, IEnumerable<IDictionary<string, object>>> cdcData,
List<string> tablesEnabled,
List<string> tablesSkipped)
Copilot AI
23 minutes ago
Each table insert is executed individually without a transaction—on failure you get a partial capture. Wrap header + detail inserts in a single transaction and consider batching to reduce round trips.

Copilot uses AI. Check for mistakes.

@ericroliver Reply...
cdc-api/Controllers/CdcController.cs
Comment on lines +392 to +397
const string insertDetailSql = @"
INSERT INTO cdc_captures (
capture_header_id, table_name, capture_data, record_count, data_hash
) VALUES (
@captureHeaderId, @tableName, @captureData::jsonb, @recordCount, @dataHash
)";
Copilot AI
23 minutes ago
Each table insert is executed individually without a transaction—on failure you get a partial capture. Wrap header + detail inserts in a single transaction and consider batching to reduce round trips.

Copilot uses AI. Check for mistakes.

@ericroliver Reply...
cdc-api/Controllers/CdcController.cs
Comment on lines +399 to +407
foreach (var tableData in cdcData)
{
var tableName = tableData.Key;
var data = tableData.Value;
var recordCount = data.Count();

            if (recordCount > 0)
            {
                var jsonData = JsonSerializer.Serialize(data);

Copilot AI
23 minutes ago
Each table insert is executed individually without a transaction—on failure you get a partial capture. Wrap header + detail inserts in a single transaction and consider batching to reduce round trips.

Copilot uses AI. Check for mistakes.

@ericroliver Reply...
cdc-api/Controllers/CdcController.cs
/// <returns>Result of the CDC start operation</returns>
[HttpPost("start")]
public async Task<ActionResult<StartCdcResponse>> StartCdc([FromBody] StartCdcRequest request)
{
Copilot AI
23 minutes ago
Model validation attributes are present but ModelState is never checked. Add if (!ModelState.IsValid) return BadRequest(ModelState); early to enforce required fields consistently.

Suggested change
{
{
if (!ModelState.IsValid)
{
return BadRequest(ModelState);
}
Copilot uses AI. Check for mistakes.

@ericroliver Reply...
cdc-api/Controllers/CdcController.cs
Comment on lines 15 to +19
[ApiController]
[Route("[controller]")]
[Route("api/[controller]")]
public class CdcController : ControllerBase
{
private static readonly string[] Summaries = new[]
private readonly ILogger<CdcController> \_logger;
Copilot AI
23 minutes ago
Sensitive operations (enabling/disabling CDC and capturing data) are exposed without authentication or authorization checks. Add appropriate auth (e.g. [Authorize]) and role/claim validation to prevent unauthorized data plane manipulation.

Copilot uses AI. Check for mistakes.

@ericroliver Reply...
cdc-api/Controllers/CdcController.cs
Comment on lines +276 to +283
private static IEnumerable<SqlTable> FilterTables(
IEnumerable<SqlTable> allTables,
List<string>? tablesToInclude,
List<string>? tablesToExclude)
{
\_logger = logger;
var tables = allTables;

        // Apply include filter if specified

Copilot AI
23 minutes ago
[nitpick] Filtering logic is duplicated in test helper (CdcControllerTests via CdcControllerTestHelper). Expose this method as internal and use InternalsVisibleTo or test through the API to avoid divergence.

Copilot uses AI. Check for mistakes.

@ericroliver Reply...
cdc-api.Tests/Controllers/SnapshotControllerTests.cs
Comment on lines 139 to +154
var request = new DeleteSnapshotRequest
{
SnapshotName = "TestSnapshot",
ConnectionString = "Server=test;Database=test;Trusted_Connection=true;"
SnapshotName = "TestSnapshot"
};

        // Act
        var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/snapshot")
        {
            Content = JsonContent.Create(request)
        });
        // Note: This test may fail due to ASP.NET Core not supporting DELETE with body by default
        // This is a known limitation and the test documents the expected behavior
        var json = JsonSerializer.Serialize(request);
        var requestMessage = new HttpRequestMessage(HttpMethod.Delete, "/api/snapshot");
        requestMessage.Content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // Expecting UnsupportedMediaType (415) due to DELETE with body limitation
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);

Copilot AI
23 minutes ago
Test name implies a successful deletion but assertion expects 415 UnsupportedMediaType. Either adjust the endpoint design (e.g., pass snapshot name in route and accept DELETE without body) or fix the test to reflect intended success behavior.

Copilot uses AI. Check for mistakes.

@ericroliver Reply...
cdc-lib/Trace/ISnapshotManager.cs
Comment on lines +10 to +11
Task<SnapshotResult> CreateSnapshotAsync(string databaseName, string snapshotName);
Task<SnapshotResult> RestoreSnapshotAsync(string snapshotName, string targetDatabaseName);
Copilot AI
23 minutes ago
Removed overloads (connectionString versions, simple CreateSnapshotAsync returning string) constitute a breaking change; document in CHANGELOG/migration notes and ensure external consumers are updated.

Copilot uses AI. Check for mistakes.

@ericroliver Reply...
cdc-lib/Trace/ISnapshotManager.cs
Comment on lines +10 to +14
Task<SnapshotResult> CreateSnapshotAsync(string databaseName, string snapshotName);
Task<SnapshotResult> RestoreSnapshotAsync(string snapshotName, string targetDatabaseName);
Task<bool> SnapshotExistsAsync(string snapshotName);
Task RestoreFromSnapshotAsync(string databaseName, string snapshotName);
Task<SnapshotResult> DropSnapshotAsync(string snapshotName, string connectionString);
Task DropSnapshotAsync(string snapshotName);
Task<SnapshotResult> DropSnapshotAsync(string snapshotName);
Copilot AI
23 minutes ago
Removed overloads (connectionString versions, simple CreateSnapshotAsync returning string) constitute a breaking change; document in CHANGELOG/migration notes and ensure external consumers are updated.

Copilot uses AI. Check for mistakes.

@ericroliver Reply...
cdc-lib/Trace/ISnapshotManager.cs
Task<SnapshotInfo> GetSnapshotInfoAsync(string snapshotName);
Task<List<SnapshotInfo>> ListSnapshotsAsync(string databaseName, string connectionString);
Task<List<SnapshotInfo>> ListSnapshotsAsync(string databaseName);
Task<List<SnapshotInfo>> ListSnapshotsAsync();
Copilot AI
23 minutes ago
Removed overloads (connectionString versions, simple CreateSnapshotAsync returning string) constitute a breaking change; document in CHANGELOG/migration notes and ensure external consumers are updated.

Suggested change
Task<List<SnapshotInfo>> ListSnapshotsAsync();
Task<List<SnapshotInfo>> ListSnapshotsAsync();
/// <summary>
/// [DEPRECATED] Creates a snapshot using a connection string. This overload will be removed in a future version.
/// </summary>
[Obsolete("Use CreateSnapshotAsync(string databaseName, string snapshotName) instead. This overload will be removed in a future version.")]
Task<SnapshotResult> CreateSnapshotAsync(string connectionString, string snapshotName);
/// <summary>
/// [DEPRECATED] Creates a snapshot and returns the snapshot name as a string. This overload will be removed in a future version.
/// </summary>
[Obsolete("Use CreateSnapshotAsync(string databaseName, string snapshotName) instead. This overload will be removed in a future version.")]
Task<string> CreateSnapshotAsync();
Copilot uses AI. Check for mistakes.

@ericroliver Reply...
cdc-lib/Trace/TraceManager.cs
}

            var sessionName = $"CDC_Trace_{sessionId:N}";
            var sessionName = GetExtendedEventsSessionName(sessionId);

Copilot AI
23 minutes ago
[nitpick] The query mixes retrieval of sql_text as an action (correct) but also treats 'statement' as an action—'statement' is typically event data for statement_completed, not an action for batch/rpc events. Clarify event set or adjust extraction to avoid always-null columns.

Copilot uses AI. Check for mistakes.

@ericroliver Reply...
cdc-lib/Trace/TraceManager.cs
event_data.value('(data[@name=''logical_reads'']/value)[1]', 'bigint') AS reads,
event_data.value('(data[@name=''writes'']/value)[1]', 'bigint') AS writes,
event_data.value('(data[@name=''statement'']/value)[1]', 'nvarchar(max)') AS sql_text,
event_data.value('(action[@name=''sql_text'']/value)[1]', 'nvarchar(max)') AS sql_text,
Copilot AI
23 minutes ago
[nitpick] The query mixes retrieval of sql_text as an action (correct) but also treats 'statement' as an action—'statement' is typically event data for statement_completed, not an action for batch/rpc events. Clarify event set or adjust extraction to avoid always-null columns.

Copilot uses AI. Check for mistakes.

@ericroliver Reply...
cdc-lib/Trace/TraceManager.cs
event_data.value('(action[@name=''request_id'']/value)[1]', 'int') AS request_id,
event_data.value('(action[@name=''client_connection_id'']/value)[1]', 'uniqueidentifier') AS client_connection_id,
event_data.value('(action[@name=''transaction_id'']/value)[1]', 'bigint') AS transaction_id,
event_data.value('(action[@name=''statement'']/value)[1]', 'nvarchar(max)') AS statement,
Copilot AI
23 minutes ago
[nitpick] The query mixes retrieval of sql_text as an action (correct) but also treats 'statement' as an action—'statement' is typically event data for statement_completed, not an action for batch/rpc events. Clarify event set or adjust extraction to avoid always-null columns.

Suggested change
event_data.value('(action[@name=''statement'']/value)[1]', 'nvarchar(max)') AS statement,
event_data.value('(data[@name=''statement'']/value)[1]', 'nvarchar(max)') AS statement,
Copilot uses AI. Check for mistakes.

@ericroliver Reply...
cdc-api/Controllers/SnapshotController.cs
Comment on lines 178 to +181
\_logger.LogInformation("Deleting snapshot {SnapshotName}", request.SnapshotName);

            var result = await _snapshotManager.DropSnapshotAsync(
                request.SnapshotName,
                request.ConnectionString);
                request.SnapshotName);

Copilot AI
23 minutes ago
[nitpick] DELETE endpoint requires a JSON body (SnapshotName) which is unconventional and causes client incompatibility (reflected in tests). Consider route-based DELETE: DELETE /api/snapshot/{snapshotName} for better REST alignment.

Copilot uses AI. Check for mistakes.

@ericroliver Reply...
cdc-lib/Trace/TraceManager.cs
Comment on lines 68 to 69
var session = await _traceProvider.GetSessionAsync(sessionId);
var sessionName = $"CDC_Trace_{sessionId:N}";
Copilot AI
23 minutes ago
Hard-coded session name format is duplicated; you introduced GetExtendedEventsSessionName earlier—use it here instead of interpolating to keep naming logic centralized.

Copilot uses AI. Check for mistakes.

@ericroliver Reply...
cdc-lib/Trace/TraceManager.cs
Comment on lines +71 to +73
// Export trace data from Extended Events to trace database BEFORE stopping
// This is critical because stopping the session clears the ring buffer
await ExportTraceDataFromRunningSessionAsync(sessionId, sessionName);
Copilot AI
23 minutes ago
Hard-coded session name format is duplicated; you introduced GetExtendedEventsSessionName earlier—use it here instead of interpolating to keep naming logic centralized.

Copilot uses AI. Check for mistakes.

@ericroliver Reply...
cdc-lib/Trace/TraceManager.cs
Comment on lines 75 to 76
// Stop Extended Events session
await StopExtendedEventsSessionAsync(sessionName);
Copilot AI
23 minutes ago
Hard-coded session name format is duplicated; you introduced GetExtendedEventsSessionName earlier—use it here instead of interpolating to keep naming logic centralized.

Copilot uses AI. Check for mistakes.

@ericroliver Reply...
