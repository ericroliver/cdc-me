using System.Data;
using Softbase;
using Softbase.Cdc.Data;

namespace cdc_api.Data
{
    public class ApiDatabaseConnectionFactory : IDatabaseConnectionFactory
    {
        private readonly IDatabaseConnectionFactory _innerFactory;

        public ApiDatabaseConnectionFactory(IConfiguration configuration, ILogger<ApiDatabaseConnectionFactory> logger)
        {
            // Extract connection strings from configuration
            var connectionStrings = new Dictionary<DatabaseRole, string>
            {
                [DatabaseRole.TestDatabase] = GetConnectionString(configuration, "TEST_DB_CONNECTION"),
                [DatabaseRole.CdcMeDatabase] = GetConnectionString(configuration, "CDCME_DB_CONNECTION")
            };

            // Extract providers from configuration
            var providers = new Dictionary<DatabaseRole, DatabaseProvider>
            {
                [DatabaseRole.TestDatabase] = ParseProvider(configuration["TEST_DB_PROVIDER"], DatabaseProvider.SqlServer),
                [DatabaseRole.CdcMeDatabase] = ParseProvider(configuration["CDCME_DB_PROVIDER"], DatabaseProvider.PostgreSQL)
            };

            // Create the inner factory with a logger that matches the expected type
            var factoryLogger = logger as ILogger<DatabaseConnectionFactory> ??
                               new LoggerAdapter<DatabaseConnectionFactory>(logger);

            _innerFactory = new DatabaseConnectionFactory(connectionStrings, providers, factoryLogger);
        }

        private static string GetConnectionString(IConfiguration configuration, string key)
        {
            // Try ConnectionStrings section first, then direct configuration
            var connectionString = configuration.GetConnectionString(key) ?? configuration[key];

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException($"Connection string for {key} not found in configuration");
            }

            return connectionString;
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

    // Logger adapter to convert between different logger types
    public class LoggerAdapter<T> : ILogger<T>
    {
        private readonly ILogger _logger;

        public LoggerAdapter(ILogger logger)
        {
            _logger = logger;
        }

        public IDisposable BeginScope<TState>(TState state) => _logger.BeginScope(state) ?? new EmptyDisposable();
        public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => _logger.Log(logLevel, eventId, state, exception, formatter);
    }

    internal class EmptyDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
