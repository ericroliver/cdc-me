# Code Examples and Sample Configurations

## Overview

This document provides comprehensive code examples, sample configurations, and implementation patterns for the CDC Testing Framework. These examples demonstrate best practices and common usage scenarios.

## Core Library Examples

### Basic CDC Operations

#### Simple CDC Initialization

```csharp
using Microsoft.Extensions.Logging;
using Softbase;
using Softbase.Cdc;

// Setup logging
var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger("CDC-Example");

// Connection string
var connectionString = "Server=localhost;Database=TestDB;Integrated Security=true;TrustServerCertificate=true;";

// Create data access component
using var dac = new SimpleDac(connectionString, logger);

try
{
    // Enable CDC on database
    CdcDataUtilities.EnableCdcOnDatabase(dac);
    logger.LogInformation("CDC enabled on database");

    // Get all tables
    var tables = CdcDataUtilities.GetTables(dac);
    logger.LogInformation($"Found {tables.Count()} tables");

    // Enable CDC on tables with primary keys
    CdcDataUtilities.EnableTableCdc(dac, tables, logger);
    logger.LogInformation("CDC enabled on eligible tables");
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to initialize CDC");
    throw;
}
```

#### Advanced Profile Generation

```csharp
public class AdvancedProfileGenerator
{
    private readonly SimpleDac _dac;
    private readonly ILogger _logger;

    public AdvancedProfileGenerator(SimpleDac dac, ILogger logger)
    {
        _dac = dac;
        _logger = logger;
    }

    public async Task<ProfileResult> GenerateProfileWithMetadataAsync(
        string profileName,
        DateTime? fromTime = null,
        DateTime? toTime = null)
    {
        var startTime = DateTime.UtcNow;
        var tables = CdcDataUtilities.GetTables(_dac);
        var profileData = new Dictionary<string, object>();

        // Add metadata
        profileData["metadata"] = new
        {
            ProfileName = profileName,
            GeneratedAt = startTime,
            FromTime = fromTime,
            ToTime = toTime,
            TableCount = tables.Count(),
            Generator = "AdvancedProfileGenerator v1.0"
        };

        // Generate profile with time filtering if specified
        IDictionary<string, IEnumerable<IDictionary<string, object>>> changes;

        if (fromTime.HasValue && toTime.HasValue)
        {
            changes = await GenerateTimeFilteredProfileAsync(tables, fromTime.Value, toTime.Value);
        }
        else
        {
            changes = CdcDataUtilities.BuildNetProfile(_dac, tables, _logger);
        }

        profileData["changes"] = changes;

        // Add statistics
        var stats = CalculateProfileStatistics(changes);
        profileData["statistics"] = stats;

        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        return new ProfileResult
        {
            ProfileName = profileName,
            Data = profileData,
            GenerationTime = duration,
            RecordCount = stats.TotalRecords,
            TableCount = stats.TablesWithChanges
        };
    }

    private async Task<IDictionary<string, IEnumerable<IDictionary<string, object>>>>
        GenerateTimeFilteredProfileAsync(IEnumerable<SqlTable> tables, DateTime fromTime, DateTime toTime)
    {
        var result = new Dictionary<string, IEnumerable<IDictionary<string, object>>>();

        foreach (var table in tables.Where(t => t.HasPrimaryKey))
        {
            var changes = await GetTimeFilteredChangesAsync(table, fromTime, toTime);
            if (changes.Any())
            {
                result[$"{table.Schema}_{table.Name}"] = changes;
            }
        }

        return result;
    }

    private async Task<IEnumerable<IDictionary<string, object>>> GetTimeFilteredChangesAsync(
        SqlTable table, DateTime fromTime, DateTime toTime)
    {
        var sql = $@"
            DECLARE @from_lsn binary(10), @to_lsn binary(10);

            SELECT @from_lsn = sys.fn_cdc_map_time_to_lsn('smallest greater than or equal', @fromTime);
            SELECT @to_lsn = sys.fn_cdc_map_time_to_lsn('largest less than or equal', @toTime);

            SELECT * FROM cdc.fn_cdc_get_net_changes_{table.Schema}_{table.Name}(@from_lsn, @to_lsn, 'all')
            WHERE __$start_lsn IS NOT NULL;";

        var parameters = new Dictionary<string, object>
        {
            ["@fromTime"] = fromTime,
            ["@toTime"] = toTime
        };

        return await Task.Run(() => _dac.ExecuteReader(sql, reader =>
        {
            var records = new List<IDictionary<string, object>>();
            while (reader.Read())
            {
                var record = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    record[reader.GetName(i)] = reader.GetValue(i);
                }
                records.Add(record);
            }
            return records;
        }, parameters));
    }

    private ProfileStatistics CalculateProfileStatistics(
        IDictionary<string, IEnumerable<IDictionary<string, object>>> changes)
    {
        var stats = new ProfileStatistics();

        foreach (var tableChanges in changes)
        {
            var records = tableChanges.Value.ToList();
            stats.TotalRecords += records.Count;

            if (records.Any())
            {
                stats.TablesWithChanges++;

                var operations = records.GroupBy(r => r["__$operation"])
                    .ToDictionary(g => g.Key.ToString(), g => g.Count());

                stats.OperationCounts.Add(tableChanges.Key, operations);
            }
        }

        return stats;
    }
}

public class ProfileResult
{
    public string ProfileName { get; set; }
    public Dictionary<string, object> Data { get; set; }
    public TimeSpan GenerationTime { get; set; }
    public int RecordCount { get; set; }
    public int TableCount { get; set; }
}

public class ProfileStatistics
{
    public int TotalRecords { get; set; }
    public int TablesWithChanges { get; set; }
    public Dictionary<string, Dictionary<string, int>> OperationCounts { get; set; } = new();
}
```

