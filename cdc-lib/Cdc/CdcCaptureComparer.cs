using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Softbase;

namespace Softbase.Cdc;

/// <summary>
/// Compares CDC captures to validate that refactored procedures produce identical data changes
/// </summary>
public class CdcCaptureComparer
{
    private readonly SimpleDac _traceDac;
    private readonly ILogger _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// Initializes a new instance of the CdcCaptureComparer
    /// </summary>
    /// <param name="traceDac">Database connection to the trace database</param>
    /// <param name="logger">Logger instance</param>
    public CdcCaptureComparer(SimpleDac traceDac, ILogger logger)
    {
        _traceDac = traceDac ?? throw new ArgumentNullException(nameof(traceDac));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Compares two CDC captures and returns detailed comparison results
    /// </summary>
    /// <param name="request">Comparison request parameters</param>
    /// <returns>Detailed comparison results</returns>
    public async Task<CompareCapturesResponse> CompareCapturesAsync(CompareCapturesRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new CompareCapturesResponse();

        try
        {
            _logger.LogInformation("Starting comparison between baseline '{Baseline}' and test '{Test}'",
                request.BaselineCaptureName, request.TestCaptureName);

            // Step 1: Retrieve capture data
            var baselineData = await GetCaptureDataAsync(request.BaselineCaptureName);
            var testData = await GetCaptureDataAsync(request.TestCaptureName);

            if (baselineData == null)
            {
                response.Errors.Add($"Baseline capture '{request.BaselineCaptureName}' not found");
                return response;
            }

            if (testData == null)
            {
                response.Errors.Add($"Test capture '{request.TestCaptureName}' not found");
                return response;
            }

            // Step 2: Perform comparison
            var failures = new List<CaptureComparisonFailure>();
            var summary = new ComparisonSummary();

            // Compare table-level differences
            CompareTableStructure(baselineData, testData, failures);

            // Compare record-level differences for common tables
            var commonTables = baselineData.TableData.Keys.Intersect(testData.TableData.Keys);
            foreach (var tableName in commonTables)
            {
                CompareTableData(tableName, baselineData.TableData[tableName], testData.TableData[tableName],
                    failures, request.FieldsToIgnore, request.IgnoreLsnDifferences);
                summary.TablesCompared++;
            }

            // Update summary statistics
            UpdateSummaryStatistics(summary, baselineData, testData, failures);
            summary.ComparisonDuration = stopwatch.Elapsed;

            response.Failures = failures;
            response.Summary = summary;
            response.IsMatch = failures.Count == 0;

            _logger.LogInformation("Comparison completed: {IsMatch}, {FailureCount} failures, {Duration}ms",
                response.IsMatch, failures.Count, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during capture comparison");
            response.Errors.Add($"Comparison failed: {ex.Message}");
            return response;
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    /// <summary>
    /// Retrieves capture data for a specific capture name
    /// </summary>
    private async Task<CaptureData?> GetCaptureDataAsync(string captureName)
    {
        const string sql = @"
            SELECT cc.table_name, cc.capture_data, cc.record_count
            FROM cdc_captures cc
            INNER JOIN cdc_capture_headers cch ON cc.capture_header_id = cch.capture_header_id
            WHERE cch.capture_name = @captureName";

        try
        {
            var parameters = new Dictionary<string, object>
            {
                ["captureName"] = captureName
            };

            var captureData = new CaptureData { CaptureName = captureName };

            await _traceDac.ExecuteReaderAsync(sql, reader =>
            {
                while (reader.Read())
                {
                    var tableName = reader.GetString(reader.GetOrdinal("table_name"));
                    var captureDataJson = reader.GetString(reader.GetOrdinal("capture_data"));

                    try
                    {
                        var tableRecords = DeserializeCaptureData(captureDataJson);
                        if (tableRecords != null)
                        {
                            captureData.TableData[tableName] = tableRecords;
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize capture data for table {TableName}", tableName);
                    }
                }
                return Task.CompletedTask;
            }, parameters);

            return captureData.TableData.Any() ? captureData : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve capture data for {CaptureName}", captureName);
            return null;
        }
    }

    /// <summary>
    /// Deserializes capture data JSON with trimming support
    /// </summary>
    /// <param name="json">JSON string to deserialize</param>
    /// <returns>Deserialized list of dictionaries</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "Dictionary<string, object> is used for dynamic CDC data that cannot be statically analyzed. The types are preserved at runtime.")]
    private static List<Dictionary<string, object>>? DeserializeCaptureData(string json)
    {
        return JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json, JsonOptions);
    }

    /// <summary>
    /// Compares table structure between two captures
    /// </summary>
    private void CompareTableStructure(CaptureData baseline, CaptureData test, List<CaptureComparisonFailure> failures)
    {
        // Check for missing tables in test capture
        var missingTables = baseline.TableData.Keys.Except(test.TableData.Keys);
        foreach (var tableName in missingTables)
        {
            failures.Add(new CaptureComparisonFailure
            {
                TableName = tableName,
                FailureType = FailureTypes.MissingTable,
                Description = $"Table '{tableName}' exists in baseline but not in test capture"
            });
        }

        // Check for extra tables in test capture
        var extraTables = test.TableData.Keys.Except(baseline.TableData.Keys);
        foreach (var tableName in extraTables)
        {
            failures.Add(new CaptureComparisonFailure
            {
                TableName = tableName,
                FailureType = FailureTypes.ExtraTable,
                Description = $"Table '{tableName}' exists in test capture but not in baseline"
            });
        }
    }

    /// <summary>
    /// Compares data within a specific table between two captures
    /// </summary>
    private void CompareTableData(string tableName, List<Dictionary<string, object>> baselineRecords,
        List<Dictionary<string, object>> testRecords, List<CaptureComparisonFailure> failures,
        List<string>? fieldsToIgnore, bool ignoreLsnDifferences)
    {
        // Create indexes by primary key for efficient lookup
        var baselineIndex = CreatePrimaryKeyIndex(baselineRecords);
        var testIndex = CreatePrimaryKeyIndex(testRecords);

        // Check record count differences
        if (baselineRecords.Count != testRecords.Count)
        {
            failures.Add(new CaptureComparisonFailure
            {
                TableName = tableName,
                FailureType = FailureTypes.RecordCountMismatch,
                BaselineValue = baselineRecords.Count,
                TestValue = testRecords.Count,
                Description = $"Record count mismatch in table '{tableName}': baseline has {baselineRecords.Count}, test has {testRecords.Count}"
            });
        }

        // Compare records by primary key
        foreach (var kvp in baselineIndex)
        {
            var primaryKey = kvp.Key;
            var baselineRecord = kvp.Value;

            if (testIndex.TryGetValue(primaryKey, out var testRecord))
            {
                // Compare individual fields
                CompareRecordFields(tableName, primaryKey, baselineRecord, testRecord, failures, fieldsToIgnore, ignoreLsnDifferences);
            }
            else
            {
                failures.Add(new CaptureComparisonFailure
                {
                    TableName = tableName,
                    FailureType = FailureTypes.MissingRecord,
                    PrimaryKey = primaryKey,
                    Description = $"Record with primary key '{primaryKey}' exists in baseline but not in test capture for table '{tableName}'"
                });
            }
        }

        // Check for extra records in test capture
        foreach (var kvp in testIndex)
        {
            var primaryKey = kvp.Key;
            if (!baselineIndex.ContainsKey(primaryKey))
            {
                failures.Add(new CaptureComparisonFailure
                {
                    TableName = tableName,
                    FailureType = FailureTypes.ExtraRecord,
                    PrimaryKey = primaryKey,
                    Description = $"Record with primary key '{primaryKey}' exists in test capture but not in baseline for table '{tableName}'"
                });
            }
        }
    }

    /// <summary>
    /// Creates an index of records by their primary key
    /// </summary>
    private Dictionary<string, Dictionary<string, object>> CreatePrimaryKeyIndex(List<Dictionary<string, object>> records)
    {
        var index = new Dictionary<string, Dictionary<string, object>>();

        foreach (var record in records)
        {
            var primaryKey = GetPrimaryKeyValue(record);
            index[primaryKey] = record;
        }

        return index;
    }

    /// <summary>
    /// Extracts the primary key value from a CDC record
    /// </summary>
    private string GetPrimaryKeyValue(Dictionary<string, object> record)
    {
        if (record.TryGetValue("__$primary_key", out var pkValue))
        {
            return pkValue?.ToString() ?? "null";
        }

        // Fallback: try to construct from available data
        return "unknown";
    }

    /// <summary>
    /// Compares individual fields between two records
    /// </summary>
    private void CompareRecordFields(string tableName, string primaryKey, Dictionary<string, object> baselineRecord,
        Dictionary<string, object> testRecord, List<CaptureComparisonFailure> failures,
        List<string>? fieldsToIgnore, bool ignoreLsnDifferences)
    {
        var fieldsToIgnoreSet = new HashSet<string>(fieldsToIgnore ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

        // Add standard CDC fields to ignore if specified
        if (ignoreLsnDifferences)
        {
            fieldsToIgnoreSet.Add("__$start_lsn");
        }

        // Always ignore these CDC metadata fields for value comparison
        var metadataFields = new[] { "__$operation", "__$table", "__$primary_key" };
        foreach (var field in metadataFields)
        {
            fieldsToIgnoreSet.Add(field);
        }

        // Compare operation types
        if (baselineRecord.TryGetValue("__$operation", out var baselineOp) &&
            testRecord.TryGetValue("__$operation", out var testOp))
        {
            if (!AreValuesEqual(baselineOp, testOp))
            {
                failures.Add(new CaptureComparisonFailure
                {
                    TableName = tableName,
                    FailureType = FailureTypes.OperationMismatch,
                    PrimaryKey = primaryKey,
                    FieldName = "__$operation",
                    BaselineValue = baselineOp,
                    TestValue = testOp,
                    Description = $"Operation type mismatch for record '{primaryKey}' in table '{tableName}'"
                });
            }
        }

        // Compare all other fields
        var allFields = baselineRecord.Keys.Union(testRecord.Keys);
        foreach (var fieldName in allFields)
        {
            // Check if field should be ignored (exact match or without old_/new_ prefix)
            if (ShouldIgnoreField(fieldName, fieldsToIgnoreSet))
                continue;

            var baselineValue = baselineRecord.TryGetValue(fieldName, out var bVal) ? bVal : null;
            var testValue = testRecord.TryGetValue(fieldName, out var tVal) ? tVal : null;

            if (!AreValuesEqual(baselineValue, testValue))
            {
                failures.Add(new CaptureComparisonFailure
                {
                    TableName = tableName,
                    FailureType = FailureTypes.FieldMismatch,
                    PrimaryKey = primaryKey,
                    FieldName = fieldName,
                    BaselineValue = baselineValue,
                    TestValue = testValue,
                    Description = $"Field '{fieldName}' mismatch for record '{primaryKey}' in table '{tableName}'"
                });
            }
        }
    }

    /// <summary>
    /// Checks if a field should be ignored based on the ignore list, including checking with prefixes removed
    /// </summary>
    private bool ShouldIgnoreField(string fieldName, HashSet<string> fieldsToIgnoreSet)
    {
        // Check exact match first
        if (fieldsToIgnoreSet.Contains(fieldName))
            return true;

        // Check if field has old_ or new_ prefix and the base name matches
        if (fieldName.StartsWith("old_", StringComparison.OrdinalIgnoreCase))
        {
            var baseFieldName = fieldName.Substring(4);
            if (fieldsToIgnoreSet.Contains(baseFieldName))
                return true;
        }
        else if (fieldName.StartsWith("new_", StringComparison.OrdinalIgnoreCase))
        {
            var baseFieldName = fieldName.Substring(4);
            if (fieldsToIgnoreSet.Contains(baseFieldName))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Compares two values for equality, handling nulls and DBNull appropriately
    /// </summary>
    private bool AreValuesEqual(object? value1, object? value2)
    {
        if (value1 == null && value2 == null)
            return true;
        if (value1 == null || value2 == null)
            return false;
        if (value1 == DBNull.Value && value2 == DBNull.Value)
            return true;
        if (value1 == DBNull.Value || value2 == DBNull.Value)
            return false;

        // Handle JsonElement comparison (from deserialized JSON)
        if (value1 is JsonElement je1 && value2 is JsonElement je2)
        {
            return JsonElementsEqual(je1, je2);
        }

        return value1.Equals(value2);
    }

    /// <summary>
    /// Compares two JsonElement values for equality
    /// </summary>
    private bool JsonElementsEqual(JsonElement element1, JsonElement element2)
    {
        if (element1.ValueKind != element2.ValueKind)
            return false;

        return element1.ValueKind switch
        {
            JsonValueKind.String => element1.GetString() == element2.GetString(),
            JsonValueKind.Number => element1.GetDecimal() == element2.GetDecimal(),
            JsonValueKind.True or JsonValueKind.False => element1.GetBoolean() == element2.GetBoolean(),
            JsonValueKind.Null => true,
            _ => element1.GetRawText() == element2.GetRawText()
        };
    }

    /// <summary>
    /// Updates summary statistics based on comparison results
    /// </summary>
    private void UpdateSummaryStatistics(ComparisonSummary summary, CaptureData baseline, CaptureData test,
        List<CaptureComparisonFailure> failures)
    {
        summary.TotalFailures = failures.Count;
        summary.TablesWithFailures = failures.Select(f => f.TableName).Distinct().Count();

        // Count total records and fields compared
        var commonTables = baseline.TableData.Keys.Intersect(test.TableData.Keys);
        foreach (var tableName in commonTables)
        {
            var baselineRecords = baseline.TableData[tableName];
            var testRecords = test.TableData[tableName];

            summary.RecordsCompared += Math.Max(baselineRecords.Count, testRecords.Count);

            // Estimate fields compared (approximate)
            if (baselineRecords.Any())
            {
                summary.FieldsCompared += baselineRecords.Count * baselineRecords.First().Keys.Count;
            }
        }
    }
}
