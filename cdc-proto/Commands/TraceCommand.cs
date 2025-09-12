using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Softbase;
using Softbase.Cdc.Trace;
using Softbase.Cdc.Models;
using Newtonsoft.Json;
using System.IO;

namespace CdcProto.Commands
{
    public static class TraceCommand
    {
        public static Command CreateCommand()
        {
            var traceCommand = new Command("trace", "Manage SQL tracing sessions");

            // Start trace command
            var startCommand = new Command("start", "Start a new trace session");
            var databaseOption = new Option<string>("--database", "Database name to trace") { IsRequired = true };
            var sessionOption = new Option<string>("--session", "Trace session name") { IsRequired = true };
            var testConnectionOption = new Option<string>("--test-connection", "Test database connection string");
            var traceConnectionOption = new Option<string>("--trace-connection", "Trace database connection string");
            var providerOption = new Option<string>("--provider", () => "PostgreSQL", "Trace database provider (PostgreSQL|SqlServer)");
            var descriptionOption = new Option<string>("--description", "Session description");

            startCommand.AddOption(databaseOption);
            startCommand.AddOption(sessionOption);
            startCommand.AddOption(testConnectionOption);
            startCommand.AddOption(traceConnectionOption);
            startCommand.AddOption(providerOption);
            startCommand.AddOption(descriptionOption);
            startCommand.SetHandler(StartTraceAsync, databaseOption, sessionOption, testConnectionOption, traceConnectionOption, providerOption, descriptionOption);

            // Stop trace command
            var stopCommand = new Command("stop", "Stop a trace session");
            var stopSessionOption = new Option<string>("--session", "Session name or ID") { IsRequired = true };
            var stopTraceConnectionOption = new Option<string>("--trace-connection", "Trace database connection string");
            var stopProviderOption = new Option<string>("--provider", () => "PostgreSQL", "Trace database provider");

            stopCommand.AddOption(stopSessionOption);
            stopCommand.AddOption(stopTraceConnectionOption);
            stopCommand.AddOption(stopProviderOption);
            stopCommand.SetHandler(StopTraceAsync, stopSessionOption, stopTraceConnectionOption, stopProviderOption);

            // Status command
            var statusCommand = new Command("status", "Get trace session status");
            var statusSessionOption = new Option<string>("--session", "Session name or ID") { IsRequired = true };
            var statusTraceConnectionOption = new Option<string>("--trace-connection", "Trace database connection string");
            var statusProviderOption = new Option<string>("--provider", () => "PostgreSQL", "Trace database provider");

            statusCommand.AddOption(statusSessionOption);
            statusCommand.AddOption(statusTraceConnectionOption);
            statusCommand.AddOption(statusProviderOption);
            statusCommand.SetHandler(GetTraceStatusAsync, statusSessionOption, statusTraceConnectionOption, statusProviderOption);

            // List sessions command
            var listCommand = new Command("list", "List active trace sessions");
            var listTraceConnectionOption = new Option<string>("--trace-connection", "Trace database connection string");
            var listProviderOption = new Option<string>("--provider", () => "PostgreSQL", "Trace database provider");

            listCommand.AddOption(listTraceConnectionOption);
            listCommand.AddOption(listProviderOption);
            listCommand.SetHandler(ListActiveSessionsAsync, listTraceConnectionOption, listProviderOption);

            // Export command
            var exportCommand = new Command("export", "Export trace data to JSON file");
            var exportSessionOption = new Option<string>("--session", "Session name or ID") { IsRequired = true };
            var exportOutputOption = new Option<string>("--output", "Output file path") { IsRequired = true };
            var exportTraceConnectionOption = new Option<string>("--trace-connection", "Trace database connection string");
            var exportProviderOption = new Option<string>("--provider", () => "PostgreSQL", "Trace database provider");

            exportCommand.AddOption(exportSessionOption);
            exportCommand.AddOption(exportOutputOption);
            exportCommand.AddOption(exportTraceConnectionOption);
            exportCommand.AddOption(exportProviderOption);
            exportCommand.SetHandler(ExportTraceDataAsync, exportSessionOption, exportOutputOption, exportTraceConnectionOption, exportProviderOption);

            traceCommand.AddCommand(startCommand);
            traceCommand.AddCommand(stopCommand);
            traceCommand.AddCommand(statusCommand);
            traceCommand.AddCommand(listCommand);
            traceCommand.AddCommand(exportCommand);

            return traceCommand;
        }