### Custom Profile Differ

```csharp
public class EnhancedProfileDiffer : ProfileDiffer
{
    private readonly DifferenceOptions _options;
    private readonly ILogger _logger;

    public EnhancedProfileDiffer(DifferenceOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
    }

    public DetailedDiffResult DiffWithDetails(
        IEnumerable<SqlTable> tables,
        IDictionary<string, IEnumerable<IDictionary<string, object>>> leftProfile,
        IDictionary<string, IEnumerable<IDictionary<string, object>>> rightProfile)
    {
        var startTime = DateTime.UtcNow;
        var baseDiff = base.Diff(tables, leftProfile, rightProfile);

        var result = new DetailedDiffResult
        {
            BaseDifferences = baseDiff,
            ComparisonMetadata = new ComparisonMetadata
            {
                ComparedAt = startTime,
                LeftProfileTables = leftProfile.Keys.Count,
                RightProfileTables = rightProfile.Keys.Count,
                TablesCompared = tables.Count()
            }
        };

        // Calculate detailed statistics
        result.Statistics = CalculateDifferenceStatistics(baseDiff);

        // Generate summary report
        result.Summary = GenerateDifferenceSummary(result.Statistics);

        // Apply filtering if configured
        if (_options.ExcludeFields?.Any() == true)
        {
            result.FilteredDifferences = ApplyFieldFiltering(baseDiff, _options.ExcludeFields);
        }

        result.ComparisonMetadata.ProcessingTime = DateTime.UtcNow - startTime;

        return result;
    }

    private DifferenceStatistics CalculateDifferenceStatistics(
        IDictionary<string, IDictionary<string, object>> differences)
    {
        var stats = new DifferenceStatistics();

        foreach (var tableDiff in differences)
        {
            var tableName = tableDiff.Key;
            var tableData = (IDictionary<string, object>)tableDiff.Value;
            var diffs = (List<Diff>)tableData["diff"];

            var tableStats = new TableDifferenceStatistics
            {
                TableName = tableName,
                TotalDifferences = diffs.Count,
                NewRecords = diffs.Count(d => d.Action == DiffType.New),
                ChangedRecords = diffs.Count(d => d.Action == DiffType.Changed),
                DeletedRecords = diffs.Count(d => d.Action == DiffType.Deleted)
            };

            // Calculate field-level changes
            foreach (var diff in diffs.Where(d => d.Changes != null))
            {
                foreach (var fieldChange in diff.Changes)
                {
                    if (!tableStats.FieldChangeCounts.ContainsKey(fieldChange.Key))
                        tableStats.FieldChangeCounts[fieldChange.Key] = 0;

                    tableStats.FieldChangeCounts[fieldChange.Key]++;
                }
            }

            stats.TableStatistics[tableName] = tableStats;
        }

        return stats;
    }

    private string GenerateDifferenceSummary(DifferenceStatistics statistics)
    {
        var summary = new StringBuilder();
        summary.AppendLine("=== Difference Summary ===");

        var totalTables = statistics.TableStatistics.Count;
        var totalDifferences = statistics.TableStatistics.Values.Sum(t => t.TotalDifferences);

        summary.AppendLine($"Tables with differences: {totalTables}");
        summary.AppendLine($"Total differences: {totalDifferences}");

        if (totalDifferences > 0)
        {
            var totalNew = statistics.TableStatistics.Values.Sum(t => t.NewRecords);
            var totalChanged = statistics.TableStatistics.Values.Sum(t => t.ChangedRecords);
            var totalDeleted = statistics.TableStatistics.Values.Sum(t => t.DeletedRecords);

            summary.AppendLine($"New records: {totalNew}");
            summary.AppendLine($"Changed records: {totalChanged}");
            summary.AppendLine($"Deleted records: {totalDeleted}");

            summary.AppendLine("\n=== Per Table Summary ===");
            foreach (var tableStats in statistics.TableStatistics.Values.OrderByDescending(t => t.TotalDifferences))
            {
                summary.AppendLine($"{tableStats.TableName}: {tableStats.TotalDifferences} differences " +
                    $"(N:{tableStats.NewRecords}, C:{tableStats.ChangedRecords}, D:{tableStats.DeletedRecords})");
            }
        }
        else
        {
            summary.AppendLine("✅ No differences found - profiles are identical!");
        }

        return summary.ToString();
    }
}

public class DifferenceOptions
{
    public string[] ExcludeFields { get; set; } = Array.Empty<string>();
    public bool IgnoreTimestampFields { get; set; } = true;
    public bool IgnoreCdcSystemFields { get; set; } = true;
    public TimeSpan TimestampTolerance { get; set; } = TimeSpan.FromSeconds(1);
}

public class DetailedDiffResult
{
    public IDictionary<string, IDictionary<string, object>> BaseDifferences { get; set; }
    public IDictionary<string, IDictionary<string, object>> FilteredDifferences { get; set; }
    public ComparisonMetadata ComparisonMetadata { get; set; }
    public DifferenceStatistics Statistics { get; set; }
    public string Summary { get; set; }
}

public class ComparisonMetadata
{
    public DateTime ComparedAt { get; set; }
    public int LeftProfileTables { get; set; }
    public int RightProfileTables { get; set; }
    public int TablesCompared { get; set; }
    public TimeSpan ProcessingTime { get; set; }
}

public class DifferenceStatistics
{
    public Dictionary<string, TableDifferenceStatistics> TableStatistics { get; set; } = new();
}

public class TableDifferenceStatistics
{
    public string TableName { get; set; }
    public int TotalDifferences { get; set; }
    public int NewRecords { get; set; }
    public int ChangedRecords { get; set; }
    public int DeletedRecords { get; set; }
    public Dictionary<string, int> FieldChangeCounts { get; set; } = new();
}
```

