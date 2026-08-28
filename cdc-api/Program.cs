using cdc_api.Data;
using cdc_api.HealthChecks;
using cdc_api.Middleware;
using DotNetEnv;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Softbase;
using Softbase.Cdc;
using Softbase.Cdc.Data;
using Softbase.Cdc.Factory;
using Softbase.Cdc.Factory.Engine;
using Softbase.Cdc.Factory.Executors;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Providers;
using Softbase.Cdc.Factory.Repositories;
using Softbase.Cdc.Models;
using Softbase.Cdc.Trace;

var builder = WebApplication.CreateBuilder(args);

// Load .env file if it exists - check multiple locations
// SECURITY: Only relative paths used to prevent hard-coding specific environments
var possibleEnvPaths = new[]
{
    Path.Combine(Directory.GetCurrentDirectory(), ".env"), // Current directory
    Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"), // Parent directory (when running from cdc-api)
    Path.Combine(AppContext.BaseDirectory, ".env"), // Application base directory
    Path.Combine(AppContext.BaseDirectory, "..", ".env") // Parent of application base directory
};

var envFileLoaded = false;
foreach (var envPath in possibleEnvPaths)
{
    if (File.Exists(envPath))
    {
        try
        {
            Env.Load(envPath);
            Console.WriteLine($"✓ Successfully loaded .env file");
            envFileLoaded = true;
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error loading .env file: {ex.Message}");
        }
    }
}

if (!envFileLoaded)
{
    Console.WriteLine("⚠ No .env file found. Relying on system environment variables.");
}

// Add environment variables to configuration (AFTER loading .env file)
builder.Configuration.AddEnvironmentVariables();

// Configure URLs to accept connections on any IP address when not specified
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls("http://0.0.0.0:5000", "https://0.0.0.0:5001");
}

// Add services to the container.

// Register Version Provider (Singleton — version doesn't change at runtime)
builder.Services.AddSingleton<IVersionProvider, VersionProvider>();

builder.Services.AddHealthChecks()
    .AddCheck<VersionHealthCheck>("version", tags: new[] { "version" });
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "CDC Testing API", Version = "v1" });
    c.EnableAnnotations();
    // Use fully qualified type names to avoid schema ID collisions
    // between types with the same class name in different namespaces
    c.CustomSchemaIds(type => type.FullName);
});

// Register Database Connection Factory
builder.Services.AddSingleton<IDatabaseConnectionFactory, ApiDatabaseConnectionFactory>();

// Register TEST_DB SimpleDac (for snapshots and Extended Events)
builder.Services.AddScoped<SimpleDac>(serviceProvider =>
{
    var factory = serviceProvider.GetRequiredService<IDatabaseConnectionFactory>();
    var logger = serviceProvider.GetRequiredService<ILogger<SimpleDac>>();
    return factory.CreateDac(DatabaseRole.TestDatabase, logger);
});

// Register Factory Schema Runner (DbUp migrations for Factory metadata tables)
builder.Services.AddSingleton<IFactorySchemaRunner>(serviceProvider =>
{
    var factory = serviceProvider.GetRequiredService<IDatabaseConnectionFactory>();
    var connectionString = factory.GetConnectionString(DatabaseRole.CdcMeDatabase);
    var logger = serviceProvider.GetRequiredService<ILogger<FactorySchemaRunner>>();
    return new FactorySchemaRunner(connectionString, logger);
});

// Register Factory Services

// Templates volume path — configurable via env var or appsettings
var templatesPath = Environment.GetEnvironmentVariable("FACTORY_TEMPLATES_PATH")
    ?? builder.Configuration["Factory:TemplatesPath"]
    ?? Path.Combine(AppContext.BaseDirectory, "templates");

// Ensure templates directory exists
if (!Directory.Exists(templatesPath))
{
    Directory.CreateDirectory(templatesPath);
}

// Register Factory services
builder.Services.AddScoped<ITemplateStorageProvider>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<LocalFileStorageProvider>>();
    return new LocalFileStorageProvider(templatesPath, logger);
});

builder.Services.AddScoped<IConnectionRegistry>(serviceProvider =>
{
    var factory = serviceProvider.GetRequiredService<IDatabaseConnectionFactory>();
    var connectionString = factory.GetConnectionString(DatabaseRole.CdcMeDatabase);
    var logger = serviceProvider.GetRequiredService<ILogger<ConnectionRegistry>>();
    return new ConnectionRegistry(connectionString, logger);
});

builder.Services.AddScoped<IDatabaseTemplateRepository>(serviceProvider =>
{
    var factory = serviceProvider.GetRequiredService<IDatabaseConnectionFactory>();
    var connectionString = factory.GetConnectionString(DatabaseRole.CdcMeDatabase);
    var storage = serviceProvider.GetRequiredService<ITemplateStorageProvider>();
    var logger = serviceProvider.GetRequiredService<ILogger<DatabaseTemplateRepository>>();
    return new DatabaseTemplateRepository(connectionString, storage, logger);
});

