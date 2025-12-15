using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Softbase.Cdc.Data;

namespace Softbase.Cdc.Configuration
{
    public class ValidationResult
    {
        public bool IsValid => !Errors.Any();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Info { get; set; } = new();
    }

    public class DatabaseConfigurationValidator
    {
        private readonly IDatabaseConnectionFactory _factory;
        private readonly ILogger<DatabaseConfigurationValidator> _logger;

        public DatabaseConfigurationValidator(
            IDatabaseConnectionFactory factory,
            ILogger<DatabaseConfigurationValidator> logger)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ValidationResult> ValidateAsync()
        {
            var result = new ValidationResult();

            _logger.LogInformation("Starting database configuration validation");

            // Validate TEST_DB configuration
            await ValidateTestDatabase(result);

            // Validate CDCME_DB configuration
            await ValidateCdcMeDatabase(result);

            // Cross-validation checks
            ValidateCrossConfiguration(result);

            _logger.LogInformation("Database configuration validation completed. Valid: {IsValid}, Errors: {ErrorCount}, Warnings: {WarningCount}",
                result.IsValid, result.Errors.Count, result.Warnings.Count);

            return result;
        }

        private async Task ValidateTestDatabase(ValidationResult result)
        {
            try
            {
                _logger.LogDebug("Validating TEST_DB configuration");

                // Check provider type
                var provider = _factory.GetProvider(DatabaseRole.TestDatabase);
                if (provider != DatabaseProvider.SqlServer)
                {
                    result.Errors.Add("TEST_DB must use SQL Server provider for snapshot and Extended Events support");
                }
                else
                {
                    result.Info.Add("TEST_DB correctly configured to use SQL Server");
                }

                // Check connection string
                var connectionString = _factory.GetConnectionString(DatabaseRole.TestDatabase);
                if (string.IsNullOrEmpty(connectionString))
                {
                    result.Errors.Add("TEST_DB_CONNECTION is required");
                    return;
                }

                result.Info.Add($"TEST_DB connection string configured: {MaskConnectionString(connectionString)}");

                // Test connection
                var canConnect = await _factory.TestConnectionAsync(DatabaseRole.TestDatabase);
                if (!canConnect)
                {
                    result.Errors.Add("Cannot connect to TEST_DB - please verify connection string and database availability");
                }
                else
                {
                    result.Info.Add("TEST_DB connection test successful");
                }

                // Additional SQL Server specific validations
                if (provider == DatabaseProvider.SqlServer && canConnect)
                {
                    await ValidateSqlServerCapabilities(DatabaseRole.TestDatabase, result);
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"TEST_DB configuration error: {ex.Message}");
                _logger.LogError(ex, "Error validating TEST_DB configuration");
            }
        }

        private async Task ValidateCdcMeDatabase(ValidationResult result)
        {
            try
            {
                _logger.LogDebug("Validating CDCME_DB configuration");

                // Check provider type
                var provider = _factory.GetProvider(DatabaseRole.CdcMeDatabase);
                if (provider != DatabaseProvider.PostgreSQL)
                {
                    result.Warnings.Add("CDCME_DB should use PostgreSQL provider (recommended for trace storage)");
                }
                else
                {
                    result.Info.Add("CDCME_DB correctly configured to use PostgreSQL");
                }

                // Check connection string
                var connectionString = _factory.GetConnectionString(DatabaseRole.CdcMeDatabase);
                if (string.IsNullOrEmpty(connectionString))
                {
                    result.Errors.Add("CDCME_DB_CONNECTION is required");
                    return;
                }

                result.Info.Add($"CDCME_DB connection string configured: {MaskConnectionString(connectionString)}");

                // Test connection
                var canConnect = await _factory.TestConnectionAsync(DatabaseRole.CdcMeDatabase);
                if (!canConnect)
                {
                    result.Errors.Add("Cannot connect to CDCME_DB - please verify connection string and database availability");
                }
                else
                {
                    result.Info.Add("CDCME_DB connection test successful");
                }

                // Additional PostgreSQL specific validations
                if (provider == DatabaseProvider.PostgreSQL && canConnect)
                {
                    await ValidatePostgreSqlCapabilities(DatabaseRole.CdcMeDatabase, result);
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"CDCME_DB configuration error: {ex.Message}");
                _logger.LogError(ex, "Error validating CDCME_DB configuration");
            }
        }