## CLI Tool Examples

### Custom Command Implementation

```csharp
// StatusCommand.cs
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using Microsoft.Extensions.Logging;
using Softbase.Cdc;

namespace Softbase
{
    public class StatusCommand : Command
    {
        private readonly SimpleDac _dac;
        private readonly ILogger _logger;

        public StatusCommand(SimpleDac dac, ILoggerFactory factory)
           : base("status", "Show CDC status and information")
        {
            _dac = dac;
            _logger = factory.CreateLogger<StatusCommand>();

            var verboseOption = new Option<bool>("--verbose", "Show detailed information");
            this.AddOption(verboseOption);

            var formatOption = new Option<string>("--format", () => "table", "Output format (table, json, csv)");
            formatOption.AddAlias("-f");
            this.AddOption(formatOption);

            this.Handler = CommandHandler.Create<bool, string>((verbose, format) =>
                this.HandleCommand(verbose, format));
        }

        private int HandleCommand(bool verbose, string format)
        {
            try
            {
                var status = GetCdcStatus(verbose);
                OutputStatus(status, format);
                return 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get CDC status");
                return 0;
            }
        }

        private CdcStatus GetCdcStatus(bool verbose)
        {
            var status = new CdcStatus();

            // Check if CDC is enabled on database
            status.IsCdcEnabled = _dac.ExecuteScalar<bool>(
                "SELECT is_cdc_enabled FROM sys.databases WHERE name = DB_NAME()");

            if (status.IsCdcEnabled)
            {
                // Get CDC tables
                var cdcTables = _dac.ExecuteReader("SELECT * FROM cdc.change_tables", reader =>
                {
                    var tables = new List<CdcTableInfo>();
                    while (reader.Read())
                    {
                        tables.Add(new CdcTableInfo
                        {
                            CaptureInstance = reader.TryReadField<string>("capture_instance"),
                            ObjectName = reader.TryReadField<string>("object_name"),
                            SourceSchema = reader.TryReadField<string>("source_schema"),
                            SourceName = reader.TryReadField<string>("source_name"),
                            StartLsn = reader.TryReadField<byte[]>("start_lsn"),
                            CreateDate = reader.TryReadField<DateTime>("create_date")
                        });
                    }
                    return tables;
                });

                status.CdcTables = cdcTables;

                if (verbose)
                {
                    // Get CDC job status
                    status.CdcJobs = GetCdcJobStatus();

                    // Get CDC table sizes
                    status.TableSizes = GetCdcTableSizes();
                }
            }

            return status;
        }

        private void OutputStatus(CdcStatus status, string format)
        {
            switch (format.ToLower())
            {
                case "json":
                    Console.WriteLine(status.ToJson(true));
                    break;
                case "csv":
                    OutputCsvFormat(status);
                    break;
                default:
                    OutputTableFormat(status);
                    break;
            }
        }

        private void OutputTableFormat(CdcStatus status)
        {
            Console.WriteLine("=== CDC Status ===");
            Console.WriteLine($"CDC Enabled: {(status.IsCdcEnabled ? "✅ Yes" : "❌ No")}");

            if (status.IsCdcEnabled && status.CdcTables.Any())
            {
                Console.WriteLine($"\nCDC Tables ({status.CdcTables.Count()}):");
                Console.WriteLine("┌─────────────────────────────────────────────────────────────────┐");
                Console.WriteLine("│ Schema.Table                    │ Capture Instance    │ Created   │");
                Console.WriteLine("├─────────────────────────────────────────────────────────────────┤");

                foreach (var table in status.CdcTables)
                {
                    var schemaTable = $"{table.SourceSchema}.{table.SourceName}".PadRight(30);
                    var instance = table.CaptureInstance.PadRight(18);
                    var created = table.CreateDate.ToString("yyyy-MM-dd");

                    Console.WriteLine($"│ {schemaTable} │ {instance} │ {created} │");
                }

                Console.WriteLine("└─────────────────────────────────────────────────────────────────┘");
            }
        }
    }

    public class CdcStatus
    {
        public bool IsCdcEnabled { get; set; }
        public IEnumerable<CdcTableInfo> CdcTables { get; set; } = new List<CdcTableInfo>();
        public IEnumerable<CdcJobInfo> CdcJobs { get; set; } = new List<CdcJobInfo>();
        public Dictionary<string, long> TableSizes { get; set; } = new();
    }

    public class CdcTableInfo
    {
        public string CaptureInstance { get; set; }
        public string ObjectName { get; set; }
        public string SourceSchema { get; set; }
        public string SourceName { get; set; }
        public byte[] StartLsn { get; set; }
        public DateTime CreateDate { get; set; }
    }
}
```

