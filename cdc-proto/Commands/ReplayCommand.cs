using System.CommandLine;
using Microsoft.Extensions.Logging;
using Softbase;
using Softbase.Cdc.Data;
using Softbase.Cdc.Trace;
using Softbase.Cdc.Models;

namespace CdcProto.Commands
{
    public static class ReplayCommand
    {
        public static Command CreateCommand()
        {
            var replayCommand = new Command("replay", "Replay captured SQL traces and compare CDC data");

            // Execute replay command
            var executeCommand = new Command("execute", "Execute a trace replay");
            var sessionOption = new Option<string>("--session", "Session name or ID to replay") { IsRequired = true };
            var testConnectionOption = new Option<string>("--test-connection", "Test database connection string");
            var traceConnectionOption = new Option<string>("--trace-connection", "Trace database connection string");
            var providerOption = new Option<string>("--provider", () => "PostgreSQL", "Trace database provider (PostgreSQL|SqlServer)");
            var skipSelectsOption = new Option<bool>("--skip-selects", () => true, "Skip SELECT statements during replay");
            var continueOnErrorOption = new Option<bool>("--continue-on-error", () => false, "Continue replay on statement errors");
            var timeoutOption = new Option<int>("--timeout", () => 30, "Statement timeout in seconds");

            executeCommand.AddOption(sessionOption);
            executeCommand.AddOption(testConnectionOption);
            executeCommand.AddOption(traceConnectionOption);
            executeCommand.AddOption(providerOption);
            executeCommand.AddOption(skipSelectsOption);
            executeCommand.AddOption(continueOnErrorOption);
            executeCommand.AddOption(timeoutOption);
            executeCommand.SetHandler(ExecuteReplayAsync, sessionOption, testConnectionOption, traceConnectionOption,
                providerOption, skipSelectsOption, continueOnErrorOption, timeoutOption);

            // Capture CDC command
            var captureCommand = new Command("capture", "Capture CDC data for comparison");
            var captureSessionOption = new Option<string>("--session", "Session ID") { IsRequired = true };
            var captureTypeOption = new Option<string>("--type", "Capture type (Baseline|Replay|Optimized)") { IsRequired = true };
            var captureTestConnectionOption = new Option<string>("--test-connection", "Test database connection string");
            var captureTraceConnectionOption = new Option<string>("--trace-connection", "Trace database connection string");
            var captureProviderOption = new Option<string>("--provider", () => "PostgreSQL", "Trace database provider");

            captureCommand.AddOption(captureSessionOption);
            captureCommand.AddOption(captureTypeOption);
            captureCommand.AddOption(captureTestConnectionOption);
            captureCommand.AddOption(captureTraceConnectionOption);
            captureCommand.AddOption(captureProviderOption);
            captureCommand.SetHandler(CaptureCdcDataAsync, captureSessionOption, captureTypeOption,
                captureTestConnectionOption, captureTraceConnectionOption, captureProviderOption);

            // Compare CDC command
            var compareCommand = new Command("compare", "Compare two CDC captures");
            var leftCaptureOption = new Option<string>("--left", "Left capture ID") { IsRequired = true };
            var rightCaptureOption = new Option<string>("--right", "Right capture ID") { IsRequired = true };
            var compareTraceConnectionOption = new Option<string>("--trace-connection", "Trace database connection string");
            var compareProviderOption = new Option<string>("--provider", () => "PostgreSQL", "Trace database provider");
            var outputOption = new Option<string>("--output", "Output file for comparison report");

            compareCommand.AddOption(leftCaptureOption);
            compareCommand.AddOption(rightCaptureOption);
            compareCommand.AddOption(compareTraceConnectionOption);
            compareCommand.AddOption(compareProviderOption);
            compareCommand.AddOption(outputOption);
            compareCommand.SetHandler(CompareCdcCapturesAsync, leftCaptureOption, rightCaptureOption,
                compareTraceConnectionOption, compareProviderOption, outputOption);

            // Workflow command (complete test workflow)
            var workflowCommand = new Command("workflow", "Execute complete test workflow");
            var workflowDatabaseOption = new Option<string>("--database", "Database name") { IsRequired = true };
            var workflowSessionOption = new Option<string>("--session", "Session name") { IsRequired = true };
            var workflowSnapshotOption = new Option<string>("--snapshot", "Snapshot name") { IsRequired = true };
            var workflowTestConnectionOption = new Option<string>("--test-connection", "Test database connection string");
            var workflowTraceConnectionOption = new Option<string>("--trace-connection", "Trace database connection string");
            var workflowProviderOption = new Option<string>("--provider", () => "PostgreSQL", "Trace database provider");

            workflowCommand.AddOption(workflowDatabaseOption);
            workflowCommand.AddOption(workflowSessionOption);
            workflowCommand.AddOption(workflowSnapshotOption);
            workflowCommand.AddOption(workflowTestConnectionOption);
            workflowCommand.AddOption(workflowTraceConnectionOption);
            workflowCommand.AddOption(workflowProviderOption);
            workflowCommand.SetHandler(ExecuteWorkflowAsync, workflowDatabaseOption, workflowSessionOption,
                workflowSnapshotOption, workflowTestConnectionOption, workflowTraceConnectionOption, workflowProviderOption);

            replayCommand.AddCommand(executeCommand);
            replayCommand.AddCommand(captureCommand);
            replayCommand.AddCommand(compareCommand);
            replayCommand.AddCommand(workflowCommand);

            return replayCommand;
        }

