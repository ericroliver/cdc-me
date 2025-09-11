# CDC Web API Documentation

## Overview

The `cdc-api` project provides a RESTful Web API for CDC operations, built on ASP.NET Core. This API is designed to support containerized database testing workflows and provides HTTP endpoints for managing database reset operations as part of the CDC testing framework.

## Project Structure

```
cdc-api/
├── Controllers/
│   ├── CdcController.cs          # Main CDC operations controller
│   └── WeatherForecastController.cs  # Template controller (unused)
├── Properties/
│   └── launchSettings.json       # Development launch configuration
├── appsettings.json              # Application configuration
├── appsettings.Development.json  # Development-specific settings
├── Program.cs                    # Application entry point
└── cdc-api.csproj               # Project file
```

## API Configuration

### Application Setup

The API is configured as a minimal ASP.NET Core application with the following features:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
```

### Launch Configuration

**Development URLs:**

- HTTPS: `https://localhost:7297`
- HTTP: `http://localhost:5102`
- Swagger UI: Available at `/swagger` endpoint

**IIS Express:**

- HTTP: `http://localhost:34375`
- HTTPS: `https://localhost:44390` (SSL Port)

### Logging Configuration

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

## API Endpoints

### CDC Controller

**Base Route:** `/Cdc`

The main controller for CDC operations, currently focused on database container management.

#### POST /Cdc/resetDatabase

Resets the database by destroying and recreating the Docker container.

**Request:**

```http
POST /Cdc HTTP/1.1
Host: localhost:7297
Content-Type: application/json
```

**Response:**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  // CdcOperationResult object (currently empty)
}
```

**Implementation Details:**

```csharp
[HttpPost(Name = "resetDatabase")]
public CdcOperationResult ResetDatabase()
{
    // Planned implementation:
    // 1. docker container rm 'container_name'
    // 2. docker run --name mssqlDb_container-1 -i -d ghcr.io/yaitde-x/sb-sql-tpa:latest

    var command = "docker container rm 'container_name'";
    return new CdcOperationResult();
}
```

**Current Status:** 🚧 **Under Development**

- The endpoint structure is defined but implementation is incomplete
- Docker commands are planned but not yet executed
- Return type is defined but empty

## Data Models

### CdcOperationResult

Base result type for CDC operations.

```csharp
public class CdcOperationResult
{
    // Currently empty - planned for future expansion
}
```

**Planned Properties:**

- `bool Success` - Operation success status
- `string Message` - Operation result message
- `string[] Errors` - Any error messages
- `DateTime Timestamp` - Operation timestamp
- `object Data` - Operation-specific data

## Docker Integration

The API is designed to work with containerized SQL Server instances for testing scenarios.

### Planned Docker Workflow

1. **Container Removal:**

   ```bash
   docker container rm 'container_name'
   ```

2. **Container Recreation:**
   ```bash
   docker run --name mssqlDb_container-1 -i -d ghcr.io/yaitde-x/sb-sql-tpa:latest
   ```

### Container Image

**Base Image:** `ghcr.io/yaitde-x/sb-sql-tpa:latest`

- Custom SQL Server image with pre-configured test data
- Supports CDC functionality
- Designed for rapid reset/restore cycles

## Development Setup

### Prerequisites

- .NET 6.0 or later
- Docker Desktop (for container operations)
- SQL Server container image access

### Running the API

**Development Mode:**

```bash
cd cdc-api
dotnet run
```

**Production Build:**

```bash
dotnet build -c Release
dotnet run -c Release
```

**Docker Development:**

```bash
# If containerizing the API itself
docker build -t cdc-api .
docker run -p 5102:80 cdc-api
```

### Swagger Documentation

When running in development mode, Swagger UI is available at:

- `https://localhost:7297/swagger`
- `http://localhost:5102/swagger`

## Integration with CLI Tool

The Web API is designed to complement the CLI tool by providing:

1. **Remote Operations:** HTTP-based CDC operations for distributed scenarios
2. **Container Management:** Docker container lifecycle management
3. **Automation Support:** RESTful endpoints for CI/CD integration
4. **Monitoring:** Future health check and status endpoints

### Example Integration Workflow

```bash
# 1. Reset database via API
curl -X POST https://localhost:7297/Cdc

# 2. Initialize CDC via CLI
cdc-proto init

# 3. Run test scenarios
./run-test-scenarios.sh

# 4. Generate profile via CLI
cdc-proto profile -out test-profile.json

# 5. Reset database via API for next test
curl -X POST https://localhost:7297/Cdc
```

## Planned Enhancements

### Additional Endpoints

#### GET /Cdc/status

Check CDC and container status.

```csharp
[HttpGet("status")]
public CdcStatusResult GetStatus()
{
    return new CdcStatusResult
    {
        CdcEnabled = CheckCdcStatus(),
        ContainerRunning = CheckContainerStatus(),
        DatabaseConnectable = TestDatabaseConnection()
    };
}
```

#### POST /Cdc/init

