2. .NET API: start/status/stop a single XE session (non-blocking)

Thread-safe singleton ensures only one active trace.

POST /trace/start → returns a traceId (GUID). Creates/starts an XE session named app*trace*{traceId} with optional filters, runs in background.

GET /trace/status/{id} → running/failed/stopped + last error.

POST /trace/stop/{id} → stops & drops the XE session.

Filters/templates: treat the JSON payload as your “template.” You can persist common payloads to file if you want “templates.”
Target uses ring_buffer so you don’t need file shares; you can add an event_file target later if you have a path on the SQL Server host.
Microsoft Learn
sqlskills.com

Program.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var b = WebApplication.CreateBuilder(args);
b.Services.AddControllers();
b.Services.AddSingleton<TraceService>();

var app = b.Build();
app.MapControllers();
app.Run();

TraceDtos.cs
public sealed class TraceStartRequest
{
public string ConnectionString { get; set; } = default!; // Microsoft.Data.SqlClient
public string? DatabaseNameEquals { get; set; } // optional filter
public string? SqlTextLike { get; set; } // optional filter
public int RingBufferKb { get; set; } = 4096; // memory for ring_buffer
}

public sealed class TraceStatus
{
public Guid TraceId { get; init; }
public string State { get; init; } = "Unknown"; // Running | Stopped | NotFound | Failed
public string? LastError { get; init; }
public DateTimeOffset? StartedAt { get; init; }
}

TraceService.cs
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

public sealed class TraceService
{
readonly SemaphoreSlim \_gate = new(1,1);
Guid? \_activeId;
string? \_cxn;
Exception? \_lastErr;
DateTimeOffset? \_startedAt;

    public async Task<Guid> StartAsync(TraceStartRequest req, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_activeId is not null) throw new InvalidOperationException("A trace is already active.");
            var id = Guid.NewGuid();
            var sessionName = $"app_trace_{id:N}";
            _cxn = req.ConnectionString;
            _lastErr = null;
            _startedAt = null;

            _ = Task.Run(async () =>
            {
                try
                {
                    using var conn = new SqlConnection(_cxn);
                    await conn.OpenAsync(ct);

                    // Clean any leftovers with same name
                    var drop = $"IF EXISTS (SELECT 1 FROM sys.server_event_sessions WHERE name=@n) DROP EVENT SESSION [{sessionName}] ON SERVER;";
                    using (var cmd = new SqlCommand(drop, conn)) { cmd.Parameters.AddWithValue("@n", sessionName); await cmd.ExecuteNonQueryAsync(ct); }

                    // Build filters
                    var pred = "TRUE";
                    if (!string.IsNullOrWhiteSpace(req.DatabaseNameEquals))
                        pred += " AND sqlserver.database_name = @db";
                    if (!string.IsNullOrWhiteSpace(req.SqlTextLike))
                        pred += " AND (sqlserver.sql_text LIKE @like)";

                    // Create XE session (add whatever events you need)
                    var create = $@"

CREATE EVENT SESSION [{sessionName}] ON SERVER
ADD EVENT sqlserver.rpc_completed
(
ACTION(sqlserver.client_app_name, sqlserver.client_hostname, sqlserver.username, sqlserver.database_name, sqlserver.sql_text)
WHERE ({pred})
),
ADD EVENT sqlserver.sql_batch_completed
(
ACTION(sqlserver.client_app_name, sqlserver.client_hostname, sqlserver.username, sqlserver.database_name, sqlserver.sql_text)
WHERE ({pred})
),
ADD EVENT sqlserver.attention
(
ACTION(sqlserver.client_app_name, sqlserver.client_hostname, sqlserver.username, sqlserver.database_name, sqlserver.sql_text)
)
ADD TARGET package0.ring_buffer(SET max_memory = {Math.Max(256, req.RingBufferKb)} )
WITH (MAX_MEMORY=64 MB, EVENT_RETENTION_MODE=ALLOW_SINGLE_EVENT_LOSS, MAX_DISPATCH_LATENCY=5 SECONDS, TRACK_CAUSALITY=ON);";
using (var cmd = new SqlCommand(create, conn))
{
if (!string.IsNullOrWhiteSpace(req.DatabaseNameEquals))
cmd.Parameters.AddWithValue("@db", req.DatabaseNameEquals);
if (!string.IsNullOrWhiteSpace(req.SqlTextLike))
cmd.Parameters.AddWithValue("@like", req.SqlTextLike);
await cmd.ExecuteNonQueryAsync(ct);
}

                    using (var cmd = new SqlCommand($"ALTER EVENT SESSION [{sessionName}] ON SERVER STATE = START;", conn))
                        await cmd.ExecuteNonQueryAsync(ct);

                    _startedAt = DateTimeOffset.UtcNow;
                }
                catch (Exception ex) { _lastErr = ex; }
            }, ct);

