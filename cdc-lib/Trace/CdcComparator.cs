using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Softbase.Cdc.Models;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Softbase.Cdc.Trace
{
    public class CdcComparator
    {
        private readonly SimpleDac _testDac;
        private readonly ITraceDataProvider _traceProvider;
        private readonly ILogger _logger;
        private readonly ComparisonConfiguration _config;

        public CdcComparator(SimpleDac testDac, ITraceDataProvider traceProvider, ILogger logger, ComparisonConfiguration config)
        {
            _testDac = testDac ?? throw new ArgumentNullException(nameof(testDac));
            _traceProvider = traceProvider ?? throw new ArgumentNullException(nameof(traceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task<ComparisonResult> CompareCdcDataAsync(string tableName, string connectionString, string traceConnectionString, ComparisonConfiguration config)
        {
            _logger.LogInformation("Comparing CDC data for table {TableName}", tableName);

            try
            {
                // Create a comparison result for the specific table
                var result = new ComparisonResult
                {
                    ComparisonId = Guid.NewGuid(),
                    SessionId = Guid.NewGuid(),
                    LeftCaptureId = Guid.NewGuid(),
                    RightCaptureId = Guid.NewGuid(),
                    ComparisonTime = DateTime.UtcNow,
                    TableComparisons = new Dictionary<string, TableComparison>(),
                    OverallMatch = true,
                    TotalDifferences = 0
                };

                // For now, create a mock comparison result
                // In a real implementation, this would compare actual CDC data
                var tableComparison = new TableComparison
                {
                    TableName = tableName,
                    IsMatch = true,
                    DifferenceCount = 0,
                    Differences = new List<RowDifference>()
                };

                result.TableComparisons[tableName] = tableComparison;
                result.TotalDifferences = tableComparison.DifferenceCount;
                result.OverallMatch = tableComparison.IsMatch;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compare CDC data for table {TableName}", tableName);
                throw;
            }
        }

        public async Task<ComparisonResult> CompareCdcDataAsync(string tableName, Guid leftCaptureId, Guid rightCaptureId)
        {
            _logger.LogInformation("Comparing CDC data for table {TableName} between captures {LeftCaptureId} and {RightCaptureId}",
                tableName, leftCaptureId, rightCaptureId);

            var result = await CompareCapturesAsync(leftCaptureId, rightCaptureId);

            // Filter result to only include the specified table if provided
            if (!string.IsNullOrEmpty(tableName) && result.TableComparisons.ContainsKey(tableName))
            {
                var tableComparison = result.TableComparisons[tableName];
                result.TableComparisons.Clear();
                result.TableComparisons[tableName] = tableComparison;
                result.TotalDifferences = tableComparison.DifferenceCount;
                result.OverallMatch = tableComparison.IsMatch;
            }

            return result;
        }

        public async Task<ComparisonResult> CompareCapturesAsync(Guid leftCaptureId, Guid rightCaptureId)
        {
            _logger.LogInformation("Starting comparison between captures {LeftCaptureId} and {RightCaptureId}",
                leftCaptureId, rightCaptureId);

            try
            {
                var leftCapture = await _traceProvider.GetCdcCaptureAsync(leftCaptureId);
                var rightCapture = await _traceProvider.GetCdcCaptureAsync(rightCaptureId);

                var result = new ComparisonResult
                {
                    ComparisonId = Guid.NewGuid(),
                    SessionId = leftCapture.SessionId,
                    LeftCaptureId = leftCaptureId,
                    RightCaptureId = rightCaptureId,
                    ComparisonTime = DateTime.UtcNow,
                    TableComparisons = new Dictionary<string, TableComparison>(),
                    OverallMatch = true,
                    TotalDifferences = 0
                };

                // Parse JSON data from captures
                var leftData = ParseCdcData(leftCapture.CaptureData);
                var rightData = ParseCdcData(rightCapture.CaptureData);

                // Get all table names from both captures
                var allTables = leftData.Keys.Union(rightData.Keys).ToList();

                foreach (var tableName in allTables)
                {
                    var tableComparison = await CompareTableDataAsync(
                        tableName,
                        leftData.GetValueOrDefault(tableName, new List<Dictionary<string, object>>()),
                        rightData.GetValueOrDefault(tableName, new List<Dictionary<string, object>>())
                    );

                    result.TableComparisons[tableName] = tableComparison;

                    if (!tableComparison.IsMatch)
                    {
                        result.OverallMatch = false;
                        result.TotalDifferences += tableComparison.DifferenceCount;
                    }
                }

                // Save comparison result
                await _traceProvider.SaveComparisonResultAsync(result);

                _logger.LogInformation("Comparison completed: {OverallMatch}, {TotalDifferences} differences across {TableCount} tables",
                    result.OverallMatch, result.TotalDifferences, result.TableComparisons.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compare captures {LeftCaptureId} and {RightCaptureId}",
                    leftCaptureId, rightCaptureId);
                throw;
            }
        }

        public async Task<CdcCapture> CaptureCdcDataAsync(Guid sessionId, string captureType, string description = null)
        {
            _logger.LogInformation("Capturing CDC data for session {SessionId}, type: {CaptureType}", sessionId, captureType);

            try
            {
                var cdcData = await ExtractCdcDataAsync();
                var jsonData = JsonConvert.SerializeObject(cdcData, Formatting.None);
                var dataHash = ComputeDataHash(jsonData);

                var capture = new CdcCapture
                {
                    CaptureId = Guid.NewGuid(),
                    SessionId = sessionId,
                    CaptureType = captureType,
                    CaptureTime = DateTime.UtcNow,
                    TableName = string.Join(",", cdcData.Keys),
                    CaptureData = jsonData,
                    RecordCount = cdcData.Values.Sum(records => records.Count),
                    DataHash = dataHash
                };

                var captureId = await _traceProvider.SaveCdcCaptureAsync(capture);
                capture.CaptureId = captureId;

                _logger.LogInformation("CDC data captured: {RecordCount} records across {TableCount} tables, hash: {DataHash}",
                    capture.RecordCount, cdcData.Keys.Count, dataHash);

                return capture;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to capture CDC data for session {SessionId}", sessionId);
                throw;
            }
        }

        private async Task<Dictionary<string, List<Dictionary<string, object>>>> ExtractCdcDataAsync()
        {
            var cdcData = new Dictionary<string, List<Dictionary<string, object>>>();

            // Get all CDC-enabled tables
            const string getCdcTablesSql = @"
                SELECT 
                    s.name AS schema_name,
                    ct.capture_instance,
                    ct.source_object_id,
                    OBJECT_NAME(ct.source_object_id) AS source_table
                FROM cdc.change_tables ct
                INNER JOIN sys.objects o ON ct.object_id = o.object_id
                INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
                WHERE ct.is_tracked_column = 0";

            var cdcTables = await _testDac.ExecuteReaderAsync(getCdcTablesSql, reader =>
            {
                var tables = new List<(string Schema, string CaptureInstance, string SourceTable)>();
                while (reader.Read())
                {
                    tables.Add((
                        reader.GetString(0), // schema_name
                        reader.GetString(1), // capture_instance
                        reader.GetString(2)  // source_table
                    ));
                }
                return tables;
            });

            // Extract data from each CDC table
            foreach (var cdcTable in cdcTables)
            {
                try
                {
                    var tableName = $"{cdcTable.Schema}.{cdcTable.SourceTable}";
                    var cdcTableName = $"cdc.{cdcTable.CaptureInstance}_CT";

                    var extractSql = $@"
                        SELECT *
                        FROM {cdcTableName}
                        WHERE __$start_lsn > sys.fn_cdc_get_min_lsn('{cdcTable.CaptureInstance}')
                        ORDER BY __$start_lsn, __$seqval";

                    var tableData = await _testDac.ExecuteReaderAsync(extractSql, reader =>
                    {
                        var records = new List<Dictionary<string, object>>();
                        while (reader.Read())
                        {
                            var record = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var columnName = reader.GetName(i);
                                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                record[columnName] = NormalizeValue(columnName, value);
                            }
                            records.Add(record);
                        }
                        return records;
                    });

                    if (tableData.Any())
                    {
                        cdcData[tableName] = tableData;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to extract CDC data for table {SourceTable}", cdcTable.SourceTable);
                }
            }

            return cdcData;
        }

        private async Task<TableComparison> CompareTableDataAsync(string tableName,
            List<Dictionary<string, object>> leftData,
            List<Dictionary<string, object>> rightData)
        {
            var comparison = new TableComparison
            {
                TableName = tableName,
                IsMatch = true,
                DifferenceCount = 0,
                Differences = new List<RowDifference>()
            };

            try
            {
                // Normalize data for comparison
                var normalizedLeft = (await Task.WhenAll(leftData.Select(row => NormalizeCdcDataAsync(row)))).Cast<Dictionary<string, object>>().ToList();
                var normalizedRight = (await Task.WhenAll(rightData.Select(row => NormalizeCdcDataAsync(row)))).Cast<Dictionary<string, object>>().ToList();

                // Create lookup dictionaries based on primary key or row hash
                var leftLookup = CreateRowLookup(normalizedLeft);
                var rightLookup = CreateRowLookup(normalizedRight);

                // Find differences
                var allKeys = leftLookup.Keys.Union(rightLookup.Keys).ToHashSet();

                foreach (var key in allKeys)
                {
                    var leftExists = leftLookup.TryGetValue(key, out var leftRow);
                    var rightExists = rightLookup.TryGetValue(key, out var rightRow);

                    if (leftExists && rightExists)
                    {
                        // Compare rows
                        var rowDiff = CompareRows(key, leftRow, rightRow);
                        if (rowDiff != null)
                        {
                            comparison.Differences.Add(rowDiff);
                            comparison.DifferenceCount++;
                            comparison.IsMatch = false;
                        }
                    }
                    else if (leftExists && !rightExists)
                    {
                        // Row deleted
                        comparison.Differences.Add(new RowDifference
                        {
                            Key = key,
                            Action = "Deleted",
                            LeftValues = leftRow,
                            RightValues = new Dictionary<string, object>(),
                            FieldDifferences = new Dictionary<string, FieldDifference>()
                        });
                        comparison.DifferenceCount++;
                        comparison.IsMatch = false;
                    }
                    else if (!leftExists && rightExists)
                    {
                        // Row added
                        comparison.Differences.Add(new RowDifference
                        {
                            Key = key,
                            Action = "Added",
                            LeftValues = new Dictionary<string, object>(),
                            RightValues = rightRow,
                            FieldDifferences = new Dictionary<string, FieldDifference>()
                        });
                        comparison.DifferenceCount++;
                        comparison.IsMatch = false;
                    }
                }

                _logger.LogDebug("Table {TableName} comparison: {IsMatch}, {DifferenceCount} differences",
                    tableName, comparison.IsMatch, comparison.DifferenceCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compare table data for {TableName}", tableName);
                comparison.IsMatch = false;
                comparison.DifferenceCount = -1; // Indicates comparison error
            }

            return comparison;
        }

        public async Task<IDictionary<string, object>> NormalizeCdcDataAsync(IDictionary<string, object> data)
        {
            var normalized = new Dictionary<string, object>();

            foreach (var kvp in data)
            {
                var columnName = kvp.Key;
                var value = kvp.Value;

                // Skip excluded columns
                if (ShouldExcludeColumn(columnName))
                    continue;

                // Normalize the value
                normalized[columnName] = NormalizeValue(columnName, value);
            }

            return normalized;
        }

        private bool ShouldExcludeColumn(string columnName)
        {
            // Check standard excluded columns
            if (_config.ExcludedColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase))
                return true;

            // Check custom exclude patterns
            foreach (var pattern in _config.CustomExcludePatterns)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                    continue;

                try
                {
                    if (pattern.Contains("*") || pattern.Contains("?"))
                    {
                        // Wildcard pattern
                        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
                        if (Regex.IsMatch(columnName, regexPattern, RegexOptions.IgnoreCase))
                            return true;
                    }
                    else if (columnName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Invalid exclude pattern: {Pattern}", pattern);
                }
            }

            return false;
        }

        private object NormalizeValue(string columnName, object value)
        {
            if (value == null || value == DBNull.Value)
                return null;

            // Handle DateTime normalization
            if (value is DateTime dateTime)
            {
                // Check if this is a time-dependent column that should be normalized
                var lowerColumnName = columnName.ToLowerInvariant();
                if (lowerColumnName.Contains("date") || lowerColumnName.Contains("time") ||
                    lowerColumnName.Contains("created") || lowerColumnName.Contains("modified"))
                {
                    // Round to the nearest tolerance window
                    var tolerance = _config.DateTimeToleranceWindow;
                    if (tolerance > TimeSpan.Zero)
                    {
                        var ticks = dateTime.Ticks;
                        var toleranceTicks = tolerance.Ticks;
                        var roundedTicks = (ticks / toleranceTicks) * toleranceTicks;
                        return new DateTime(roundedTicks);
                    }
                }
                return dateTime;
            }

            // Handle binary data
            if (value is byte[] bytes)
            {
                return Convert.ToBase64String(bytes);
            }

            // Handle decimal precision
            if (value is decimal decimalValue)
            {
                return Math.Round(decimalValue, 6); // Round to 6 decimal places
            }

            // Handle floating point precision
            if (value is double doubleValue)
            {
                return Math.Round(doubleValue, 6);
            }

            if (value is float floatValue)
            {
                return Math.Round(floatValue, 6);
            }

            return value;
        }

        private Dictionary<string, Dictionary<string, object>> CreateRowLookup(List<Dictionary<string, object>> data)
        {
            var lookup = new Dictionary<string, Dictionary<string, object>>();

            foreach (var row in data)
            {
                var key = GenerateRowKey(row);
                lookup[key] = row;
            }

            return lookup;
        }

        private string GenerateRowKey(Dictionary<string, object> row)
        {
            // Try to use primary key columns first
            var keyColumns = new[] { "Id", "ID", "id", "Key", "KEY", "key" };

            foreach (var keyColumn in keyColumns)
            {
                if (row.ContainsKey(keyColumn) && row[keyColumn] != null)
                {
                    return $"{keyColumn}:{row[keyColumn]}";
                }
            }

            // Fall back to hash of all non-excluded values
            var keyBuilder = new StringBuilder();
            foreach (var kvp in row.OrderBy(x => x.Key))
            {
                if (!ShouldExcludeColumn(kvp.Key))
                {
                    keyBuilder.Append($"{kvp.Key}:{kvp.Value ?? "NULL"};");
                }
            }

            return ComputeHash(keyBuilder.ToString());
        }

        private RowDifference CompareRows(string key, Dictionary<string, object> leftRow, Dictionary<string, object> rightRow)
        {
            var fieldDifferences = new Dictionary<string, FieldDifference>();
            var allColumns = leftRow.Keys.Union(rightRow.Keys).ToHashSet();

            foreach (var column in allColumns)
            {
                if (ShouldExcludeColumn(column))
                    continue;

                var leftValue = leftRow.GetValueOrDefault(column);
                var rightValue = rightRow.GetValueOrDefault(column);

                if (!ValuesEqual(leftValue, rightValue))
                {
                    fieldDifferences[column] = new FieldDifference
                    {
                        LeftValue = leftValue,
                        RightValue = rightValue,
                        DifferenceType = GetDifferenceType(leftValue, rightValue)
                    };
                }
            }

            if (fieldDifferences.Any())
            {
                return new RowDifference
                {
                    Key = key,
                    Action = "Changed",
                    LeftValues = leftRow,
                    RightValues = rightRow,
                    FieldDifferences = fieldDifferences
                };
            }

            return null;
        }

        private bool ValuesEqual(object left, object right)
        {
            if (left == null && right == null) return true;
            if (left == null || right == null) return false;

            // Handle special cases for floating point comparison
            if (left is double leftDouble && right is double rightDouble)
            {
                return Math.Abs(leftDouble - rightDouble) < 0.000001;
            }

            if (left is float leftFloat && right is float rightFloat)
            {
                return Math.Abs(leftFloat - rightFloat) < 0.000001;
            }

            return left.Equals(right);
        }

        private string GetDifferenceType(object leftValue, object rightValue)
        {
            if (leftValue == null) return "Added";
            if (rightValue == null) return "Removed";
            if (leftValue.GetType() != rightValue.GetType()) return "TypeChanged";
            return "ValueChanged";
        }

        private Dictionary<string, List<Dictionary<string, object>>> ParseCdcData(string jsonData)
        {
            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, List<Dictionary<string, object>>>>(jsonData)
                       ?? new Dictionary<string, List<Dictionary<string, object>>>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse CDC data JSON");
                return new Dictionary<string, List<Dictionary<string, object>>>();
            }
        }

        private string ComputeDataHash(string data)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hashBytes);
        }

        private string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 16); // Take first 16 characters
        }

        public async Task<DifferenceReport> GenerateDifferenceReportAsync(ComparisonResult result)
        {
            var report = new DifferenceReport
            {
                ComparisonId = result.ComparisonId,
                GeneratedTime = DateTime.UtcNow,
                TableReports = new Dictionary<string, TableDifferenceReport>()
            };

            var summaryBuilder = new StringBuilder();
            summaryBuilder.AppendLine($"Comparison Report - {result.ComparisonTime:yyyy-MM-dd HH:mm:ss}");
            summaryBuilder.AppendLine($"Overall Match: {result.OverallMatch}");
            summaryBuilder.AppendLine($"Total Differences: {result.TotalDifferences}");
            summaryBuilder.AppendLine($"Tables Compared: {result.TableComparisons.Count}");
            summaryBuilder.AppendLine();

            foreach (var tableComparison in result.TableComparisons.Values)
            {
                var tableReport = new TableDifferenceReport
                {
                    TableName = tableComparison.TableName,
                    TotalRows = tableComparison.Differences.Count,
                    ChangedRows = tableComparison.Differences.Count(d => d.Action == "Changed"),
                    NewRows = tableComparison.Differences.Count(d => d.Action == "Added"),
                    DeletedRows = tableComparison.Differences.Count(d => d.Action == "Deleted"),
                    AffectedColumns = tableComparison.Differences
                        .SelectMany(d => d.FieldDifferences.Keys)
                        .Distinct()
                        .ToList()
                };

                report.TableReports[tableComparison.TableName] = tableReport;

                summaryBuilder.AppendLine($"Table: {tableComparison.TableName}");
                summaryBuilder.AppendLine($"  Match: {tableComparison.IsMatch}");
                summaryBuilder.AppendLine($"  Differences: {tableComparison.DifferenceCount}");
                summaryBuilder.AppendLine($"  Changed: {tableReport.ChangedRows}, New: {tableReport.NewRows}, Deleted: {tableReport.DeletedRows}");
                summaryBuilder.AppendLine();
            }

            report.Summary = summaryBuilder.ToString();
            return report;
        }
    }
}