        private void ValidateCrossConfiguration(ValidationResult result)
        {
            try
            {
                // Ensure different databases are not using the same connection string
                var testDbConnection = _factory.GetConnectionString(DatabaseRole.TestDatabase);
                var cdcMeConnection = _factory.GetConnectionString(DatabaseRole.CdcMeDatabase);

                if (testDbConnection.Equals(cdcMeConnection, StringComparison.OrdinalIgnoreCase))
                {
                    result.Warnings.Add("TEST_DB and CDCME_DB are using the same connection string - this may cause conflicts");
                }

                // Validate provider combinations
                var testDbProvider = _factory.GetProvider(DatabaseRole.TestDatabase);
                var cdcMeProvider = _factory.GetProvider(DatabaseRole.CdcMeDatabase);

                if (testDbProvider == DatabaseProvider.SqlServer && cdcMeProvider == DatabaseProvider.PostgreSQL)
                {
                    result.Info.Add("Optimal configuration: SQL Server for TEST_DB, PostgreSQL for CDCME_DB");
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Cross-configuration validation warning: {ex.Message}");
                _logger.LogWarning(ex, "Error during cross-configuration validation");
            }
        }

        private Task ValidateSqlServerCapabilities(DatabaseRole role, ValidationResult result)
        {
            try
            {
                using var connection = _factory.CreateConnection(role);
                connection.Open();

                using var command = connection.CreateCommand();

                // Check SQL Server version
                command.CommandText = "SELECT @@VERSION";
                var version = command.ExecuteScalar()?.ToString();
                result.Info.Add($"SQL Server version: {version?.Split('\n')[0]}");

                // Check if Extended Events are supported
                command.CommandText = "SELECT COUNT(*) FROM sys.server_event_sessions";
                var extendedEventsSupported = Convert.ToInt32(command.ExecuteScalar()) >= 0;
                if (extendedEventsSupported)
                {
                    result.Info.Add("Extended Events are supported");
                }
                else
                {
                    result.Warnings.Add("Extended Events may not be fully supported on this SQL Server version");
                }

                // Check snapshot isolation support
                command.CommandText = "SELECT snapshot_isolation_state FROM sys.databases WHERE name = DB_NAME()";
                var snapshotIsolation = command.ExecuteScalar();
                if (snapshotIsolation != null)
                {
                    result.Info.Add($"Snapshot isolation state: {snapshotIsolation}");
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Could not validate SQL Server capabilities: {ex.Message}");
                _logger.LogWarning(ex, "Error validating SQL Server capabilities");
            }
            return Task.CompletedTask;
        }

        private Task ValidatePostgreSqlCapabilities(DatabaseRole role, ValidationResult result)
        {
            try
            {
                using var connection = _factory.CreateConnection(role);
                connection.Open();

                using var command = connection.CreateCommand();

                // Check PostgreSQL version
                command.CommandText = "SELECT version()";
                var version = command.ExecuteScalar()?.ToString();
                result.Info.Add($"PostgreSQL version: {version?.Split(' ')[1]}");

                // Check if JSON/JSONB support is available
                command.CommandText = "SELECT COUNT(*) FROM pg_type WHERE typname = 'jsonb'";
                var jsonbSupported = Convert.ToInt32(command.ExecuteScalar()) > 0;
                if (jsonbSupported)
                {
                    result.Info.Add("JSONB support is available (recommended for trace data storage)");
                }
                else
                {
                    result.Warnings.Add("JSONB support not detected - trace data storage may be less efficient");
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Could not validate PostgreSQL capabilities: {ex.Message}");
                _logger.LogWarning(ex, "Error validating PostgreSQL capabilities");
            }
            return Task.CompletedTask;
        }

        private static string MaskConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return "[empty]";

            // Mask sensitive information in connection string
            var masked = connectionString;

            // Common password patterns
            var passwordPatterns = new[]
            {
                @"Password=([^;]+)",
                @"Pwd=([^;]+)",
                @"password=([^;]+)",
                @"pwd=([^;]+)"
            };

            foreach (var pattern in passwordPatterns)
            {
                masked = System.Text.RegularExpressions.Regex.Replace(
                    masked, pattern, m => m.Value.Substring(0, m.Value.IndexOf('=') + 1) + "***",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return masked;
        }
    }
}