            _activeId = id;
            return id;
        }
        finally { _gate.Release(); }
    }

    public async Task<TraceStatus> StatusAsync(Guid id, CancellationToken ct)
    {
        if (_activeId != id) return new TraceStatus { TraceId = id, State = "NotFound", LastError = _lastErr?.Message };
        try
        {
            using var conn = new SqlConnection(_cxn!);
            await conn.OpenAsync(ct);
            var sessionName = $"app_trace_{id:N}";
            var sql = @"SELECT s.name, s.create_time, s.start_time
                        FROM sys.dm_xe_sessions s WHERE s.name = @n;";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@n", sessionName);
            using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
            if (await r.ReadAsync(ct))
            {
                var running = r["start_time"] != DBNull.Value;
                return new TraceStatus
                {
                    TraceId = id,
                    State = running ? "Running" : "Stopped",
                    LastError = _lastErr?.Message,
                    StartedAt = _startedAt
                };
            }
            return new TraceStatus { TraceId = id, State = "NotFound", LastError = _lastErr?.Message };
        }
        catch (Exception ex) { return new TraceStatus { TraceId = id, State = "Failed", LastError = ex.Message }; }
    }

    public async Task StopAsync(Guid id, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_activeId != id) return;
            using var conn = new SqlConnection(_cxn!);
            await conn.OpenAsync(ct);
            var sessionName = $"app_trace_{id:N}";
            // Stop (if exists) and drop
            var tsql = $@"

IF EXISTS (SELECT 1 FROM sys.server_event_sessions WHERE name=@n)
BEGIN
IF EXISTS (SELECT 1 FROM sys.dm_xe_sessions WHERE name=@n)
ALTER EVENT SESSION [{sessionName}] ON SERVER STATE = STOP;
DROP EVENT SESSION [{sessionName}] ON SERVER;
END";
using var cmd = new SqlCommand(tsql, conn);
cmd.Parameters.AddWithValue("@n", sessionName);
await cmd.ExecuteNonQueryAsync(ct);

            _activeId = null;
            _cxn = null;
            _startedAt = null;
        }
        finally { _gate.Release(); }
    }

}

TraceController.cs
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("trace")]
public sealed class TraceController : ControllerBase
{
readonly TraceService \_svc;
public TraceController(TraceService svc) => \_svc = svc;

    [HttpPost("start")]
    public async Task<ActionResult<object>> Start([FromBody] TraceStartRequest req, CancellationToken ct)
    {
        var id = await _svc.StartAsync(req, ct);
        return Ok(new { traceId = id });
    }

    [HttpGet("status/{id:guid}")]
    public Task<TraceStatus> Status([FromRoute] Guid id, CancellationToken ct)
        => _svc.StatusAsync(id, ct);

    [HttpPost("stop/{id:guid}")]
    public async Task<IActionResult> Stop([FromRoute] Guid id, CancellationToken ct)
    {
        await _svc.StopAsync(id, ct);
        return NoContent();
    }

}

Notes / tweaks

Swap/add events you care about (e.g., rpc_starting, sql_batch_starting, sp_statement_completed, wait/lock events, etc.). XE DDL reference here.
Microsoft Learn

ring_buffer keeps data in memory while the session runs (simple and remote-friendly). For heavy traces, change to an event_file target that writes on the SQL Server machine and later fn_xe_file_target_read_file it.
sqlskills.com

If you really want a GUI “Profiler” feel on Linux, Azure Data Studio had a Profiler extension, but it’s still just a front-end—server-side capture should be XE.
MSSQLTips.com

That’s it—drop this into your API project, point it at your SQL Server connection string, and you’ve got start/status/stop for a single active background trace that won’t block the API.

You said:
how do i save the trace?
ChatGPT said:
Thought for 17s

You’ve got two solid options:

Option A — write to event_file (best for “save to disk”)

The XE file is written by the SQL Server host, not your API container. Use a path the SQL Server service can write to (Windows: D:\XE\... or UNC \\server\share\...; Linux SQL Server: /var/opt/mssql/traces/...). Grant write perms to the SQL Server service account.

