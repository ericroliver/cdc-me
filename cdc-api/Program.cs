using Softbase.Cdc.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "CDC Testing API", Version = "v1" });
    c.EnableAnnotations();
});

// Register trace services
builder.Services.AddScoped<SnapshotManager>();
builder.Services.AddScoped<TraceManager>();
builder.Services.AddScoped<ReplayEngine>();
builder.Services.AddScoped<CdcComparator>();

// Register trace data provider based on configuration
var traceProvider = builder.Configuration.GetValue<string>("TraceProvider", "SqlServer");
if (traceProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<ITraceDataProvider, PostgreSqlTraceProvider>();
}
else
{
    builder.Services.AddScoped<ITraceDataProvider, SqlServerTraceProvider>();
}

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
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
    app.UseCors("AllowAll");
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Make Program class accessible to test projects
public partial class Program { }