### Batch Processing Script

```csharp
// BatchProcessor.cs
public class BatchProcessor
{
    private readonly SimpleDac _dac;
    private readonly ILogger _logger;
    private readonly BatchProcessorOptions _options;

    public BatchProcessor(SimpleDac dac, ILogger logger, BatchProcessorOptions options)
    {
        _dac = dac;
        _logger = logger;
        _options = options;
    }

    public async Task<BatchProcessResult> ProcessBatchAsync(BatchProcessRequest request)
    {
        var result = new BatchProcessResult
        {
            BatchId = Guid.NewGuid().ToString(),
            StartTime = DateTime.UtcNow,
            Request = request
        };

        try
        {
            _logger.LogInformation($"Starting batch process {result.BatchId}");

            // Step 1: Initialize CDC
            if (request.InitializeCdc)
            {
                await InitializeCdcAsync();
                result.Steps.Add("CDC Initialized");
            }

            // Step 2: Execute scenarios
            foreach (var scenario in request.Scenarios)
            {
                await ExecuteScenarioAsync(scenario);
                result.Steps.Add($"Executed scenario: {scenario.Name}");
            }

            // Step 3: Generate profiles
            var profiles = new List<string>();
            foreach (var profileRequest in request.ProfileRequests)
            {
                var profilePath = await GenerateProfileAsync(profileRequest);
                profiles.Add(profilePath);
                result.Steps.Add($"Generated profile: {profilePath}");
            }

            result.GeneratedProfiles = profiles;

            // Step 4: Compare profiles if requested
            if (request.CompareProfiles && profiles.Count >= 2)
            {
                var comparisonPath = await CompareProfilesAsync(profiles[0], profiles[1]);
                result.ComparisonResult = comparisonPath;
                result.Steps.Add($"Generated comparison: {comparisonPath}");
            }

            // Step 5: Cleanup if requested
            if (request.CleanupAfter)
            {
                await CleanupCdcAsync();
                result.Steps.Add("CDC cleaned up");
            }

            result.Success = true;
            result.EndTime = DateTime.UtcNow;

            _logger.LogInformation($"Batch process {result.BatchId} completed successfully");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            result.EndTime = DateTime.UtcNow;

            _logger.LogError(ex, $"Batch process {result.BatchId} failed");
        }

        return result;
    }

    private async Task ExecuteScenarioAsync(ScenarioRequest scenario)
    {
        _logger.LogInformation($"Executing scenario: {scenario.Name}");

        switch (scenario.Type.ToLower())
        {
            case "sql":
                await _dac.ExecuteCommandAsync(scenario.Content);
                break;

            case "script":
                await ExecuteScriptAsync(scenario.Content);
                break;

            case "stored-procedure":
                await ExecuteStoredProcedureAsync(scenario.Content, scenario.Parameters);
                break;

            default:
                throw new NotSupportedException($"Scenario type '{scenario.Type}' is not supported");
        }

        // Wait for CDC capture if specified
        if (scenario.WaitForCdcCapture)
        {
            await Task.Delay(TimeSpan.FromSeconds(_options.CdcCaptureDelaySeconds));
        }
    }
}

public class BatchProcessorOptions
{
    public int CdcCaptureDelaySeconds { get; set; } = 30;
    public string DefaultProfilePath { get; set; } = "./profiles";
    public bool EnableDetailedLogging { get; set; } = true;
}

public class BatchProcessRequest
{
    public bool InitializeCdc { get; set; } = true;
    public List<ScenarioRequest> Scenarios { get; set; } = new();
    public List<ProfileRequest> ProfileRequests { get; set; } = new();
    public bool CompareProfiles { get; set; } = false;
    public bool CleanupAfter { get; set; } = true;
}

public class ScenarioRequest
{
    public string Name { get; set; }
    public string Type { get; set; } // sql, script, stored-procedure
    public string Content { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public bool WaitForCdcCapture { get; set; } = true;
}

public class ProfileRequest
{
    public string Name { get; set; }
    public DateTime? FromTime { get; set; }
    public DateTime? ToTime { get; set; }
    public string OutputPath { get; set; }
}

public class BatchProcessResult
{
    public string BatchId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool Success { get; set; }
    public string Error { get; set; }
    public BatchProcessRequest Request { get; set; }
    public List<string> Steps { get; set; } = new();
    public List<string> GeneratedProfiles { get; set; } = new();
    public string ComparisonResult { get; set; }
}
```

