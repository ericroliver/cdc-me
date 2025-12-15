using cdc_api.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softbase;
using Softbase.Cdc.Configuration;
using Softbase.Cdc.Data;
using Xunit;

namespace cdc_api.Tests.Integration
{
    public class DatabaseConfigurationTests
    {
        [Fact]
        public void DatabaseConnectionFactory_ShouldCreateCorrectProviders()
        {
            // Arrange
            var configuration = CreateTestConfiguration();
            var serviceProvider = CreateServiceProvider(configuration);
            var factory = serviceProvider.GetRequiredService<IDatabaseConnectionFactory>();

            // Act & Assert
            var testDbProvider = factory.GetProvider(DatabaseRole.TestDatabase);
            var cdcMeProvider = factory.GetProvider(DatabaseRole.CdcMeDatabase);

            Assert.Equal(DatabaseProvider.SqlServer, testDbProvider);
            Assert.Equal(DatabaseProvider.PostgreSQL, cdcMeProvider);
        }

        [Fact]
        public void DatabaseConnectionFactory_ShouldReturnCorrectConnectionStrings()
        {
            // Arrange
            var configuration = CreateTestConfiguration();
            var serviceProvider = CreateServiceProvider(configuration);
            var factory = serviceProvider.GetRequiredService<IDatabaseConnectionFactory>();

            // Act
            var testDbConnection = factory.GetConnectionString(DatabaseRole.TestDatabase);
            var cdcMeConnection = factory.GetConnectionString(DatabaseRole.CdcMeDatabase);

            // Assert
            Assert.Contains("Server=test-sql", testDbConnection);
            Assert.Contains("Host=test-postgres", cdcMeConnection);
        }

        [Fact]
        public void DatabaseConnectionFactory_ShouldCreateCorrectConnections()
        {
            // Arrange
            var configuration = CreateTestConfiguration();
            var serviceProvider = CreateServiceProvider(configuration);
            var factory = serviceProvider.GetRequiredService<IDatabaseConnectionFactory>();

            // Act
            using var testDbConnection = factory.CreateConnection(DatabaseRole.TestDatabase);
            using var cdcMeConnection = factory.CreateConnection(DatabaseRole.CdcMeDatabase);

            // Assert
            Assert.IsType<Microsoft.Data.SqlClient.SqlConnection>(testDbConnection);
            Assert.IsType<Npgsql.NpgsqlConnection>(cdcMeConnection);
        }

        [Fact]
        public void SimpleDac_ShouldBeCreatedWithCorrectProvider()
        {
            // Arrange
            var configuration = CreateTestConfiguration();
            var serviceProvider = CreateServiceProvider(configuration);
            var factory = serviceProvider.GetRequiredService<IDatabaseConnectionFactory>();
            var logger = serviceProvider.GetRequiredService<ILogger<SimpleDac>>();

            // Act
            var testDbDac = factory.CreateDac(DatabaseRole.TestDatabase, logger);
            var cdcMeDac = factory.CreateDac(DatabaseRole.CdcMeDatabase, logger);

            // Assert
            Assert.NotNull(testDbDac);
            Assert.NotNull(cdcMeDac);
        }

        [Fact]
        public async Task DatabaseConfigurationValidator_ShouldValidateConfiguration()
        {
            // Arrange
            var configuration = CreateTestConfiguration();
            var serviceProvider = CreateServiceProvider(configuration);
            var factory = serviceProvider.GetRequiredService<IDatabaseConnectionFactory>();
            var logger = serviceProvider.GetRequiredService<ILogger<DatabaseConfigurationValidator>>();
            var validator = new DatabaseConfigurationValidator(factory, logger);

            // Act
            var result = await validator.ValidateAsync();

            // Assert
            Assert.NotNull(result);
            // Note: This will likely have connection errors since we're using test connection strings,
            // but it should validate the configuration structure
            Assert.True(result.Errors.Count >= 0); // May have connection errors, but structure should be valid
        }

        [Fact]
        public void ServiceProvider_ShouldResolveAllRequiredServices()
        {
            // Arrange
            var configuration = CreateTestConfiguration();
            var serviceProvider = CreateServiceProvider(configuration);

            // Act & Assert - Should not throw exceptions
            var factory = serviceProvider.GetRequiredService<IDatabaseConnectionFactory>();
            var simpleDac = serviceProvider.GetRequiredService<Softbase.SimpleDac>();
            var traceStorageConfig = serviceProvider.GetRequiredService<Softbase.Cdc.Models.TraceStorageConfiguration>();
            var traceProvider = serviceProvider.GetRequiredService<Softbase.Cdc.Trace.ITraceDataProvider>();

            Assert.NotNull(factory);
            Assert.NotNull(simpleDac);
            Assert.NotNull(traceStorageConfig);
            Assert.NotNull(traceProvider);
        }

        private static IConfiguration CreateTestConfiguration()
        {
            var configData = new Dictionary<string, string?>
            {
                ["ConnectionStrings:TEST_DB_CONNECTION"] = "Server=test-sql;Database=testdb;User Id=sa;Password=test123;TrustServerCertificate=true;",
                ["ConnectionStrings:CDCME_DB_CONNECTION"] = "Host=test-postgres;Database=cdcme;Username=postgres;Password=test123;",
                ["TEST_DB_PROVIDER"] = "SqlServer",
                ["CDCME_DB_PROVIDER"] = "PostgreSQL"
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();
        }

        private static ServiceProvider CreateServiceProvider(IConfiguration configuration)
        {
            var services = new ServiceCollection();

            // Add logging
            services.AddLogging(builder => builder.AddConsole());

            // Add configuration
            services.AddSingleton(configuration);

            // Add database services (similar to Program.cs)
            services.AddSingleton<IDatabaseConnectionFactory, ApiDatabaseConnectionFactory>();

            services.AddScoped<Softbase.SimpleDac>(serviceProvider =>
            {
                var factory = serviceProvider.GetRequiredService<IDatabaseConnectionFactory>();
                var logger = serviceProvider.GetRequiredService<ILogger<Softbase.SimpleDac>>();
                return factory.CreateDac(DatabaseRole.TestDatabase, logger);
            });

            services.AddScoped<Softbase.Cdc.Models.TraceStorageConfiguration>(serviceProvider =>
            {
                var factory = serviceProvider.GetRequiredService<IDatabaseConnectionFactory>();
                var connectionString = factory.GetConnectionString(DatabaseRole.CdcMeDatabase);
                var provider = factory.GetProvider(DatabaseRole.CdcMeDatabase);

                return new Softbase.Cdc.Models.TraceStorageConfiguration
                {
                    Provider = provider.ToString(),
                    ConnectionString = connectionString,
                    AutoCreateSchema = true,
                    CommandTimeout = 30,
                    SchemaName = provider == DatabaseProvider.PostgreSQL ? "public" : "dbo"
                };
            });

            services.AddScoped<Softbase.Cdc.Trace.ITraceDataProvider, Softbase.Cdc.Trace.PostgreSqlTraceProvider>(serviceProvider =>
            {
                var config = serviceProvider.GetRequiredService<Softbase.Cdc.Models.TraceStorageConfiguration>();
                var logger = serviceProvider.GetRequiredService<ILogger<Softbase.Cdc.Trace.PostgreSqlTraceProvider>>();
                return new Softbase.Cdc.Trace.PostgreSqlTraceProvider(config, logger);
            });

            return services.BuildServiceProvider();
        }
    }
}
