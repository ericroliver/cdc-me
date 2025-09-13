using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Softbase.Cdc.Data
{
    public enum DatabaseRole
    {
        TestDatabase,    // The database being tested (always SQL Server)
        CdcMeDatabase   // Storage for traces/snapshots (PostgreSQL)
    }

    public enum DatabaseProvider
    {
        SqlServer,
        PostgreSQL
    }

    public interface IDatabaseConnectionFactory
    {
        IDbConnection CreateConnection(DatabaseRole role);
        SimpleDac CreateDac(DatabaseRole role, ILogger logger);
        string GetConnectionString(DatabaseRole role);
        DatabaseProvider GetProvider(DatabaseRole role);
        Task<bool> TestConnectionAsync(DatabaseRole role);
    }

    public class DatabaseConnectionFactory : IDatabaseConnectionFactory
    {
        private readonly Dictionary<DatabaseRole, string> _connectionStrings;
        private readonly Dictionary<DatabaseRole, DatabaseProvider> _providers;
        private readonly ILogger<DatabaseConnectionFactory> _logger;

        public DatabaseConnectionFactory(
            Dictionary<DatabaseRole, string> connectionStrings,
            Dictionary<DatabaseRole, DatabaseProvider> providers,
            ILogger<DatabaseConnectionFactory> logger)
        {
            _connectionStrings = connectionStrings ?? throw new ArgumentNullException(nameof(connectionStrings));
            _providers = providers ?? throw new ArgumentNullException(nameof(providers));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IDbConnection CreateConnection(DatabaseRole role)
        {
            var connectionString = GetConnectionString(role);
            var provider = GetProvider(role);

            return provider switch
            {
                DatabaseProvider.SqlServer => new SqlConnection(connectionString),
                DatabaseProvider.PostgreSQL => new NpgsqlConnection(connectionString),
                _ => throw new NotSupportedException($"Provider {provider} not supported")
            };
        }

        public SimpleDac CreateDac(DatabaseRole role, ILogger logger)
        {
            var connectionString = GetConnectionString(role);
            var provider = GetProvider(role);
            return new SimpleDac(connectionString, provider, logger);
        }

        public string GetConnectionString(DatabaseRole role)
        {
            if (!_connectionStrings.TryGetValue(role, out var connectionString) || string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException($"Connection string for {role} not found or is empty");
            }

            return connectionString;
        }

        public DatabaseProvider GetProvider(DatabaseRole role)
        {
            if (_providers.TryGetValue(role, out var provider))
            {
                return provider;
            }

            // Default providers based on role
            return role switch
            {
                DatabaseRole.TestDatabase => DatabaseProvider.SqlServer,
                DatabaseRole.CdcMeDatabase => DatabaseProvider.PostgreSQL,
                _ => throw new ArgumentException($"Unknown database role: {role}")
            };
        }

        public async Task<bool> TestConnectionAsync(DatabaseRole role)
        {
            try
            {
                using var connection = CreateConnection(role);
                var provider = GetProvider(role);

                // Handle async connection opening based on provider
                switch (provider)
                {
                    case DatabaseProvider.SqlServer when connection is SqlConnection sqlConn:
                        await sqlConn.OpenAsync();
                        break;
                    case DatabaseProvider.PostgreSQL when connection is NpgsqlConnection npgsqlConn:
                        await npgsqlConn.OpenAsync();
                        break;
                    default:
                        connection.Open();
                        break;
                }

                _logger.LogInformation("Successfully tested connection for {Role}", role);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to test connection for {Role}", role);
                return false;
            }
        }
    }

    // Configuration-based factory for use in API projects that have Microsoft.Extensions.Configuration
    public class ConfigurationBasedDatabaseConnectionFactory : IDatabaseConnectionFactory
    {
        private readonly IDatabaseConnectionFactory _innerFactory;

        public ConfigurationBasedDatabaseConnectionFactory(object configuration, ILogger<DatabaseConnectionFactory> logger)
        {
            // This will be implemented when we update the API project
            // For now, we'll use environment variables or a simple approach
            var connectionStrings = new Dictionary<DatabaseRole, string>
            {
                [DatabaseRole.TestDatabase] = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION") ?? "",
                [DatabaseRole.CdcMeDatabase] = Environment.GetEnvironmentVariable("CDCME_DB_CONNECTION") ?? ""
            };

            var providers = new Dictionary<DatabaseRole, DatabaseProvider>
            {
                [DatabaseRole.TestDatabase] = ParseProvider(Environment.GetEnvironmentVariable("TEST_DB_PROVIDER"), DatabaseProvider.SqlServer),
                [DatabaseRole.CdcMeDatabase] = ParseProvider(Environment.GetEnvironmentVariable("CDCME_DB_PROVIDER"), DatabaseProvider.PostgreSQL)
            };

            _innerFactory = new DatabaseConnectionFactory(connectionStrings, providers, logger);
        }

        private static DatabaseProvider ParseProvider(string? providerString, DatabaseProvider defaultProvider)
        {
            if (string.IsNullOrEmpty(providerString))
                return defaultProvider;

            return Enum.TryParse<DatabaseProvider>(providerString, ignoreCase: true, out var provider)
                ? provider
                : defaultProvider;
        }

        public IDbConnection CreateConnection(DatabaseRole role) => _innerFactory.CreateConnection(role);
        public SimpleDac CreateDac(DatabaseRole role, ILogger logger) => _innerFactory.CreateDac(role, logger);
        public string GetConnectionString(DatabaseRole role) => _innerFactory.GetConnectionString(role);
        public DatabaseProvider GetProvider(DatabaseRole role) => _innerFactory.GetProvider(role);
        public Task<bool> TestConnectionAsync(DatabaseRole role) => _innerFactory.TestConnectionAsync(role);
    }
}