builder.Services.AddScoped<IScriptGroupRepository>(serviceProvider =>
{
    var factory = serviceProvider.GetRequiredService<IDatabaseConnectionFactory>();
    var connectionString = factory.GetConnectionString(DatabaseRole.CdcMeDatabase);
    var logger = serviceProvider.GetRequiredService<ILogger<ScriptGroupRepository>>();
    return new ScriptGroupRepository(connectionString, logger);
});

builder.Services.AddScoped<IScriptLibrary>(serviceProvider =>
{
    var factory = serviceProvider.GetRequiredService<IDatabaseConnectionFactory>();
    var connectionString = factory.GetConnectionString(DatabaseRole.CdcMeDatabase);
    var logger = serviceProvider.GetRequiredService<ILogger<ScriptLibrary>>();
    return new ScriptLibrary(connectionString, logger);
});

builder.Services.AddScoped<IDatabaseRegistry>(serviceProvider =>
{
    var factory = serviceProvider.GetRequiredService<IDatabaseConnectionFactory>();
    var connectionString = factory.GetConnectionString(DatabaseRole.CdcMeDatabase);
    var logger = serviceProvider.GetRequiredService<ILogger<DatabaseRegistry>>();
    return new DatabaseRegistry(connectionString, logger);
});

builder.Services.AddScoped<IDatabaseProvider, SqlServerDatabaseProvider>();

builder.Services.AddScoped<IScriptExecutor, SqlScriptExecutor>();

builder.Services.AddSingleton<ParameterResolver>();

builder.Services.AddScoped<IOrderRepository>(serviceProvider =>
{
    var factory = serviceProvider.GetRequiredService<IDatabaseConnectionFactory>();
    var connectionString = factory.GetConnectionString(DatabaseRole.CdcMeDatabase);
    var logger = serviceProvider.GetRequiredService<ILogger<OrderRepository>>();
    return new OrderRepository(connectionString, logger);
});

builder.Services.AddScoped<IDatabaseFactory, DatabaseFactory>();

// Register TraceStorageConfiguration for CDCME_DB
builder.Services.AddScoped<TraceStorageConfiguration>(serviceProvider =>
{
    var factory = serviceProvider.GetRequiredService<IDatabaseConnectionFactory>();
    var connectionString = factory.GetConnectionString(DatabaseRole.CdcMeDatabase);
    var provider = factory.GetProvider(DatabaseRole.CdcMeDatabase);

    return new TraceStorageConfiguration
    {
        Provider = provider.ToString(),
        ConnectionString = connectionString,
        AutoCreateSchema = true,
        CommandTimeout = 30,
        SchemaName = provider == DatabaseProvider.PostgreSQL ? "public" : "dbo"
    };
});

// Register ComparisonConfiguration
builder.Services.AddScoped<ComparisonConfiguration>(serviceProvider =>
{
    return new ComparisonConfiguration(); // Uses default values
});

// Register trace data provider (always PostgreSQL for CDCME_DB)
builder.Services.AddScoped<ITraceDataProvider, PostgreSqlTraceProvider>(serviceProvider =>
{
    var config = serviceProvider.GetRequiredService<TraceStorageConfiguration>();
    var logger = serviceProvider.GetRequiredService<ILogger<PostgreSqlTraceProvider>>();
    return new PostgreSqlTraceProvider(config, logger);
});

// Register trace services with proper database role separation
builder.Services.AddScoped<ISnapshotManager, SnapshotManager>(serviceProvider =>
{
    var factory = serviceProvider.GetRequiredService<IDatabaseConnectionFactory>();
    var logger = serviceProvider.GetRequiredService<ILogger<SnapshotManager>>();
    var testDbDac = factory.CreateDac(DatabaseRole.TestDatabase, logger);
    return new SnapshotManager(testDbDac, logger);
});

builder.Services.AddScoped<ITraceManager, TraceManager>(serviceProvider =>
{
    var factory = serviceProvider.GetRequiredService<IDatabaseConnectionFactory>();
    var traceProvider = serviceProvider.GetRequiredService<ITraceDataProvider>();
    var logger = serviceProvider.GetRequiredService<ILogger<TraceManager>>();
    var testDbDac = factory.CreateDac(DatabaseRole.TestDatabase, logger);
    return new TraceManager(testDbDac, traceProvider, logger);
});

