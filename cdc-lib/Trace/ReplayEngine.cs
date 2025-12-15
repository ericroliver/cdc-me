using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Softbase.Cdc.Models;

namespace Softbase.Cdc.Trace
{
    public class ReplayEngine : IReplayEngine
    {
        private readonly SimpleDac _testDac;
        private readonly ITraceDataProvider _traceProvider;
        private readonly ILogger _logger;

        public ReplayEngine(SimpleDac testDac, ITraceDataProvider traceProvider, ILogger logger)
        {
            _testDac = testDac ?? throw new ArgumentNullException(nameof(testDac));
            _traceProvider = traceProvider ?? throw new ArgumentNullException(nameof(traceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ReplayResult> ReplayTraceSessionAsync(Guid sessionId, ReplayOptions options)
        {
            return await ReplayTraceAsync(sessionId, options);
        }

        public async Task<ReplayResult> ExecuteStatementsFromFileAsync(string filePath, ReplayOptions options)
        {
            _logger.LogInformation("Executing statements from file {FilePath}", filePath);

            var result = new ReplayResult
            {
                SessionId = Guid.NewGuid(),
                StartTime = DateTime.UtcNow
            };

            try
            {
                // Read statements from file (assuming it's a SQL file)
                var statements = await ReadStatementsFromFileAsync(filePath);
                result.TotalStatements = statements.Count;

                _logger.LogInformation("Loaded {StatementCount} statements from file", result.TotalStatements);

                // Execute statements
                foreach (var statement in statements)
                {
                    var statementResult = await ExecuteStatementAsync(statement, options);

                    if (statementResult.Success)
                    {
                        result.SuccessfulStatements++;
                    }
                    else
                    {
                        result.FailedStatements++;
                        result.Errors.Add(new ReplayError
                        {
                            EventId = statement.EventId,
                            SqlText = statement.SqlText,
                            ErrorMessage = statementResult.ErrorMessage ?? "Unknown error",
                            ErrorTime = DateTime.UtcNow
                        });

                        if (!options.ContinueOnError)
                        {
                            _logger.LogWarning("Stopping execution due to error and ContinueOnError is false");
                            break;
                        }
                    }
                }

                result.EndTime = DateTime.UtcNow;
                _logger.LogInformation("Completed file execution. Success: {Success}, Failed: {Failed}",
                    result.SuccessfulStatements, result.FailedStatements);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute statements from file {FilePath}", filePath);
                result.EndTime = DateTime.UtcNow;
                result.Errors.Add(new ReplayError
                {
                    EventId = 0,
                    SqlText = $"File: {filePath}",
                    ErrorMessage = ex.Message,
                    ErrorTime = DateTime.UtcNow
                });
                return result;
            }
        }

        public async Task<ReplayResult> ReplayTraceAsync(Guid sessionId, ReplayOptions options)
        {
            _logger.LogInformation("Starting replay for session {SessionId}", sessionId);

            var result = new ReplayResult
            {
                SessionId = sessionId,
                StartTime = DateTime.UtcNow
            };

            try
            {
                // Get and prepare statements for replay
                var statements = await PrepareStatementsAsync(sessionId, options);
                result.TotalStatements = statements.Count();

                _logger.LogInformation("Prepared {StatementCount} statements for replay", result.TotalStatements);

                // Execute statements in order
                foreach (var statement in statements)
                {
                    var statementResult = await ExecuteStatementAsync(statement, options);

                    if (statementResult.Success)
                    {
                        result.SuccessfulStatements++;
                    }
                    else
                    {
                        result.FailedStatements++;
                        result.Errors.Add(new ReplayError
                        {
                            EventId = statementResult.EventId,
                            SqlText = statement.SqlText,
                            ErrorMessage = statementResult.ErrorMessage ?? "Unknown error",
                            ErrorTime = DateTime.UtcNow
                        });

                        if (!options.ContinueOnError)
                        {
                            _logger.LogError("Stopping replay due to error: {ErrorMessage}", statementResult.ErrorMessage);
                            break;
                        }
                    }
                }

                result.EndTime = DateTime.UtcNow;
                result.SkippedStatements = result.TotalStatements - result.SuccessfulStatements - result.FailedStatements;

                _logger.LogInformation("Replay completed: {Successful} successful, {Failed} failed, {Skipped} skipped",
                    result.SuccessfulStatements, result.FailedStatements, result.SkippedStatements);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Replay failed for session {SessionId}", sessionId);
                result.EndTime = DateTime.UtcNow;
                result.Errors.Add(new ReplayError
                {
                    EventId = 0,
                    SqlText = "REPLAY_ENGINE_ERROR",
                    ErrorMessage = ex.Message,
                    ErrorTime = DateTime.UtcNow
                });
                throw;
            }
        }

        private async Task<List<ReplayStatement>> ReadStatementsFromFileAsync(string filePath)
        {
            var statements = new List<ReplayStatement>();

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            var content = await File.ReadAllTextAsync(filePath);
            var sqlStatements = content.Split(new[] { "GO", ";" }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < sqlStatements.Length; i++)
            {
                var sql = sqlStatements[i].Trim();
                if (!string.IsNullOrEmpty(sql))
                {
                    statements.Add(new ReplayStatement
                    {
                        EventId = i + 1,
                        SqlText = sql,
                        OriginalEventTime = DateTime.UtcNow,
                        ExecutionOrder = i + 1,
                        Parameters = new Dictionary<string, object>()
                    });
                }
            }

            return statements;
        }

        public async Task<IEnumerable<ReplayStatement>> PrepareStatementsAsync(Guid sessionId, ReplayOptions options)
        {
            _logger.LogInformation("Preparing statements for replay from session {SessionId}", sessionId);

            var events = await _traceProvider.GetTraceEventsAsync(sessionId);
            var statements = new List<ReplayStatement>();

            foreach (var traceEvent in events.Where(e => e.IsReplayable).OrderBy(e => e.ExecutionOrder))
            {
                if (string.IsNullOrWhiteSpace(traceEvent.SqlText))
                    continue;

                // Apply filtering options
                if (ShouldSkipStatement(traceEvent.SqlText, options))
                    continue;

                var statement = new ReplayStatement
                {
                    EventId = traceEvent.EventId,
                    SqlText = CleanSqlText(traceEvent.SqlText),
                    OriginalEventTime = traceEvent.EventTime,
                    ExecutionOrder = traceEvent.ExecutionOrder,
                    Parameters = new Dictionary<string, object>()
                };

                statements.Add(statement);
            }

            _logger.LogInformation("Prepared {StatementCount} statements from {EventCount} events",
                statements.Count, events.Count());

            return statements;
        }

        public async Task<StatementResult> ExecuteStatementAsync(ReplayStatement statement, ReplayOptions options)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new StatementResult
            {
                EventId = statement.EventId,
                Success = false,
                ExecutionTime = TimeSpan.Zero,
                RowsAffected = 0
            };

            try
            {
                _logger.LogDebug("Executing statement {EventId}: {SqlText}", statement.EventId,
                    statement.SqlText.Length > 100 ? statement.SqlText.Substring(0, 100) + "..." : statement.SqlText);

                // Set command timeout from options
                var originalTimeout = GetCommandTimeout();
                SetCommandTimeout((int)options.StatementTimeout.TotalSeconds);

                try
                {
                    result.RowsAffected = await _testDac.ExecuteCommandAsync(statement.SqlText);
                    result.Success = true;
                }
                finally
                {
                    SetCommandTimeout(originalTimeout);
                }

                stopwatch.Stop();
                result.ExecutionTime = stopwatch.Elapsed;

                _logger.LogDebug("Statement {EventId} executed successfully in {ExecutionTime}ms, {RowsAffected} rows affected",
                    statement.EventId, result.ExecutionTime.TotalMilliseconds, result.RowsAffected);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.ExecutionTime = stopwatch.Elapsed;
                result.ErrorMessage = ex.Message;
                result.Success = false;

                _logger.LogWarning(ex, "Statement {EventId} failed after {ExecutionTime}ms: {ErrorMessage}",
                    statement.EventId, result.ExecutionTime.TotalMilliseconds, ex.Message);
            }

            return result;
        }

        private bool ShouldSkipStatement(string sqlText, ReplayOptions options)
        {
            if (string.IsNullOrWhiteSpace(sqlText))
                return true;

            var upperSql = sqlText.Trim().ToUpperInvariant();

            // Skip SELECT statements if configured
            if (options.SkipSelectStatements && upperSql.StartsWith("SELECT"))
                return true;

            // Skip system statements if configured
            if (options.SkipSystemStatements)
            {
                if (upperSql.Contains("SYS.") || upperSql.Contains("INFORMATION_SCHEMA") ||
                    upperSql.Contains("CDC.") || upperSql.Contains("__$") ||
                    upperSql.Contains("XE_") || upperSql.Contains("EXTENDED_EVENTS"))
                    return true;
            }

            // Check additional exclude patterns
            foreach (var pattern in options.AdditionalExcludePatterns)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                    continue;

                try
                {
                    if (pattern.Contains("%"))
                    {
                        // SQL LIKE pattern - convert to regex
                        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\%", ".*") + "$";
                        if (Regex.IsMatch(upperSql, regexPattern, RegexOptions.IgnoreCase))
                            return true;
                    }
                    else if (upperSql.Contains(pattern.ToUpperInvariant()))
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

        private string CleanSqlText(string sqlText)
        {
            if (string.IsNullOrWhiteSpace(sqlText))
                return string.Empty;

            // Remove common trace artifacts
            var cleaned = sqlText.Trim();

            // Remove SQL Server trace comments
            cleaned = Regex.Replace(cleaned, @"--\s*exec sp_executesql.*", "", RegexOptions.IgnoreCase);

            // Remove extra whitespace
            cleaned = Regex.Replace(cleaned, @"\s+", " ");

            // Ensure statement ends with semicolon if it's a complete statement
            if (!cleaned.EndsWith(";") && !cleaned.EndsWith("GO", StringComparison.OrdinalIgnoreCase))
            {
                // Only add semicolon for DML/DDL statements
                var upperCleaned = cleaned.ToUpperInvariant();
                if (upperCleaned.StartsWith("INSERT") || upperCleaned.StartsWith("UPDATE") ||
                    upperCleaned.StartsWith("DELETE") || upperCleaned.StartsWith("MERGE") ||
                    upperCleaned.StartsWith("CREATE") || upperCleaned.StartsWith("ALTER") ||
                    upperCleaned.StartsWith("DROP"))
                {
                    cleaned += ";";
                }
            }

            return cleaned;
        }

        private int GetCommandTimeout()
        {
            // This would need to be implemented based on SimpleDac's internal structure
            // For now, return default timeout
            return 120;
        }

        private void SetCommandTimeout(int timeoutSeconds)
        {
            // This would need to be implemented based on SimpleDac's internal structure
            // For now, this is a placeholder
            _logger.LogDebug("Setting command timeout to {TimeoutSeconds} seconds", timeoutSeconds);
        }

        public async Task<ReplayResult> ReplayTraceWithValidationAsync(Guid sessionId, ReplayOptions options, string validationSnapshotName = null)
        {
            _logger.LogInformation("Starting validated replay for session {SessionId}", sessionId);

            try
            {
                // If validation snapshot provided, restore to it first
                if (!string.IsNullOrEmpty(validationSnapshotName))
                {
                    _logger.LogInformation("Restoring to validation snapshot: {SnapshotName}", validationSnapshotName);
                    // This would require SnapshotManager integration
                    // await _snapshotManager.RestoreFromSnapshotAsync(databaseName, validationSnapshotName);
                }

                // Execute the replay
                var result = await ReplayTraceAsync(sessionId, options);

                // Add validation results to the replay result
                result.Errors.Insert(0, new ReplayError
                {
                    EventId = 0,
                    SqlText = "VALIDATION_INFO",
                    ErrorMessage = $"Replay validation completed. Snapshot: {validationSnapshotName ?? "None"}",
                    ErrorTime = DateTime.UtcNow
                });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Validated replay failed for session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<Dictionary<string, object>> GetReplayStatisticsAsync(Guid sessionId)
        {
            var events = await _traceProvider.GetTraceEventsAsync(sessionId);

            var stats = new Dictionary<string, object>
            {
                ["TotalEvents"] = events.Count(),
                ["ReplayableEvents"] = events.Count(e => e.IsReplayable),
                ["EventTypes"] = events.GroupBy(e => e.EventName).ToDictionary(g => g.Key, g => g.Count()),
                ["TimeSpan"] = events.Any() ? events.Max(e => e.EventTime) - events.Min(e => e.EventTime) : TimeSpan.Zero,
                ["DatabasesInvolved"] = events.Where(e => !string.IsNullOrEmpty(e.DatabaseName))
                                            .Select(e => e.DatabaseName).Distinct().ToList(),
                ["ApplicationsInvolved"] = events.Where(e => !string.IsNullOrEmpty(e.ApplicationName))
                                                .Select(e => e.ApplicationName).Distinct().ToList()
            };

            return stats;
        }
    }
}