## Web API Examples

### Enhanced Controller with Validation

```csharp
// CdcController.cs
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CdcController : ControllerBase
{
    private readonly SimpleDac _dac;
    private readonly ILogger<CdcController> _logger;
    private readonly CdcSettings _settings;

    public CdcController(SimpleDac dac, ILogger<CdcController> logger, IOptions<CdcSettings> settings)
    {
        _dac = dac;
        _logger = logger;
        _settings = settings.Value;
    }

    /// <summary>
    /// Initialize CDC on the database
    /// </summary>
    /// <returns>Operation result</returns>
    [HttpPost("initialize")]
    [ProducesResponseType(typeof(CdcOperationResult), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    public async Task<ActionResult<CdcOperationResult>> InitializeCdc()
    {
        try
        {
            _logger.LogInformation("Initializing CDC");

            // Check if CDC is already enabled
            var isCdcEnabled = await _dac.ExecuteScalarAsync<bool>(
                "SELECT is_cdc_enabled FROM sys.databases WHERE name = DB_NAME()");

            if (isCdcEnabled)
            {
                return Ok(new CdcOperationResult
                {
                    Success = true,
                    Message = "CDC is already enabled",
                    Data = new { AlreadyEnabled = true }
                });
            }

            // Enable CDC
            CdcDataUtilities.EnableCdcOnDatabase(_dac);

            // Get and enable tables
            var tables = CdcDataUtilities.GetTables(_dac);
            var eligibleTables = tables.Where(t => t.HasPrimaryKey).ToList();

            CdcDataUtilities.EnableTableCdc(_dac, eligibleTables, _logger);

            return Ok(new CdcOperationResult
            {
                Success = true,
                Message = "CDC initialized successfully",
                Data = new
                {
                    TablesEnabled = eligibleTables.Count,
                    TableNames = eligibleTables.Select(t => $"{t.Schema}.{t.Name}").ToArray()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize CDC");
            return StatusCode(500, new ProblemDetails
            {
                Title = "CDC Initialization Failed",
                Detail = ex.Message,
                Status = 500
            });
        }
    }

    /// <summary>
    /// Generate a CDC profile
    /// </summary>
    /// <param name="request">Profile generation request</param>
    /// <returns>Generated profile data</returns>
    [HttpPost("profile")]
    [ProducesResponseType(typeof(ProfileResponse), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    public async Task<ActionResult<ProfileResponse>> GenerateProfile([FromBody] ProfileRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            _logger.LogInformation($"Generating profile: {request.Name}");

            var tables = CdcDataUtilities.GetTables(_dac);
            var profile = await Task.Run(() =>
                CdcDataUtilities.BuildNetProfile(_dac, tables, _logger));

            var response = new ProfileResponse
            {
                Name = request.Name,
                GeneratedAt = DateTime.UtcNow,
                TableCount = profile.Count,
                TotalRecords = profile.Values.Sum(v => v.Count()),
                Data = profile
            };

            // Save to storage if configured
            if (_settings.AutoSaveProfiles)
            {
                var fileName = $"{request.Name}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                var filePath = Path.Combine(_settings.ProfileStoragePath, fileName);
                await System.IO.File.WriteAllTextAsync(filePath, response.ToJson(true));
                response.SavedToFile = filePath;
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to generate profile: {request.Name}");
            return StatusCode(500, new ProblemDetails
            {
                Title = "Profile Generation Failed",
                Detail = ex.Message,
                Status = 500
            });
        }
    }

    /// <summary>
    /// Compare two profiles
    /// </summary>
    /// <param name="request">Comparison request</param>
    /// <returns>Comparison result</returns>
    [HttpPost("compare")]
    [ProducesResponseType(typeof(ComparisonResponse), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    public async Task<ActionResult<ComparisonResponse>> CompareProfiles([FromBody] ComparisonRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            _logger.LogInformation($"Comparing profiles: {request.LeftProfileName} vs {request.RightProfileName}");

            var tables = CdcDataUtilities.GetTables(_dac);
            var differ = new ProfileDiffer();

            var differences = differ.Diff(tables, request.LeftProfile, request.RightProfile);

            var response = new ComparisonResponse
            {
                LeftProfileName = request.LeftProfileName,
                RightProfileName = request.RightProfileName,
                ComparedAt = DateTime.UtcNow,
                TablesCompared = tables.Count(),
                TablesWithDifferences = differences.Count,
                TotalDifferences = differences.Values
                    .Sum(v => ((List<Diff>)((IDictionary<string, object>)v)["diff"]).Count),
                Differences = differences
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compare profiles");
            return StatusCode(500, new ProblemDetails
            {
                Title = "Profile Comparison Failed",
                Detail = ex.Message,
                Status = 500
            });
        }
    }

    /// <summary>
    /// Get CDC status information
    /// </summary>
    /// <returns>CDC status</returns>
    [HttpGet("status")]
    [ProducesResponseType(typeof(CdcStatusResponse), 200)]
    public async Task<ActionResult<Cdc
```