builder.Services.AddScoped<IReplayEngine, ReplayEngine>(serviceProvider =>
{
    var factory = serviceProvider.GetRequiredService<IDatabaseConnectionFactory>();
    var traceProvider = serviceProvider.GetRequiredService<ITraceDataProvider>();
    var logger = serviceProvider.GetRequiredService<ILogger<ReplayEngine>>();
    var testDbDac = factory.CreateDac(DatabaseRole.TestDatabase, logger);
    return new ReplayEngine(testDbDac, traceProvider, logger);
});

builder.Services.AddScoped<ICdcComparator, CdcComparator>(serviceProvider =>
{
    var factory = serviceProvider.GetRequiredService<IDatabaseConnectionFactory>();
    var traceProvider = serviceProvider.GetRequiredService<ITraceDataProvider>();
    var logger = serviceProvider.GetRequiredService<ILogger<CdcComparator>>();
    var config = serviceProvider.GetRequiredService<ComparisonConfiguration>();
    var testDbDac = factory.CreateDac(DatabaseRole.TestDatabase, logger);
    return new CdcComparator(testDbDac, traceProvider, logger, config);
});

// Add CORS configuration
builder.Services.AddCors(options =>
{
    // Development policy - permissive for local development
    options.AddPolicy("Development", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });

    // Production policy - secure with specific origins
    // Configure via environment variable CORS_ALLOWED_ORIGINS (comma-separated)
    options.AddPolicy("Production", policy =>
    {
        var allowedOrigins = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS")
            ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? new[] { "http://localhost:3000", "http://localhost:8080" };

        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Register global exception handler — must be first in the pipeline
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// Run Factory schema migrations on startup
var schemaLogger = app.Services.GetRequiredService<ILogger<Program>>();
try
{
    var schemaRunner = app.Services.GetRequiredService<IFactorySchemaRunner>();
    if (schemaRunner.RunMigrations())
    {
        schemaLogger.LogInformation("Factory schema migrations completed successfully");
    }
    else
    {
        schemaLogger.LogWarning("Factory schema migrations did not complete successfully. Factory features may be unavailable.");
    }
}
catch (Exception ex)
{
    schemaLogger.LogWarning(ex, "Factory schema migrations could not be run. Factory features may be unavailable.");
}

// Log the exact URLs Kestrel is binding to for diagnostics
var urls = app.Urls;
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogDebug("Kestrel binding diagnostics");
logger.LogDebug("Environment: {Environment}", app.Environment.EnvironmentName);
logger.LogDebug("ASPNETCORE_URLS from env: {Urls}", Environment.GetEnvironmentVariable("ASPNETCORE_URLS"));
foreach (var url in urls)
{
    logger.LogDebug("Kestrel listening on: {Url}", url);
}

// Add request logging middleware for Development environment only
if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        var requestLogger = context.RequestServices.GetRequiredService<ILogger<Program>>();

        // Filter sensitive headers before logging
        var sensitiveHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization",
            "Cookie",
            "Set-Cookie",
            "X-API-Key",
            "X-Api-Key",
            "X-Auth-Token"
        };

        var safeHeaderStrings = context.Request.Headers.Select(h =>
            sensitiveHeaders.Contains(h.Key)
                ? $"{h.Key}=<redacted>"
                : $"{h.Key}={h.Value}");

        requestLogger.LogDebug("Incoming request diagnostics");
        requestLogger.LogDebug("Method: {Method}, Path: {Path}, RemoteIP: {RemoteIP}",
            context.Request.Method,
            context.Request.Path,
            context.Connection.RemoteIpAddress);
        requestLogger.LogDebug("Headers: {Headers}", string.Join(", ", safeHeaderStrings));

        try
        {
            await next();
            requestLogger.LogDebug("Response Status: {StatusCode}", context.Response.StatusCode);
        }
        catch (Exception ex)
        {
            requestLogger.LogError(ex, "Exception during request processing");
            throw;
        }
    });
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "CDC Testing API v1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at root
    });
    app.UseCors("Development");

    // Only use HTTPS redirection in Development when both HTTP and HTTPS are configured
    var aspnetcoreUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? string.Empty;
    if (aspnetcoreUrls.Contains("https", StringComparison.OrdinalIgnoreCase))
    {
        app.UseHttpsRedirection();
    }
}
else
{
    // Production environment
    app.UseCors("Production");
    // Don't use HTTPS redirection in Production/Docker - typically handled by reverse proxy/load balancer
}

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                data = e.Value.Data,
                description = e.Value.Description
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };
        await context.Response.WriteAsJsonAsync(response);
    }
});

// Test endpoint for development/diagnostics only
if (app.Environment.IsDevelopment())
{
    app.MapGet("/test", () => Results.Ok(new { message = "Test endpoint works", timestamp = DateTime.UtcNow.ToString("o") }));
}

app.Run();

// Make Program class accessible to test projects
public partial class Program { }
