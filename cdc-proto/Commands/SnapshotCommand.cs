using System.CommandLine;
using Microsoft.Extensions.Logging;
using Softbase;
using Softbase.Cdc.Data;
using Softbase.Cdc.Trace;

namespace CdcProto.Commands
{
    public static class SnapshotCommand
    {
        public static Command CreateCommand()
        {
            var snapshotCommand = new Command("snapshot", "Manage database snapshots for testing");

            // Create snapshot command
            var createCommand = new Command("create", "Create a database snapshot");
            var databaseOption = new Option<string>("--database", "Database name to snapshot") { IsRequired = true };
            var nameOption = new Option<string>("--name", "Snapshot name") { IsRequired = true };
            var connectionOption = new Option<string>("--connection", "Connection string to SQL Server");

            createCommand.AddOption(databaseOption);
            createCommand.AddOption(nameOption);
            createCommand.AddOption(connectionOption);
            createCommand.SetHandler(CreateSnapshotAsync, databaseOption, nameOption, connectionOption);

            // Restore snapshot command
            var restoreCommand = new Command("restore", "Restore database from snapshot");
            var restoreDatabaseOption = new Option<string>("--database", "Database name to restore") { IsRequired = true };
            var restoreSnapshotOption = new Option<string>("--snapshot", "Snapshot name to restore from") { IsRequired = true };
            var restoreConnectionOption = new Option<string>("--connection", "Connection string to SQL Server");

            restoreCommand.AddOption(restoreDatabaseOption);
            restoreCommand.AddOption(restoreSnapshotOption);
            restoreCommand.AddOption(restoreConnectionOption);
            restoreCommand.SetHandler(RestoreSnapshotAsync, restoreDatabaseOption, restoreSnapshotOption, restoreConnectionOption);

            // List snapshots command
            var listCommand = new Command("list", "List all database snapshots");
            var listConnectionOption = new Option<string>("--connection", "Connection string to SQL Server");
            listCommand.AddOption(listConnectionOption);
            listCommand.SetHandler(ListSnapshotsAsync, listConnectionOption);

            // Drop snapshot command
            var dropCommand = new Command("drop", "Drop a database snapshot");
            var dropNameOption = new Option<string>("--name", "Snapshot name to drop") { IsRequired = true };
            var dropConnectionOption = new Option<string>("--connection", "Connection string to SQL Server");

            dropCommand.AddOption(dropNameOption);
            dropCommand.AddOption(dropConnectionOption);
            dropCommand.SetHandler(DropSnapshotAsync, dropNameOption, dropConnectionOption);

            // Info command
            var infoCommand = new Command("info", "Get information about a snapshot");
            var infoNameOption = new Option<string>("--name", "Snapshot name") { IsRequired = true };
            var infoConnectionOption = new Option<string>("--connection", "Connection string to SQL Server");

            infoCommand.AddOption(infoNameOption);
            infoCommand.AddOption(infoConnectionOption);
            infoCommand.SetHandler(GetSnapshotInfoAsync, infoNameOption, infoConnectionOption);

            snapshotCommand.AddCommand(createCommand);
            snapshotCommand.AddCommand(restoreCommand);
            snapshotCommand.AddCommand(listCommand);
            snapshotCommand.AddCommand(dropCommand);
            snapshotCommand.AddCommand(infoCommand);

            return snapshotCommand;
        }