        private static async Task ExecuteReplayAsync(string session, string testConnection, string traceConnection,
            string provider, bool skipSelects, bool continueOnError, int timeout)
        {
            var logger = CreateLogger();

            try
            {
                var testConnectionString = GetTestConnectionString(testConnection);
                var traceConnectionString = GetTraceConnectionString(traceConnection, provider);

                var testDac = new SimpleDac(testConnectionString, DatabaseProvider.SqlServer, logger);
                var traceProvider = CreateTraceProvider(provider, traceConnectionString, logger);
                var replayEngine = new ReplayEngine(testDac, traceProvider, logger);

                // Get session by name or ID
                TraceSession traceSession;
                if (Guid.TryParse(session, out var sessionId))
                {
                    traceSession = await traceProvider.GetSessionAsync(sessionId);
                }
                else
                {
                    traceSession = await traceProvider.GetSessionByNameAsync(session);
                }

                var options = new ReplayOptions
                {
                    SkipSelectStatements = skipSelects,
                    SkipSystemStatements = true,
                    ContinueOnError = continueOnError,
                    MaxConcurrentConnections = 1,
                    StatementTimeout = TimeSpan.FromSeconds(timeout),
                    AdditionalExcludePatterns = new string[0]
                };

                Console.WriteLine($"🔄 Starting replay for session '{traceSession.SessionName}'...");
                var result = await replayEngine.ReplayTraceAsync(traceSession.SessionId, options);

                Console.WriteLine($"✅ Replay completed:");
                Console.WriteLine($"   Session: {traceSession.SessionName}");
                Console.WriteLine($"   Duration: {(result.EndTime - result.StartTime).TotalSeconds:F1} seconds");
                Console.WriteLine($"   Total Statements: {result.TotalStatements}");
                Console.WriteLine($"   Successful: {result.SuccessfulStatements}");
                Console.WriteLine($"   Failed: {result.FailedStatements}");
                Console.WriteLine($"   Skipped: {result.SkippedStatements}");

                if (result.Errors.Any())
                {
                    Console.WriteLine($"\n⚠️  Errors encountered:");
                    foreach (var error in result.Errors.Take(5))
                    {
                        Console.WriteLine($"   Event {error.EventId}: {error.ErrorMessage}");
                    }
                    if (result.Errors.Count > 5)
                    {
                        Console.WriteLine($"   ... and {result.Errors.Count - 5} more errors");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error executing replay: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static async Task CaptureCdcDataAsync(string session, string captureType, string testConnection,
            string traceConnection, string provider)
        {
            var logger = CreateLogger();

            try
            {
                var testConnectionString = GetTestConnectionString(testConnection);
                var traceConnectionString = GetTraceConnectionString(traceConnection, provider);

                var testDac = new SimpleDac(testConnectionString, DatabaseProvider.SqlServer, logger);
                var traceProvider = CreateTraceProvider(provider, traceConnectionString, logger);
                var comparator = new CdcComparator(testDac, traceProvider, logger, new ComparisonConfiguration());

                var sessionId = Guid.Parse(session);

                Console.WriteLine($"📊 Capturing CDC data for session {sessionId}, type: {captureType}...");
                var capture = await comparator.CaptureCdcDataAsync(sessionId, captureType);

                Console.WriteLine($"✅ CDC data captured:");
                Console.WriteLine($"   Capture ID: {capture.CaptureId}");
                Console.WriteLine($"   Type: {capture.CaptureType}");
                Console.WriteLine($"   Records: {capture.RecordCount:N0}");
                Console.WriteLine($"   Tables: {capture.TableName}");
                Console.WriteLine($"   Hash: {capture.DataHash}");
                Console.WriteLine($"   Captured: {capture.CaptureTime:yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error capturing CDC data: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static async Task CompareCdcCapturesAsync(string leftCapture, string rightCapture,
            string traceConnection, string provider, string output)
        {
            var logger = CreateLogger();

            try
            {
                var traceConnectionString = GetTraceConnectionString(traceConnection, provider);
                var traceProvider = CreateTraceProvider(provider, traceConnectionString, logger);
                var comparator = new CdcComparator(null, traceProvider, logger, new ComparisonConfiguration());

                var leftCaptureId = Guid.Parse(leftCapture);
                var rightCaptureId = Guid.Parse(rightCapture);

                Console.WriteLine($"🔍 Comparing CDC captures {leftCaptureId} vs {rightCaptureId}...");
                var result = await comparator.CompareCapturesAsync(leftCaptureId, rightCaptureId);

                Console.WriteLine($"📋 Comparison Results:");
                Console.WriteLine($"   Overall Match: {(result.OverallMatch ? "✅ YES" : "❌ NO")}");
                Console.WriteLine($"   Total Differences: {result.TotalDifferences}");
                Console.WriteLine($"   Tables Compared: {result.TableComparisons.Count}");
                Console.WriteLine($"   Comparison Time: {result.ComparisonTime:yyyy-MM-dd HH:mm:ss}");

                if (result.TableComparisons.Any())
                {
                    Console.WriteLine($"\n📊 Table Details:");
                    Console.WriteLine($"{"Table Name",-30} {"Match",-8} {"Differences",-12}");
                    Console.WriteLine(new string('-', 55));

                    foreach (var table in result.TableComparisons.Values)
                    {
                        var matchIcon = table.IsMatch ? "✅" : "❌";
                        Console.WriteLine($"{table.TableName,-30} {matchIcon,-8} {table.DifferenceCount,-12}");
                    }
                }

                // Generate detailed report if output specified
                if (!string.IsNullOrEmpty(output))
                {
                    var report = await comparator.GenerateDifferenceReportAsync(result);
                    await File.WriteAllTextAsync(output, report.Summary);
                    Console.WriteLine($"\n📄 Detailed report saved to: {output}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error comparing CDC captures: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static async Task ExecuteWorkflowAsync(string database, string session, string snapshot,
            string testConnection, string traceConnection, string provider)
        {
            var logger = CreateLogger();

            try
            {
                var testConnectionString = GetTestConnectionString(testConnection);
                var traceConnectionString = GetTraceConnectionString(traceConnection, provider);

                var testDac = new SimpleDac(testConnectionString, DatabaseProvider.SqlServer, logger);
                var traceProvider = CreateTraceProvider(provider, traceConnectionString, logger);
                var snapshotManager = new SnapshotManager(testDac, logger);
                var traceManager = new TraceManager(testDac, traceProvider, logger);
                var replayEngine = new ReplayEngine(testDac, traceProvider, logger);
                var comparator = new CdcComparator(testDac, traceProvider, logger, new ComparisonConfiguration());

                Console.WriteLine($"🚀 Starting complete test workflow for '{session}'...");
                Console.WriteLine($"   Database: {database}");
                Console.WriteLine($"   Snapshot: {snapshot}");

                // Step 1: Create snapshot
                Console.WriteLine("\n1️⃣  Creating database snapshot...");
                await snapshotManager.CreateSnapshotAsync(database, snapshot);
                Console.WriteLine("✅ Snapshot created");

                // Step 2: Start tracing
                Console.WriteLine("\n2️⃣  Starting trace session...");
                var config = new TraceConfiguration
                {
                    DatabaseName = database,
                    SessionName = session,
                    Description = "Complete workflow test",
                    EventTypes = new[] { "sql_batch_completed", "rpc_completed" },
                    RingBufferSizeMB = 64
                };
                var traceSession = await traceManager.StartTraceAsync(config);
                Console.WriteLine($"✅ Trace session started: {traceSession.SessionId}");

                // Step 3: Wait for user to execute test scenarios
                Console.WriteLine("\n3️⃣  Execute your test scenarios now, then press Enter to continue...");
                Console.ReadLine();

                // Step 4: Stop trace and capture baseline CDC
                Console.WriteLine("\n4️⃣  Stopping trace and capturing baseline CDC data...");
                await traceManager.StopTraceAsync(traceSession.SessionId);
                var baselineCapture = await comparator.CaptureCdcDataAsync(traceSession.SessionId, "Baseline");
                Console.WriteLine($"✅ Baseline captured: {baselineCapture.RecordCount} records");

                // Step 5: Restore snapshot
                Console.WriteLine("\n5️⃣  Restoring from snapshot...");
                await snapshotManager.RestoreFromSnapshotAsync(database, snapshot);
                Console.WriteLine("✅ Database restored");

                // Step 6: Replay trace
                Console.WriteLine("\n6️⃣  Replaying trace...");
                var replayOptions = new ReplayOptions
                {
                    SkipSelectStatements = true,
                    ContinueOnError = false,
                    StatementTimeout = TimeSpan.FromSeconds(30)
                };
                var replayResult = await replayEngine.ReplayTraceAsync(traceSession.SessionId, replayOptions);
                Console.WriteLine($"✅ Replay completed: {replayResult.SuccessfulStatements}/{replayResult.TotalStatements} successful");

                // Step 7: Capture replay CDC data
                Console.WriteLine("\n7️⃣  Capturing replay CDC data...");
                var replayCapture = await comparator.CaptureCdcDataAsync(traceSession.SessionId, "Replay");
                Console.WriteLine($"✅ Replay data captured: {replayCapture.RecordCount} records");

                // Step 8: Compare captures
                Console.WriteLine("\n8️⃣  Comparing CDC captures...");
                var comparison = await comparator.CompareCapturesAsync(baselineCapture.CaptureId, replayCapture.CaptureId);

                Console.WriteLine($"\n🎯 Workflow Results:");
                Console.WriteLine($"   Data Match: {(comparison.OverallMatch ? "✅ PASS" : "❌ FAIL")}");
                Console.WriteLine($"   Differences: {comparison.TotalDifferences}");
                Console.WriteLine($"   Replay Success: {replayResult.SuccessfulStatements}/{replayResult.TotalStatements}");

                // Step 9: Cleanup
                Console.WriteLine("\n9️⃣  Cleaning up...");
                await snapshotManager.DropSnapshotAsync(snapshot);
                Console.WriteLine("✅ Cleanup completed");

                Console.WriteLine($"\n🏁 Workflow completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Workflow failed: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static string GetTestConnectionString(string connection)
        {
            if (!string.IsNullOrEmpty(connection))
                return connection;

            var envConnection = Environment.GetEnvironmentVariable("CDC_TEST_CONNECTION");
            if (!string.IsNullOrEmpty(envConnection))
                return envConnection;

            // No hardcoded fallback - require environment variable
            throw new InvalidOperationException("Test connection string not provided. Please specify --test-connection parameter or set CDC_TEST_CONNECTION environment variable.");
        }

        private static string GetTraceConnectionString(string connection, string provider)
        {
            if (!string.IsNullOrEmpty(connection))
                return connection;

            var envConnection = Environment.GetEnvironmentVariable("CDC_TRACE_CONNECTION");
            if (!string.IsNullOrEmpty(envConnection))
                return envConnection;

            // No hardcoded fallback - require environment variable
            throw new InvalidOperationException("Trace connection string not provided. Please specify --trace-connection parameter or set CDC_TRACE_CONNECTION environment variable.");
        }

        private static ITraceDataProvider CreateTraceProvider(string provider, string connectionString, ILogger logger)
        {
            var config = new TraceStorageConfiguration
            {
                Provider = provider,
                ConnectionString = connectionString,
                AutoCreateSchema = false,
                CommandTimeout = 30
            };

            return provider.ToLower() switch
            {
                "postgresql" => new PostgreSqlTraceProvider(config, logger),
                "sqlserver" => new SqlServerTraceProvider(config, logger),
                _ => throw new ArgumentException($"Unsupported provider: {provider}")
            };
        }

        private static ILogger CreateLogger()
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
                builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            return loggerFactory.CreateLogger("ReplayCommand");
        }
    }
}