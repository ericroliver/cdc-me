using cdc_api.Data;
using DotNetEnv;
using Softbase;
using Softbase.Cdc.Data;
using Softbase.Cdc.Models;
using Softbase.Cdc.Trace;

var builder = WebApplication.CreateBuilder(args);

// Load .env file if it exists - check multiple locations
var possibleEnvPaths = new[]
{
    Path.Combine(Directory.GetCurrentDirectory(), ".env"), // Current directory
    Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"), // Parent directory (when running from cdc-api)
    Path.Combine(AppContext.BaseDirectory, ".env"), // Application base directory
    Path.Combine(AppContext.BaseDirectory, "..", ".env"), // Parent of application base directory
    "/Users/eo/code/cdc-me/.env" // Absolute path as fallback
};

var envFileLoaded = false;
foreach (var envPath in possibleEnvPaths)
{
    Console.WriteLine($"Checking for .env file at: {envPath}");
    if (File.Exists(envPath))
    {
        try
        {
            Env.Load(envPath);
            Console.WriteLine($"✓ Successfully loaded .env file from: {envPath}");

            // Verify the variables were loaded into environment
            var testDb = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION");
            var cdcmeDb = Environment.GetEnvironmentVariable("CDCME_DB_CONNECTION");
            Console.WriteLine($"TEST_DB_CONNECTION loaded: {!string.IsNullOrEmpty(testDb)}");
            Console.WriteLine($"CDCME_DB_CONNECTION loaded: {!string.IsNullOrEmpty(cdcmeDb)}");

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

    // Production policy - restricted to specific origins
    // SECURITY: Update these origins to match your actual deployment URLs
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",  // Example: React dev server
                "http://localhost:8080",  // Example: API itself
                "https://your-production-domain.com"  // CHANGE THIS to your actual domain
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

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
}
else
{
    // Production environment
    app.UseCors("Production");
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Make Program class accessible to test projects
public partial class Program { }