Initialize CDC via API.

```csharp
[HttpPost("init")]
public CdcOperationResult InitializeCdc([FromBody] CdcInitRequest request)
{
    // Initialize CDC on specified database
}
```

#### POST /Cdc/profile

Generate profile via API.

```csharp
[HttpPost("profile")]
public CdcProfileResult GenerateProfile([FromBody] CdcProfileRequest request)
{
    // Generate and return profile data
}
```

#### POST /Cdc/diff

Compare profiles via API.

```csharp
[HttpPost("diff")]
public CdcDiffResult CompareProfiles([FromBody] CdcDiffRequest request)
{
    // Compare two profiles and return differences
}
```

### Enhanced Models

```csharp
public class CdcOperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string[] Errors { get; set; }
    public DateTime Timestamp { get; set; }
    public object Data { get; set; }
}

public class CdcStatusResult : CdcOperationResult
{
    public bool CdcEnabled { get; set; }
    public bool ContainerRunning { get; set; }
    public bool DatabaseConnectable { get; set; }
    public string ContainerName { get; set; }
    public string DatabaseName { get; set; }
}

public class CdcInitRequest
{
    public string ConnectionString { get; set; }
    public string[] TablesToInclude { get; set; }
    public string[] TablesToExclude { get; set; }
}

public class CdcProfileRequest
{
    public string ConnectionString { get; set; }
    public string OutputFormat { get; set; } = "json";
    public bool IncludeSystemFields { get; set; } = false;
}

public class CdcDiffRequest
{
    public object LeftProfile { get; set; }
    public object RightProfile { get; set; }
    public string[] FieldsToIgnore { get; set; }
    public bool IgnoreDateTimeFields { get; set; } = true;
}
```

## Security Considerations

### Current State

- **Authentication:** None (development only)
- **Authorization:** None (development only)
- **HTTPS:** Enabled in development
- **CORS:** Not configured

### Production Recommendations

#### Authentication & Authorization

```csharp
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = "https://your-identity-server";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CdcOperations", policy =>
        policy.RequireClaim("scope", "cdc.operations"));
});
```

#### CORS Configuration

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("CdcPolicy", policy =>
    {
        policy.WithOrigins("https://your-frontend-domain")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
```

#### Input Validation

```csharp
public class CdcInitRequest
{
    [Required]
    [StringLength(500)]
    public string ConnectionString { get; set; }

    [MaxLength(100)]
    public string[] TablesToInclude { get; set; }
}
```

## Error Handling

### Global Exception Handling

```csharp
public class GlobalExceptionMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = new CdcOperationResult
        {
            Success = false,
            Message = "An error occurred processing your request",
            Errors = new[] { exception.Message }
        };

        context.Response.StatusCode = 500;
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
```

## Testing

### Unit Testing

```csharp
[Test]
public async Task ResetDatabase_ShouldReturnSuccess()
{
    // Arrange
    var controller = new CdcController(_mockLogger.Object);

    // Act
    var result = controller.ResetDatabase();

    // Assert
    Assert.IsNotNull(result);
    Assert.IsInstanceOf<CdcOperationResult>(result);
}
```

### Integration Testing

```csharp
[Test]
public async Task ResetDatabase_Integration_ShouldResetContainer()
{
    // Arrange
    var client = _factory.CreateClient();

    // Act
    var response = await client.PostAsync("/Cdc", null);

    // Assert
    response.EnsureSuccessStatusCode();
    var content = await response.Content.ReadAsStringAsync();
    var result = JsonSerializer.Deserialize<CdcOperationResult>(content);
    Assert.IsNotNull(result);
}
```

## Deployment

### Development

```bash
dotnet run --environment Development
```

### Production

```bash
dotnet publish -c Release -o ./publish
cd publish
dotnet cdc-api.dll
```

### Docker Deployment

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["cdc-api/cdc-api.csproj", "cdc-api/"]
RUN dotnet restore "cdc-api/cdc-api.csproj"
COPY . .
WORKDIR "/src/cdc-api"
RUN dotnet build "cdc-api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "cdc-api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "cdc-api.dll"]
```

## Monitoring & Logging

### Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString)
    .AddDockerContainer("mssqlDb_container-1");
```

### Application Insights

```csharp
builder.Services.AddApplicationInsightsTelemetry();
```

### Structured Logging

```csharp
Log.Information("CDC operation {Operation} completed for container {ContainerName}",
    "ResetDatabase", containerName);
```

## Performance Considerations

- **Container Operations:** Docker commands can be slow; consider async operations
- **Database Connections:** Implement connection pooling for frequent operations
- **Memory Usage:** Profile operations may consume significant memory
- **Concurrent Requests:** Consider rate limiting for resource-intensive operations

## Future Integration Points

1. **Message Queues:** For long-running operations
2. **Event Sourcing:** For operation audit trails
3. **Caching:** For frequently accessed profile data
4. **Real-time Updates:** SignalR for operation status updates
5. **Batch Operations:** Support for multiple database operations