Change your session to use event_file
// add to your DTO
public sealed class TraceStartRequest {
public string ConnectionString { get; set; } = default!;
public string? DatabaseNameEquals { get; set; }
public string? SqlTextLike { get; set; }
public int RingBufferKb { get; set; } = 4096;

    // new (for saving to disk)
    public bool UseEventFile { get; set; } = true;
    public string? EventFilePath { get; set; } // e.g. "D:\\XE\\app_trace" or "/var/opt/mssql/traces/app_trace"
    public int MaxFileSizeMB { get; set; } = 200;
    public int MaxRollovers { get; set; } = 10;

}

// inside TraceService.StartAsync, replace the target part:
var target = req.UseEventFile && !string.IsNullOrWhiteSpace(req.EventFilePath)
? $"ADD TARGET package0.event*file(SET filename = N'{req.EventFilePath}*{id:N}.xel', max_file_size = {req.MaxFileSizeMB}, max_rollover_files = {req.MaxRollovers})"
: $"ADD TARGET package0.ring_buffer(SET max_memory = {Math.Max(256, req.RingBufferKb)} )";

var create = $@"
CREATE EVENT SESSION [{sessionName}] ON SERVER
ADD EVENT sqlserver.rpc_completed
(
ACTION(sqlserver.client_app_name, sqlserver.client_hostname, sqlserver.username, sqlserver.database_name, sqlserver.sql_text)
WHERE ({pred})
),
ADD EVENT sqlserver.sql_batch_completed
(
ACTION(sqlserver.client_app_name, sqlserver.client_hostname, sqlserver.username, sqlserver.database_name, sqlserver.sql_text)
WHERE ({pred})
),
ADD EVENT sqlserver.attention
(
ACTION(sqlserver.client_app_name, sqlserver.client_hostname, sqlserver.username, sqlserver.database_name, sqlserver.sql_text)
)
{target}
WITH (MAX_MEMORY=64 MB, EVENT_RETENTION_MODE=ALLOW_SINGLE_EVENT_LOSS, MAX_DISPATCH_LATENCY=5 SECONDS, TRACK_CAUSALITY=ON);";

Reading the saved .xel files later
-- Example: import files to a table
IF OBJECT_ID('dbo.AppTraceRaw','U') IS NULL
CREATE TABLE dbo.AppTraceRaw(
event_time datetime2(7) NULL,
name sysname NULL,
cpu_time bigint NULL,
duration bigint NULL,
database_name sysname NULL,
client_app_name nvarchar(256) NULL,
client_hostname nvarchar(256) NULL,
username nvarchar(256) NULL,
sql_text nvarchar(max) NULL
);

INSERT dbo.AppTraceRaw(event*time,name,cpu_time,duration,database_name,client_app_name,client_hostname,username,sql_text)
SELECT
DATEADD(hh, DATEDIFF(hh, SYSUTCDATETIME(), SYSDATETIME()), x.event_data.value('(event/@timestamp)[1]','datetime2')),
x.event_data.value('(event/@name)[1]','sysname'),
x.event_data.value('(event/data[@name="cpu_time"]/value)[1]','bigint'),
x.event_data.value('(event/data[@name="duration"]/value)[1]','bigint'),
x.event_data.value('(event/action[@name="database_name"]/value)[1]','sysname'),
x.event_data.value('(event/action[@name="client_app_name"]/value)[1]','nvarchar(256)'),
x.event_data.value('(event/action[@name="client_hostname"]/value)[1]','nvarchar(256)'),
x.event_data.value('(event/action[@name="username"]/value)[1]','nvarchar(256)'),
x.event_data.value('(event/action[@name="sql_text"]/value)[1]','nvarchar(max)')
FROM sys.fn_xe_file_target_read_file(N'D:\XE\app_trace*\*.xel', NULL, NULL, NULL) AS F
CROSS APPLY (SELECT TRY_CAST(F.event_data AS XML)) AS T(event_data);

Option B — keep ring_buffer, then persist to a table on demand

No files needed; you snapshot whatever’s in memory into a table when you stop or whenever you call an export action.

