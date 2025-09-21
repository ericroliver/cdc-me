using Microsoft.AspNetCore.Mvc;
using cdc_api.Models;
using Softbase;
using Softbase.Cdc;
using Softbase.Cdc.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace cdc_api.Controllers;

/// <summary>
/// Controller for CDC (Change Data Capture) operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CdcController : ControllerBase
{
    private readonly ILogger<CdcController> _logger;
    private readonly IDatabaseConnectionFactory _connectionFactory;

    /// <summary>
    /// Initializes a new instance of the CdcController
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="connectionFactory">Database connection factory</param>
    public CdcController(ILogger<CdcController> logger, IDatabaseConnectionFactory connectionFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// Start CDC operations on the database with optional table filtering
    /// </summary>
    /// <param name="request">CDC start request parameters</param>
    /// <returns>Result of the CDC start operation</returns>
    [HttpPost("start")]
    public async Task<ActionResult<StartCdcResponse>> StartCdc([FromBody] StartCdcRequest request)
    {
        try
        {
            _logger.LogInformation("Starting CDC for session {SessionName}", request.SessionName);

            var response = new StartCdcResponse
            {
                SessionName = request.SessionName
            };

            // Create DAC for test database (SQL Server)
            var testDac = _connectionFactory.CreateDac(DatabaseRole.TestDatabase, _logger);

            // Step 1: Enable CDC on database
            _logger.LogDebug("Enabling CDC on database");
            CdcDataUtilities.EnableCdcOnDatabase(testDac);

            // Step 2: Get all tables and apply filtering
            var allTables = CdcDataUtilities.GetTables(testDac);
            var filteredTables = FilterTables(allTables, request.TablesToInclude, request.TablesToExclude);

            _logger.LogDebug("Found {TotalTables} total tables, {FilteredTables} after filtering",
                allTables.Count(), filteredTables.Count());

            // Step 3: Enable CDC on filtered tables
            var tablesEnabled = new List<string>();
            var tablesSkipped = new List<string>();
            var errors = new List<string>();

            foreach (var table in filteredTables)
            {
                try
                {
                    _logger.LogDebug("Enabling CDC for table {Schema}.{Table}", table.Schema, table.Name);

                    // Check if table has primary key (required for CDC)
                    if (!table.HasPrimaryKey)
                    {
                        var skipMessage = $"Table {table.Schema}.{table.Name} skipped - no primary key";
                        _logger.LogWarning(skipMessage);
                        tablesSkipped.Add($"{table.Schema}.{table.Name}");
                        continue;
                    }

                    // Enable CDC on this table
                    CdcDataUtilities.EnableTableCdc(testDac, new[] { table }, _logger);
                    tablesEnabled.Add($"{table.Schema}.{table.Name}");
                }
                catch (Exception ex)
                {
                    var errorMessage = $"Failed to enable CDC on table {table.Schema}.{table.Name}: {ex.Message}";
                    _logger.LogError(ex, errorMessage);
                    errors.Add(errorMessage);
                    tablesSkipped.Add($"{table.Schema}.{table.Name}");
                }
            }

            // Step 4: Create or update session in trace database
            await CreateOrUpdateSessionAsync(request.SessionName, request.TablesToInclude, request.TablesToExclude);

            // Build response
            response.Success = true;
            response.Message = $"CDC enabled successfully on {tablesEnabled.Count} tables";
            response.TablesEnabled = tablesEnabled;
            response.TablesSkipped = tablesSkipped;
            response.Errors = errors;

            _logger.LogInformation("CDC started successfully for session {SessionName}. Enabled: {EnabledCount}, Skipped: {SkippedCount}",
                request.SessionName, tablesEnabled.Count, tablesSkipped.Count);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting CDC for session {SessionName}", request.SessionName);
            return BadRequest(new StartCdcResponse
            {
                Success = false,
                SessionName = request.SessionName,
                Message = $"Error starting CDC: {ex.Message}",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Stop CDC operations, capture data, and save to CdcMe database
    /// </summary>
    /// <param name="request">CDC stop request parameters</param>
    /// <returns>Result of the CDC stop operation</returns>
    [HttpPost("stop")]
    public async Task<ActionResult<StopCdcResponse>> StopCdc([FromBody] StopCdcRequest request)
    {
        try
        {
            _logger.LogInformation("Stopping CDC for session {SessionName}, capture {CaptureName}",
                request.SessionName, request.CaptureName);

            var response = new StopCdcResponse
            {
                SessionName = request.SessionName,
                CaptureName = request.CaptureName
            };

            // Create DACs for both databases
            var testDac = _connectionFactory.CreateDac(DatabaseRole.TestDatabase, _logger);
            var cdcMeDac = _connectionFactory.CreateDac(DatabaseRole.CdcMeDatabase, _logger);

            // Step 1: Capture CDC data
            var allTables = CdcDataUtilities.GetTables(testDac);
            var cdcData = CdcDataUtilities.BuildProfile(testDac, allTables, _logger);

            _logger.LogDebug("Captured CDC data from {TableCount} tables", cdcData.Count);

            // Step 2: Save captured data to CdcMe database
            var captureHeaderId = await SaveCdcCaptureAsync(
                cdcMeDac,
                request.SessionName,
                request.CaptureName,
                request.CaptureType,
                cdcData,
                allTables.Select(t => $"{t.Schema}.{t.Name}").ToList(),
                new List<string>() // tablesSkipped - we'll enhance this later
            );

            // Step 3: Disable CDC on database
            _logger.LogDebug("Disabling CDC on database");
            CdcDataUtilities.DisableCdcOnDatabase(testDac);

            // Build response
            var tablesWithChanges = cdcData.Keys.ToList();
            var totalRecords = cdcData.Values.Sum(tableData => tableData.Count());

            response.Success = true;
            response.Message = $"CDC data captured and CDC disabled successfully";
            response.TablesWithChanges = tablesWithChanges;
            response.TotalRecords = totalRecords;
            response.CaptureId = captureHeaderId;

            _logger.LogInformation("CDC stopped successfully for session {SessionName}. Captured {RecordCount} records from {TableCount} tables",
                request.SessionName, totalRecords, tablesWithChanges.Count);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping CDC for session {SessionName}", request.SessionName);
            return BadRequest(new StopCdcResponse
            {
                Success = false,
                SessionName = request.SessionName,
                CaptureName = request.CaptureName,
                Message = $"Error stopping CDC: {ex.Message}",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Capture CDC data without stopping CDC (for intermediate captures)
    /// </summary>
    /// <param name="request">CDC capture request parameters</param>
    /// <returns>Result of the CDC capture operation</returns>
    [HttpPost("capture")]
    public async Task<ActionResult<CaptureCdcResponse>> CaptureCdc([FromBody] CaptureCdcRequest request)
    {
        try
        {
            _logger.LogInformation("Capturing CDC data for session {SessionName}, capture {CaptureName}",
                request.SessionName, request.CaptureName);

            var response = new CaptureCdcResponse
            {
                SessionName = request.SessionName,
                CaptureName = request.CaptureName,
                CaptureType = request.CaptureType
            };

            // Create DACs for both databases
            var testDac = _connectionFactory.CreateDac(DatabaseRole.TestDatabase, _logger);
            var cdcMeDac = _connectionFactory.CreateDac(DatabaseRole.CdcMeDatabase, _logger);

            // Step 1: Capture CDC data (without stopping CDC)
            var allTables = CdcDataUtilities.GetTables(testDac);
            var cdcData = CdcDataUtilities.BuildProfile(testDac, allTables, _logger);

            _logger.LogDebug("Captured CDC data from {TableCount} tables", cdcData.Count);

            // Step 2: Save captured data to CdcMe database
            var captureHeaderId = await SaveCdcCaptureAsync(
                cdcMeDac,
                request.SessionName,
                request.CaptureName,
                request.CaptureType,
                cdcData,
                allTables.Select(t => $"{t.Schema}.{t.Name}").ToList(),
                new List<string>() // tablesSkipped
            );

            // Build response
            var tablesWithChanges = cdcData.Keys.ToList();
            var totalRecords = cdcData.Values.Sum(tableData => tableData.Count());

            response.Success = true;
            response.Message = $"CDC data captured successfully (CDC still active)";
            response.TablesWithChanges = tablesWithChanges;
            response.TotalRecords = totalRecords;
            response.CaptureId = captureHeaderId;

            _logger.LogInformation("CDC captured successfully for session {SessionName}. Captured {RecordCount} records from {TableCount} tables",
                request.SessionName, totalRecords, tablesWithChanges.Count);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing CDC for session {SessionName}", request.SessionName);
            return BadRequest(new CaptureCdcResponse
            {
                Success = false,
                SessionName = request.SessionName,
                CaptureName = request.CaptureName,
                CaptureType = request.CaptureType,
                Message = $"Error capturing CDC: {ex.Message}",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Filter tables based on include/exclude criteria
    /// </summary>
    /// <param name="allTables">All available tables</param>
    /// <param name="tablesToInclude">Tables to include (optional)</param>
    /// <param name="tablesToExclude">Tables to exclude (optional)</param>
    /// <returns>Filtered list of tables</returns>
    private static IEnumerable<SqlTable> FilterTables(
        IEnumerable<SqlTable> allTables,
        List<string>? tablesToInclude,
        List<string>? tablesToExclude)
    {
        var tables = allTables;

        // Apply include filter if specified
        if (tablesToInclude != null && tablesToInclude.Any())
        {
            var includeSet = new HashSet<string>(tablesToInclude, StringComparer.OrdinalIgnoreCase);
            tables = tables.Where(t => includeSet.Contains($"{t.Schema}.{t.Name}"));
        }

        // Apply exclude filter if specified
        if (tablesToExclude != null && tablesToExclude.Any())
        {
            var excludeSet = new HashSet<string>(tablesToExclude, StringComparer.OrdinalIgnoreCase);
            tables = tables.Where(t => !excludeSet.Contains($"{t.Schema}.{t.Name}"));
        }

        return tables;
    }

    /// <summary>
    /// Create or update session in trace database
    /// </summary>
    /// <param name="sessionName">Name of the session</param>
    /// <param name="tablesToInclude">Tables to include</param>
    /// <param name="tablesToExclude">Tables to exclude</param>
    private async Task CreateOrUpdateSessionAsync(
        string sessionName,
        List<string>? tablesToInclude,
        List<string>? tablesToExclude)
    {
        var cdcMeDac = _connectionFactory.CreateDac(DatabaseRole.CdcMeDatabase, _logger);

        var configuration = new
        {
            tablesToInclude = tablesToInclude ?? new List<string>(),
            tablesToExclude = tablesToExclude ?? new List<string>()
        };

        const string upsertSql = @"
            INSERT INTO trace_sessions (session_name, test_database, description, configuration)
            VALUES (@sessionName, @testDatabase, @description, @configuration::jsonb)
            ON CONFLICT (session_name)
            DO UPDATE SET
                configuration = @configuration::jsonb,
                start_time = NOW()";

        var parameters = new Dictionary<string, object>
        {
            ["sessionName"] = sessionName,
            ["testDatabase"] = "TestDatabase", // Could be made configurable
            ["description"] = $"CDC session created via API",
            ["configuration"] = JsonSerializer.Serialize(configuration)
        };

        await cdcMeDac.ExecuteCommandAsync(upsertSql, parameters);
        _logger.LogDebug("Created/updated session {SessionName}", sessionName);
    }

    /// <summary>
    /// Save CDC capture data to CdcMe database using header-detail pattern
    /// </summary>
    /// <param name="cdcMeDac">CdcMe database connection</param>
    /// <param name="sessionName">Session name</param>
    /// <param name="captureName">Capture name</param>
    /// <param name="captureType">Type of capture</param>
    /// <param name="cdcData">CDC data to save</param>
    /// <param name="tablesEnabled">Tables that were enabled</param>
    /// <param name="tablesSkipped">Tables that were skipped</param>
    /// <returns>Capture header ID</returns>
    private async Task<string> SaveCdcCaptureAsync(
        SimpleDac cdcMeDac,
        string sessionName,
        string captureName,
        string captureType,
        IDictionary<string, IEnumerable<IDictionary<string, object>>> cdcData,
        List<string> tablesEnabled,
        List<string> tablesSkipped)
    {
        // Step 1: Get session ID
        const string getSessionSql = "SELECT session_id FROM trace_sessions WHERE session_name = @sessionName";
        var sessionId = await cdcMeDac.ExecuteScalarAsync<Guid>(getSessionSql,
            new Dictionary<string, object> { ["sessionName"] = sessionName });

        if (sessionId == Guid.Empty)
        {
            throw new InvalidOperationException($"Session '{sessionName}' not found. Please start CDC first.");
        }

        // Step 2: Create capture header
        const string insertHeaderSql = @"
            INSERT INTO cdc_capture_headers (
                session_id, capture_name, capture_type, tables_enabled,
                tables_skipped, total_records, status
            ) VALUES (
                @sessionId, @captureName, @captureType, @tablesEnabled::jsonb,
                @tablesSkipped::jsonb, @totalRecords, @status
            ) RETURNING capture_header_id";

        var totalRecords = cdcData.Values.Sum(tableData => tableData.Count());
        var captureHeaderId = await cdcMeDac.ExecuteScalarAsync<Guid>(insertHeaderSql, new Dictionary<string, object>
        {
            ["sessionId"] = sessionId,
            ["captureName"] = captureName,
            ["captureType"] = captureType,
            ["tablesEnabled"] = JsonSerializer.Serialize(tablesEnabled),
            ["tablesSkipped"] = JsonSerializer.Serialize(tablesSkipped),
            ["totalRecords"] = totalRecords,
            ["status"] = "Completed"
        });

        // Step 3: Create capture details for each table
        const string insertDetailSql = @"
            INSERT INTO cdc_captures (
                capture_header_id, table_name, capture_data, record_count, data_hash
            ) VALUES (
                @captureHeaderId, @tableName, @captureData::jsonb, @recordCount, @dataHash
            )";

        foreach (var tableData in cdcData)
        {
            var tableName = tableData.Key;
            var data = tableData.Value;
            var recordCount = data.Count();

            if (recordCount > 0)
            {
                var jsonData = JsonSerializer.Serialize(data);
                var dataHash = ComputeSha256Hash(jsonData);

                await cdcMeDac.ExecuteCommandAsync(insertDetailSql, new Dictionary<string, object>
                {
                    ["captureHeaderId"] = captureHeaderId,
                    ["tableName"] = tableName,
                    ["captureData"] = jsonData,
                    ["recordCount"] = recordCount,
                    ["dataHash"] = dataHash
                });
            }
        }

        _logger.LogDebug("Saved CDC capture {CaptureName} with header ID {HeaderId}",
            captureName, captureHeaderId);

        return captureHeaderId.ToString();
    }

    /// <summary>
    /// Compute SHA256 hash of a string
    /// </summary>
    /// <param name="input">Input string</param>
    /// <returns>SHA256 hash as hex string</returns>
    private static string ComputeSha256Hash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