        private static async Task StartTraceAsync(string database, string session, string testConnection, string traceConnection, string provider, string description)
        {
            var logger = CreateLogger();

            try
            {
                var testConnectionString = GetTestConnectionString(testConnection);
                var traceConnectionString = GetTraceConnectionString(traceConnection, provider);

                var testDac = new SimpleDac(testConnectionString, logger);
                var traceProvider = CreateTraceProvider(provider, traceConnectionString, logger);
                var traceManager = new TraceManager(testDac, traceProvider, logger);

                var config = new TraceConfiguration
                {
                    DatabaseName = database,
                    SessionName = session,
                    Description = description ?? "",
                    EventTypes = new[] { "sql_batch_completed", "rpc_completed" },
                    ExcludePatterns = new[] { "SELECT%", "sys.%", "INFORMATION_SCHEMA%" },
                    RingBufferSizeMB = 64,
                    CaptureStatementText = true,
                    CapturePerformanceMetrics = true
                };

                Console.WriteLine($"Starting trace session '{session}' for database '{database}'...");
                var traceSession = await traceManager.StartTraceAsync(config, testConnectionString);

                Console.WriteLine($"✅ Successfully started trace session:");
                Console.WriteLine($"   Session ID: {traceSession.SessionId}");
                Console.WriteLine($"   Session Name: {traceSession.SessionName}");
                Console.WriteLine($"   Database: {traceSession.TestDatabase}");
                Console.WriteLine($"   Started: {traceSession.StartTime:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"   Status: {traceSession.Status}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error starting trace session: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static async Task StopTraceAsync(string session, string traceConnection, string provider)
        {
            var logger = CreateLogger();

            try
            {
                var traceConnectionString = GetTraceConnectionString(traceConnection, provider);
                var traceProvider = CreateTraceProvider(provider, traceConnectionString, logger);

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

                var testDac = new SimpleDac(traceSession.TestConnectionString, logger);
                var traceManager = new TraceManager(testDac, traceProvider, logger);

                Console.WriteLine($"Stopping trace session '{traceSession.SessionName}'...");
                var stoppedSession = await traceManager.StopTraceAsync(traceSession.SessionId);

                Console.WriteLine($"✅ Successfully stopped trace session:");
                Console.WriteLine($"   Session ID: {stoppedSession.SessionId}");
                Console.WriteLine($"   Session Name: {stoppedSession.SessionName}");
                Console.WriteLine($"   Started: {stoppedSession.StartTime:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"   Ended: {stoppedSession.EndTime:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"   Status: {stoppedSession.Status}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error stopping trace session: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static async Task GetTraceStatusAsync(string session, string traceConnection, string provider)
        {
            var logger = CreateLogger();

            try
            {
                var traceConnectionString = GetTraceConnectionString(traceConnection, provider);
                var traceProvider = CreateTraceProvider(provider, traceConnectionString, logger);

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

                var testDac = new SimpleDac(traceSession.TestConnectionString, logger);
                var traceManager = new TraceManager(testDac, traceProvider, logger);

                var status = await traceManager.GetTraceStatusAsync(traceSession.SessionId);

                Console.WriteLine($"📊 Trace Session Status: {traceSession.SessionName}");
                Console.WriteLine(new string('-', 50));
                Console.WriteLine($"Session ID:    {status.SessionId}");
                Console.WriteLine($"State:         {status.State}");
                Console.WriteLine($"Started:       {status.StartedAt:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"Event Count:   {status.EventCount:N0}");
                if (!string.IsNullOrEmpty(status.LastError))
                {
                    Console.WriteLine($"Last Error:    {status.LastError}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting trace status: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static async Task ListActiveSessionsAsync(string traceConnection, string provider)
        {
            var logger = CreateLogger();

            try
            {
                var traceConnectionString = GetTraceConnectionString(traceConnection, provider);
                var traceProvider = CreateTraceProvider(provider, traceConnectionString, logger);

                Console.WriteLine("📋 Active Trace Sessions:");
                Console.WriteLine(new string('-', 100));

                var sessions = await traceProvider.GetActiveSessionsAsync();
                if (!sessions.Any())
                {
                    Console.WriteLine("No active trace sessions found.");
                    return;
                }

                Console.WriteLine($"{"Session Name",-25} {"Database",-15} {"Started",-20} {"Status",-10} {"Created By",-15}");
                Console.WriteLine(new string('-', 100));

                foreach (var session in sessions)
                {
                    Console.WriteLine($"{session.SessionName,-25} {session.TestDatabase,-15} {session.StartTime:yyyy-MM-dd HH:mm,-20} {session.Status,-10} {session.CreatedBy,-15}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error listing active sessions: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static async Task ExportTraceDataAsync(string session, string output, string traceConnection, string provider)
        {
            var logger = CreateLogger();

            try
            {
                var traceConnectionString = GetTraceConnectionString(traceConnection, provider);
                var traceProvider = CreateTraceProvider(provider, traceConnectionString, logger);

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

                Console.WriteLine($"Exporting trace data for session '{traceSession.SessionName}'...");

                var events = await traceProvider.GetTraceEventsAsync(traceSession.SessionId);
                var exportData = new
                {
                    Session = traceSession,
                    Events = events,
                    ExportedAt = DateTime.UtcNow
                };

                var json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
                await File.WriteAllTextAsync(output, json);

                Console.WriteLine($"✅ Successfully exported {events.Count()} events to: {output}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error exporting trace data: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static string GetTestConnectionString(string connection)
        {
            if (!string.IsNullOrEmpty(connection))
                return connection;

            // Try environment variable
            var envConnection = Environment.GetEnvironmentVariable("CDC_TEST_CONNECTION");
            if (!string.IsNullOrEmpty(envConnection))
                return envConnection;

            // Default connection string for blue.local
            return "Server=blue.local;Database=master;User Id=sa;Password=A123_Z321!;TrustServerCertificate=true;";
        }

        private static string GetTraceConnectionString(string connection, string provider)
        {
            if (!string.IsNullOrEmpty(connection))
                return connection;

            // Try environment variable
            var envConnection = Environment.GetEnvironmentVariable("CDC_TRACE_CONNECTION");
            if (!string.IsNullOrEmpty(envConnection))
                return envConnection;

            // Default connection strings for blue.local
            return provider.ToLower() switch
            {
                "postgresql" => "Host=blue.local;Database=cdc_tracedb;Username=postgres;Password=A123_Z321!",
                "sqlserver" => "Server=blue.local;Database=CDC_TraceDB;User Id=sa;Password=A123_Z321!;TrustServerCertificate=true;",
                _ => throw new ArgumentException($"Unsupported provider: {provider}")
            };
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
            return loggerFactory.CreateLogger("TraceCommand");
        }
    }
}