T-SQL to dump current ring buffer to a table
IF OBJECT_ID('dbo.AppTraceRingDump','U') IS NULL
CREATE TABLE dbo.AppTraceRingDump(
event_time datetime2(7) NULL,
name sysname NULL,
cpu_time bigint NULL,
duration bigint NULL,
database_name sysname NULL,
client_app_name nvarchar(256) NULL,
client_hostname nvarchar(256) NULL,
username nvarchar(256) NULL,
sql_text nvarchar(max) NULL
);

DECLARE @session sysname = N'app*trace*<<TRACE_ID_WITHOUT_DASHES>>';

WITH x AS (
SELECT CAST(t.target_data AS xml) AS xdata
FROM sys.dm_xe_session_targets t
JOIN sys.dm_xe_sessions s ON s.address = t.event_session_address
WHERE s.name = @session AND t.target_name = N'ring_buffer'
)
INSERT dbo.AppTraceRingDump(event_time,name,cpu_time,duration,database_name,client_app_name,client_hostname,username,sql_text)
SELECT
DATEADD(hh, DATEDIFF(hh, SYSUTCDATETIME(), SYSDATETIME()), n.value('@timestamp','datetime2')),
n.value('@name','sysname'),
n.value('(data[@name="cpu_time"]/value)[1]','bigint'),
n.value('(data[@name="duration"]/value)[1]','bigint'),
n.value('(action[@name="database_name"]/value)[1]','sysname'),
n.value('(action[@name="client_app_name"]/value)[1]','nvarchar(256)'),
n.value('(action[@name="client_hostname"]/value)[1]','nvarchar(256)'),
n.value('(action[@name="username"]/value)[1]','nvarchar(256)'),
n.value('(action[@name="sql_text"]/value)[1]','nvarchar(max)')
FROM x
CROSS APPLY x.xdata.nodes('/RingBufferTarget/event') AS q(n);

Tiny API add-on: endpoint to export the trace

If you want your API to hand back the saved data (works for either event_file or ring_buffer), you can stream the rows as NDJSON or CSV:

[HttpGet("export/{id:guid}")]
public async Task Export([FromRoute] Guid id, CancellationToken ct)
{
Response.ContentType = "application/json; charset=utf-8";
await using var conn = new SqlConnection(\_svc.ConnectionStringFor(id)!);
await conn.OpenAsync(ct);

    // If event_file: point the TVF to your path; if ring_buffer: use the dump SELECT
    var sql = @"

SELECT
DATEADD(hh, DATEDIFF(hh, SYSUTCDATETIME(), SYSDATETIME()), x.event_data.value('(event/@timestamp)[1]','datetime2')) AS event_time,
x.event_data.value('(event/@name)[1]','sysname') AS name,
x.event_data.value('(event/action[@name=""database_name""]/value)[1]','sysname') AS database_name,
x.event_data.value('(event/action[@name=""client_app_name""]/value)[1]','nvarchar(256)') AS client_app_name,
x.event_data.value('(event/action[@name=""client_hostname""]/value)[1]','nvarchar(256)') AS client_hostname,
x.event_data.value('(event/action[@name=""username""]/value)[1]','nvarchar(256)') AS username,
x.event_data.value('(event/action[@name=""sql_text""]/value)[1]','nvarchar(max)') AS sql_text
FROM sys.fn_xe_file_target_read_file(@path, NULL, NULL, NULL) AS F
CROSS APPLY (SELECT TRY_CAST(F.event_data AS XML)) AS T(event_data);";

    using var cmd = new SqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("@path", @"D:\XE\app_trace_*" + id.ToString("N") + "*.xel"); // or Linux path
    using var r = await cmd.ExecuteReaderAsync(ct);

    await Response.StartAsync(ct);
    while (await r.ReadAsync(ct))
    {
        var obj = new {
            event_time = r.GetDateTime(0),
            name = r.GetString(1),
            database_name = r.IsDBNull(2) ? null : r.GetString(2),
            client_app_name = r.IsDBNull(3) ? null : r.GetString(3),
            client_hostname = r.IsDBNull(4) ? null : r.GetString(4),
            username = r.IsDBNull(5) ? null : r.GetString(5),
            sql_text = r.IsDBNull(6) ? null : r.GetString(6)
        };
        await Response.WriteAsJsonAsync(obj, cancellationToken: ct);
    }
    await Response.Body.FlushAsync(ct);

}

Quick picks

Want durable, roll-over files and offline analysis? Use event_file with max_file_size + max_rollover_files.

Just need quick capture without touching the server’s filesystem? Use ring_buffer then dump to a table (or export via API).