        private static async Task CreateSnapshotAsync(string database, string name, string connection)
        {
            var logger = CreateLogger();
            var connectionString = GetConnectionString(connection);

            try
            {
                var dac = new SimpleDac(connectionString, DatabaseProvider.SqlServer, logger);
                var snapshotManager = new SnapshotManager(dac, logger);

                Console.WriteLine($"Creating snapshot '{name}' for database '{database}'...");
                var result = await snapshotManager.CreateSnapshotAsync(database, name);
                Console.WriteLine($"✅ Successfully created snapshot: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error creating snapshot: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static async Task RestoreSnapshotAsync(string database, string snapshot, string connection)
        {
            var logger = CreateLogger();
            var connectionString = GetConnectionString(connection);

            try
            {
                var dac = new SimpleDac(connectionString, DatabaseProvider.SqlServer, logger);
                var snapshotManager = new SnapshotManager(dac, logger);

                Console.WriteLine($"Restoring database '{database}' from snapshot '{snapshot}'...");
                await snapshotManager.RestoreFromSnapshotAsync(database, snapshot);
                Console.WriteLine($"✅ Successfully restored database '{database}' from snapshot '{snapshot}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error restoring snapshot: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static async Task ListSnapshotsAsync(string connection)
        {
            var logger = CreateLogger();
            var connectionString = GetConnectionString(connection);

            try
            {
                var dac = new SimpleDac(connectionString, DatabaseProvider.SqlServer, logger);
                var snapshotManager = new SnapshotManager(dac, logger);

                Console.WriteLine("📋 Database Snapshots:");
                Console.WriteLine(new string('-', 80));

                var snapshots = await snapshotManager.ListSnapshotsAsync();
                if (!snapshots.Any())
                {
                    Console.WriteLine("No snapshots found.");
                    return;
                }

                Console.WriteLine($"{"Name",-30} {"Source Database",-20} {"Created",-20} {"Size",-10} {"Status",-10}");
                Console.WriteLine(new string('-', 80));

                foreach (var snapshot in snapshots)
                {
                    var sizeInMB = snapshot.SizeInBytes / (1024 * 1024);
                    Console.WriteLine($"{snapshot.SnapshotName,-30} {snapshot.SourceDatabase,-20} {snapshot.CreatedTime:yyyy-MM-dd HH:mm,-20} {sizeInMB:N0} MB{"",-5} {snapshot.Status,-10}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error listing snapshots: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static async Task DropSnapshotAsync(string name, string connection)
        {
            var logger = CreateLogger();
            var connectionString = GetConnectionString(connection);

            try
            {
                var dac = new SimpleDac(connectionString, DatabaseProvider.SqlServer, logger);
                var snapshotManager = new SnapshotManager(dac, logger);

                Console.WriteLine($"Dropping snapshot '{name}'...");
                await snapshotManager.DropSnapshotAsync(name);
                Console.WriteLine($"✅ Successfully dropped snapshot: {name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error dropping snapshot: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static async Task GetSnapshotInfoAsync(string name, string connection)
        {
            var logger = CreateLogger();
            var connectionString = GetConnectionString(connection);

            try
            {
                var dac = new SimpleDac(connectionString, DatabaseProvider.SqlServer, logger);
                var snapshotManager = new SnapshotManager(dac, logger);

                var info = await snapshotManager.GetSnapshotInfoAsync(name);

                Console.WriteLine($"📊 Snapshot Information: {name}");
                Console.WriteLine(new string('-', 50));
                Console.WriteLine($"Name:            {info.SnapshotName}");
                Console.WriteLine($"Source Database: {info.SourceDatabase}");
                Console.WriteLine($"Created:         {info.CreatedTime:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"Size:            {info.SizeInBytes / (1024 * 1024):N0} MB");
                Console.WriteLine($"Status:          {info.Status}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting snapshot info: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static string GetConnectionString(string connection)
        {
            if (!string.IsNullOrEmpty(connection))
                return connection;

            // Try environment variable
            var envConnection = Environment.GetEnvironmentVariable("CDC_SQL_CONNECTION");
            if (!string.IsNullOrEmpty(envConnection))
                return envConnection;

            // No hardcoded fallback - require environment variable
            throw new InvalidOperationException("Connection string not provided. Please specify --connection parameter or set CDC_SQL_CONNECTION environment variable.");
        }

        private static ILogger CreateLogger()
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
                builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            return loggerFactory.CreateLogger("SnapshotCommand");
        }
    }
}