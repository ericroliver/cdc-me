using cdc_api.Data;
using DotNetEnv;
using Softbase;
using Softbase.Cdc.Data;
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

builder.Services.AddHealthChecks();
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "CDC Testing API", Version = "v1" });
    c.EnableAnnotations();
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

    // Production policy - permissive for Docker/API testing
    // SECURITY: In actual production deployment, restrict this to specific origins
    options.AddPolicy("Production", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Log the exact URLs Kestrel is binding to for diagnostics
var urls = app.Urls;
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("=== KESTREL BINDING DIAGNOSTICS ===");
logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
logger.LogInformation("ASPNETCORE_URLS from env: {Urls}", Environment.GetEnvironmentVariable("ASPNETCORE_URLS"));
foreach (var url in urls)
{
    logger.LogInformation("Kestrel listening on: {Url}", url);
}

// Add request logging middleware to diagnose connection issues
app.Use(async (context, next) =>
{
    var requestLogger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    requestLogger.LogInformation("=== INCOMING REQUEST ===");
    requestLogger.LogInformation("Method: {Method}, Path: {Path}, RemoteIP: {RemoteIP}",
        context.Request.Method,
        context.Request.Path,
        context.Connection.RemoteIpAddress);
    requestLogger.LogInformation("Headers: {Headers}",
        string.Join(", ", context.Request.Headers.Select(h => $"{h.Key}={h.Value}")));

    try
    {
        await next();
        requestLogger.LogInformation("Response Status: {StatusCode}", context.Response.StatusCode);
    }
    catch (Exception ex)
    {
        requestLogger.LogError(ex, "Exception during request processing");
        throw;
    }
});

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
app.MapHealthChecks("/health");

// Add a simple test endpoint to verify routing
app.MapGet("/test", () => Results.Ok(new { message = "Test endpoint works", timestamp = DateTime.UtcNow }));

app.Run();

// Make Program class accessible to test projects
public partial class Program { }
