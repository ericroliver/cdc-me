using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CdcModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Softbase;
using Softbase.Cdc;
using Softbase.Cdc.Data;

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
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

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

            // Log all table names for debugging
            _logger.LogDebug("All tables from database: {Tables}",
                string.Join(", ", allTables.Select(t => $"{t.Schema}.{t.Name}")));
            _logger.LogDebug("Tables to include from request: {Include}",
                request.TablesToInclude != null ? string.Join(", ", request.TablesToInclude) : "null");

            var filteredTables = FilterTables(allTables, request.TablesToInclude, request.TablesToExclude);

            _logger.LogDebug("Found {TotalTables} total tables, {FilteredTables} after filtering",
                allTables.Count(), filteredTables.Count());
            _logger.LogDebug("Filtered tables: {FilteredTableNames}",
                string.Join(", ", filteredTables.Select(t => $"{t.Schema}.{t.Name}")));

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
                    // SECURITY: Log detailed error server-side only
                    _logger.LogError(ex, "Failed to enable CDC on table {Schema}.{TableName}", table.Schema, table.Name);
                    errors.Add($"Failed to enable CDC on table {table.Schema}.{table.Name}");
                    tablesSkipped.Add($"{table.Schema}.{table.Name}");
                }
            }

            // Step 4: Check if user requested specific tables but none were enabled
            if (request.TablesToInclude != null && request.TablesToInclude.Any() && tablesEnabled.Count == 0)
            {
                var errorMsg = $"None of the {request.TablesToInclude.Count} requested tables could be CDC-enabled. " +
                              $"Tables may not exist, lack primary keys, or have other issues. Check tablesSkipped and errors for details.";
                _logger.LogError(errorMsg);

                _logger.LogError("No tables could be CDC-enabled for session {SessionName}", request.SessionName);
                return BadRequest(new { error = "No tables could be CDC-enabled. Tables may not exist, lack primary keys, or have other issues. Check server logs for details." });
            }

            // Step 5: Create or update session in trace database (non-blocking - don't fail if this fails)
            try
            {
                await CreateOrUpdateSessionAsync(request.SessionName, request.TablesToInclude, request.TablesToExclude);
            }
            catch (Exception sessionEx)
            {
                _logger.LogWarning(sessionEx, "Failed to create/update session in trace database. CDC is still enabled. Session: {SessionName}", request.SessionName);
                // Don't fail the whole operation - CDC is already enabled successfully
            }

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
            return BadRequest(new { error = "Failed to start CDC. Please check server logs for details." });
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
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            _logger.LogInformation("Stopping CDC for session {SessionName}, capture {CaptureName}",
                request.SessionName, request.CaptureName);

            var testDac = _connectionFactory.CreateDac(DatabaseRole.TestDatabase, _logger);
            var cdcMeDac = _connectionFactory.CreateDac(DatabaseRole.CdcMeDatabase, _logger);

            // Check if CDC is enabled before attempting to capture
            if (!CdcDataUtilities.IsCdcEnabled(testDac))
            {
                _logger.LogWarning("CDC is not enabled on the database. Cannot capture CDC data.");
                return NotFound(new { error = "CDC is not currently running. No active session found." });
            }

            // Perform CDC capture
            var captureResult = await PerformCdcCaptureAsync(
                testDac,
                cdcMeDac,
                request.SessionName,
                request.CaptureName,
                request.CaptureType);

            // Handle capture failure - still disable CDC
            if (!captureResult.IsSuccess)
            {
                _logger.LogDebug("Disabling CDC on database after capture errors");
                CdcDataUtilities.DisableCdcOnDatabase(testDac);

                _logger.LogError("CDC capture failed for session {SessionName}: {ErrorMessage}", request.SessionName, captureResult.ErrorMessage);
                return BadRequest(new { error = "CDC was stopped but data capture failed. Please check server logs for details." });
            }

            // Disable CDC on database after successful capture
            _logger.LogDebug("Disabling CDC on database");
            CdcDataUtilities.DisableCdcOnDatabase(testDac);

            var response = new StopCdcResponse
            {
                Success = true,
                SessionName = request.SessionName,
                CaptureName = request.CaptureName,
                Message = "CDC data captured and CDC disabled successfully",
                TablesWithChanges = captureResult.TablesWithChanges,
                TotalRecords = captureResult.TotalRecords,
                CaptureId = captureResult.CaptureId
            };

            _logger.LogInformation("CDC stopped successfully for session {SessionName}. Captured {RecordCount} records from {TableCount} tables",
                request.SessionName, captureResult.TotalRecords, captureResult.TablesWithChanges.Count);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping CDC for session {SessionName}", request.SessionName);
            return BadRequest(new { error = "Failed to stop CDC. Please check server logs for details." });
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

            var testDac = _connectionFactory.CreateDac(DatabaseRole.TestDatabase, _logger);
            var cdcMeDac = _connectionFactory.CreateDac(DatabaseRole.CdcMeDatabase, _logger);

            // Perform CDC capture
            var captureResult = await PerformCdcCaptureAsync(
                testDac,
                cdcMeDac,
                request.SessionName,
                request.CaptureName,
                request.CaptureType);

            // Handle capture failure
            if (!captureResult.IsSuccess)
            {
                _logger.LogError("CDC capture failed for session {SessionName}: {ErrorMessage}", request.SessionName, captureResult.ErrorMessage);
                return BadRequest(new { error = "Failed to capture CDC data. Please check server logs for details." });
            }

            var response = new CaptureCdcResponse
            {
                Success = true,
                SessionName = request.SessionName,
                CaptureName = request.CaptureName,
                CaptureType = request.CaptureType,
                Message = "CDC data captured successfully (CDC still active)",
                TablesWithChanges = captureResult.TablesWithChanges,
                TotalRecords = captureResult.TotalRecords,
                CaptureId = captureResult.CaptureId
            };

            _logger.LogInformation("CDC captured successfully for session {SessionName}. Captured {RecordCount} records from {TableCount} tables",
                request.SessionName, captureResult.TotalRecords, captureResult.TablesWithChanges.Count);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing CDC for session {SessionName}", request.SessionName);
            return BadRequest(new { error = "Failed to capture CDC data. Please check server logs for details." });
        }
    }

    /// <summary>
    /// Result of CDC capture operation
    /// </summary>
    private class CdcCaptureResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<string> TablesWithChanges { get; set; } = new();
        public int TotalRecords { get; set; }
        public string CaptureId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Perform CDC capture operation (common logic for both stop and capture endpoints)
    /// </summary>
    /// <param name="testDac">Test database connection</param>
    /// <param name="cdcMeDac">CdcMe database connection</param>
    /// <param name="sessionName">Session name</param>
    /// <param name="captureName">Capture name</param>
    /// <param name="captureType">Capture type</param>
    /// <returns>CDC capture result</returns>
    private async Task<CdcCaptureResult> PerformCdcCaptureAsync(
        SimpleDac testDac,
        SimpleDac cdcMeDac,
        string sessionName,
        string captureName,
        string captureType)
    {
        // Step 1: Get session configuration to determine which tables to capture
        var sessionConfig = await GetSessionConfigurationAsync(cdcMeDac, sessionName);

        // Step 2: Get all tables and apply session filters
        var allTables = CdcDataUtilities.GetTables(testDac);
        var filteredTables = FilterTables(allTables, sessionConfig.TablesToInclude, sessionConfig.TablesToExclude);

        _logger.LogDebug("Capturing CDC data for {FilteredCount} of {TotalCount} tables based on session filters",
            filteredTables.Count(), allTables.Count());

        // If user requested specific tables but none match the filter, fail
        if (sessionConfig.TablesToInclude != null && sessionConfig.TablesToInclude.Any() && !filteredTables.Any())
        {
            var errorMsg = $"None of the {sessionConfig.TablesToInclude.Count} tables from session configuration could be found or are CDC-enabled. " +
                          "Tables may have been disabled, dropped, or the session configuration is incorrect.";
            _logger.LogError(errorMsg);
            return new CdcCaptureResult
            {
                IsSuccess = false,
                ErrorMessage = errorMsg
            };
        }

        // If no tables match (and no specific tables were requested), return empty success
        if (!filteredTables.Any())
        {
            _logger.LogWarning("No CDC-enabled tables found to capture data from.");
            return new CdcCaptureResult
            {
                IsSuccess = true,
                TablesWithChanges = new List<string>(),
                TotalRecords = 0,
                CaptureId = string.Empty
            };
        }

        // Step 3: Capture CDC data (net changes only) for filtered tables
        var cdcResult = CdcDataUtilities.BuildNetProfile(testDac, filteredTables, _logger);

        _logger.LogDebug("Captured CDC data from {TableCount} tables", cdcResult.Data.Count);

        // Check for capture errors
        if (!cdcResult.IsSuccess)
        {
            return new CdcCaptureResult
            {
                IsSuccess = false,
                ErrorMessage = string.Join("; ", cdcResult.Errors)
            };
        }

        // Step 2: Save captured data to CdcMe database
        var captureHeaderId = await SaveCdcCaptureAsync(
            cdcMeDac,
            sessionName,
            captureName,
            captureType,
            cdcResult.Data,
            allTables.Select(t => $"{t.Schema}.{t.Name}").ToList(),
            new List<string>() // tablesSkipped - we'll enhance this later
        );

        // Build result
        var tablesWithChanges = cdcResult.Data.Keys.ToList();
        var totalRecords = cdcResult.Data.Values.Sum(tableData => tableData.Count());

        return new CdcCaptureResult
        {
            IsSuccess = true,
            TablesWithChanges = tablesWithChanges,
            TotalRecords = totalRecords,
            CaptureId = captureHeaderId
        };
    }

    /// <summary>
    /// Session configuration retrieved from database
    /// </summary>
    private class SessionConfiguration
    {
        public List<string>? TablesToInclude { get; set; }
        public List<string>? TablesToExclude { get; set; }
    }

    /// <summary>
    /// Get session configuration from trace database
    /// </summary>
    /// <param name="cdcMeDac">CdcMe database connection</param>
    /// <param name="sessionName">Session name</param>
    /// <returns>Session configuration</returns>
    private async Task<SessionConfiguration> GetSessionConfigurationAsync(SimpleDac cdcMeDac, string sessionName)
    {
        const string sql = "SELECT configuration FROM trace_sessions WHERE session_name = @sessionName";
        var parameters = new Dictionary<string, object>
        {
            ["sessionName"] = sessionName
        };

        var configJson = await cdcMeDac.ExecuteScalarAsync<string>(sql, parameters);

        if (string.IsNullOrEmpty(configJson))
        {
            _logger.LogWarning("No configuration found for session {SessionName}, using default (no filters)", sessionName);
            return new SessionConfiguration();
        }

        var config = JsonSerializer.Deserialize<SessionConfiguration>(configJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return config ?? new SessionConfiguration();
    }

    /// <summary>
    /// Filter tables based on include/exclude criteria
    /// </summary>
    /// <param name="allTables">All available tables</param>
    /// <param name="tablesToInclude">Tables to include (optional)</param>
    /// <param name="tablesToExclude">Tables to exclude (optional)</param>
    /// <returns>Filtered list of tables</returns>
    internal static IEnumerable<SqlTable> FilterTables(
        IEnumerable<SqlTable> allTables,
        List<string>? tablesToInclude,
        List<string>? tablesToExclude)
    {
        var tables = allTables;

        // Apply include filter if specified
        if (tablesToInclude != null && tablesToInclude.Any())
        {
            var includeSet = new HashSet<string>(tablesToInclude, StringComparer.OrdinalIgnoreCase);
            tables = tables.Where(t =>
                includeSet.Contains(t.Name) ||
                includeSet.Contains($"{t.Schema}.{t.Name}"));
        }

        // Apply exclude filter if specified
        if (tablesToExclude != null && tablesToExclude.Any())
        {
            var excludeSet = new HashSet<string>(tablesToExclude, StringComparer.OrdinalIgnoreCase);
            tables = tables.Where(t =>
                !excludeSet.Contains(t.Name) &&
                !excludeSet.Contains($"{t.Schema}.{t.Name}"));
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

        // Extract actual database name from connection string
        var testConnectionString = _connectionFactory.GetConnectionString(DatabaseRole.TestDatabase);
        var databaseName = ExtractDatabaseNameFromConnectionString(testConnectionString);

        var parameters = new Dictionary<string, object>
        {
            ["sessionName"] = sessionName,
            ["testDatabase"] = databaseName,
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
        // Wrap entire operation in a transaction to ensure data consistency
        using var transaction = await cdcMeDac.BeginTransactionAsync();
        try
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

            // Commit the transaction if all operations succeeded
            await transaction.CommitAsync();

            _logger.LogDebug("Saved CDC capture {CaptureName} with header ID {HeaderId}",
                captureName, captureHeaderId);

            return captureHeaderId.ToString();
        }
        catch (Exception ex)
        {
            // Rollback transaction on any error
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to save CDC capture {CaptureName}, transaction rolled back", captureName);
            throw;
        }
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

    /// <summary>
    /// Extract database name from SQL Server connection string
    /// </summary>
    /// <param name="connectionString">SQL Server connection string</param>
    /// <returns>Database name</returns>
    private static string ExtractDatabaseNameFromConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return "Unknown";

        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            return builder.InitialCatalog ?? "Unknown";
        }
        catch
        {
            // Fallback: try to parse manually
            var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var keyValue = part.Split('=', 2);
                if (keyValue.Length == 2)
                {
                    var key = keyValue[0].Trim().ToLowerInvariant();
                    if (key == "database" || key == "initial catalog")
                    {
                        return keyValue[1].Trim();
                    }
                }
            }
            return "Unknown";
        }
    }

    /// <summary>
    /// Compare two CDC captures to validate that they produce identical data changes
    /// </summary>
    /// <param name="request">Comparison request parameters</param>
    /// <returns>Detailed comparison results</returns>
    [HttpPost("compare")]
    public async Task<ActionResult<CdcModels.CompareCapturesResponse>> CompareCapturesAsync([FromBody] CdcModels.CompareCapturesRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            _logger.LogInformation("Starting comparison between baseline '{Baseline}' and test '{Test}'",
                request.BaselineCaptureName, request.TestCaptureName);

            // Create connection to trace database
            var traceDac = _connectionFactory.CreateDac(DatabaseRole.CdcMeDatabase, _logger);

            // Create comparer and perform comparison
            var comparer = new CdcCaptureComparer(traceDac, _logger);
            var cdcRequest = new Softbase.Cdc.CompareCapturesRequest
            {
                BaselineCaptureName = request.BaselineCaptureName,
                TestCaptureName = request.TestCaptureName,
                FieldsToIgnore = request.FieldsToIgnore ?? new List<string>(),
                IgnoreLsnDifferences = request.IgnoreLsnDifferences
            };

            var result = await comparer.CompareCapturesAsync(cdcRequest);

            // Map to API response model
            var response = new CdcModels.CompareCapturesResponse
            {
                IsMatch = result.IsMatch,
                Failures = result.Failures.Select(f => new CdcModels.CaptureComparisonFailure
                {
                    TableName = f.TableName,
                    FailureType = f.FailureType,
                    PrimaryKey = f.PrimaryKey,
                    FieldName = f.FieldName,
                    BaselineValue = f.BaselineValue,
                    TestValue = f.TestValue,
                    Description = f.Description
                }).ToList(),
                Summary = new CdcModels.ComparisonSummary
                {
                    TablesCompared = result.Summary.TablesCompared,
                    RecordsCompared = result.Summary.RecordsCompared,
                    FieldsCompared = result.Summary.FieldsCompared,
                    TotalFailures = result.Summary.TotalFailures,
                    TablesWithFailures = result.Summary.TablesWithFailures,
                    ComparisonDuration = result.Summary.ComparisonDuration
                },
                Errors = result.Errors
            };

            // If comparison produced errors (e.g., missing captures), return BadRequest
            if (result.Errors != null && result.Errors.Any())
            {
                _logger.LogWarning("Comparison produced errors: {Errors}", string.Join("; ", result.Errors));
                return BadRequest(new { error = "Comparison failed. Required captures not found or other errors occurred. Please check server logs for details." });
            }

            if (result.IsMatch)
            {
                _logger.LogInformation("Comparison successful: captures match exactly. Compared {TablesCompared} tables, {RecordsCompared} records in {Duration}ms",
                    result.Summary.TablesCompared, result.Summary.RecordsCompared, result.Summary.ComparisonDuration.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning("Comparison failed: {FailureCount} differences found across {TablesWithFailures} tables",
                    result.Summary.TotalFailures, result.Summary.TablesWithFailures);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing captures");
            return BadRequest(new { error = "Failed to compare captures. Please check server logs for details." });
        }
    }
}
