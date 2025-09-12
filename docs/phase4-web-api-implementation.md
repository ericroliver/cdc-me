# Phase 4: Web API & Testing Implementation

## Overview

Phase 4 completes the CDC Testing Framework by implementing comprehensive Web API endpoints and integration tests. This phase provides REST API access to all trace functionality and includes automated testing to ensure system reliability.

## Implementation Summary

### 1. Web API Controllers

#### SnapshotController (`cdc-api/Controllers/SnapshotController.cs`)

- **POST /api/snapshot** - Create database snapshot
- **POST /api/snapshot/restore** - Restore database from snapshot
- **GET /api/snapshot/{databaseName}/snapshots** - List all snapshots
- **GET /api/snapshot/{databaseName}/snapshots/{snapshotName}** - Get snapshot info
- **DELETE /api/snapshot** - Delete snapshot

**Key Features:**

- Full async/await support
- Comprehensive error handling and logging
- Structured API request/response models
- Integration with existing SnapshotManager

#### TraceController (`cdc-api/Controllers/TraceController.cs`)

- **POST /api/trace/start** - Start trace session
- **POST /api/trace/stop** - Stop trace session
- **GET /api/trace/status/{sessionName}** - Get trace status
- **GET /api/trace/sessions** - List all trace sessions
- **POST /api/trace/export** - Export trace data
- **GET /api/trace/sessions/{sessionId}/events** - Get trace events
- **DELETE /api/trace/sessions/{sessionId}** - Delete trace session

**Key Features:**

- Extended Events integration
- Multi-database provider support
- Session lifecycle management
- Trace data export functionality

#### TestWorkflowController (`cdc-api/Controllers/TestWorkflowController.cs`)

- **POST /api/testworkflow/execute** - Execute complete test workflow
- **GET /api/testworkflow/status/{workflowId}** - Get workflow status
- **GET /api/testworkflow/executions** - List workflow executions

**Key Features:**

- 11-step automated workflow execution
- Comprehensive error handling and rollback
- Detailed step-by-step progress tracking
- Test report generation

### 2. API Configuration (`cdc-api/Program.cs`)

**Service Registration:**

- SnapshotManager, TraceManager, ReplayEngine, CdcComparator
- ITraceDataProvider with configurable PostgreSQL/SQL Server support
- Swagger/OpenAPI documentation with annotations
- CORS support for development

**Middleware Pipeline:**

- Swagger UI served at root for easy API exploration
- HTTPS redirection and authorization
- Controller routing and error handling

### 3. Integration Tests (`cdc-api.Tests/`)

#### Test Project Structure

- **cdc-api.Tests.csproj** - Test project configuration with xUnit, Moq, FluentAssertions
- **SnapshotControllerTests.cs** - Comprehensive snapshot API tests
- **TraceControllerTests.cs** - Complete trace API test coverage
- **TestWorkflowControllerTests.cs** - Workflow execution tests

**Testing Features:**

- WebApplicationFactory for integration testing
- Mock service dependencies for isolated testing
- HTTP client testing with JSON serialization
- Comprehensive status code and response validation

### 4. Solution Integration

Updated `cdc-me.sln` to include:

- cdc-api project for Web API
- cdc-api.Tests project for integration tests
- Proper build configurations for all platforms

## API Request/Response Models

### Snapshot Operations

```csharp
public class CreateSnapshotRequest
{
    public string DatabaseName { get; set; }
    public string SnapshotName { get; set; }
    public string ConnectionString { get; set; }
}

public class SnapshotApiResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string SnapshotName { get; set; }
    public DateTime? CreatedAt { get; set; }
}
```

### Trace Operations

```csharp
public class StartTraceRequest
{
    public string SessionName { get; set; }
    public string DatabaseName { get; set; }
    public string ConnectionString { get; set; }
    public int? MaxFileSize { get; set; }
    public List<string>? EventsToCapture { get; set; }
}

public class TraceApiResult
{
    public bool Success { get; set; }
    public Guid? SessionId { get; set; }
    public TraceStatus? Status { get; set; }
    public DateTime? StartedAt { get; set; }
}
```

### Workflow Operations

```csharp
public class WorkflowExecutionRequest
{
    public string WorkflowName { get; set; }
    public string DatabaseName { get; set; }
    public string ConnectionString { get; set; }
    public string TraceConnectionString { get; set; }
    public bool EnableCdc { get; set; }
    public List<string>? CdcTables { get; set; }
    public TraceConfiguration? TraceConfig { get; set; }
}

public class WorkflowExecutionResult
{
    public Guid WorkflowId { get; set; }
    public bool Success { get; set; }
    public List<WorkflowStepResult> Steps { get; set; }
    public ReplayResult? ReplayResult { get; set; }
    public List<ComparisonResult>? ComparisonResults { get; set; }
}
```

## Complete 11-Step Workflow

The TestWorkflowController implements the complete testing workflow:

1. **Create Baseline Snapshot** - Capture initial database state
2. **Enable CDC** - Configure Change Data Capture (optional)
3. **Start Trace Capture** - Begin Extended Events session
4. **Execute Baseline Workload** - Run initial SQL workload (optional)
5. **Stop Trace Capture** - End trace session
6. **Export Trace Data** - Save trace to trace database
7. **Create Test Snapshot** - Capture post-workload state
8. **Restore Baseline Snapshot** - Reset to initial state
9. **Replay Captured Statements** - Execute traced SQL statements
10. **Compare CDC Data** - Analyze data differences (optional)
11. **Generate Test Report** - Create comprehensive test summary

## Usage Examples

### Start a Trace Session

```bash
curl -X POST "https://localhost:7000/api/trace/start" \
  -H "Content-Type: application/json" \
  -d '{
    "sessionName": "MyTestTrace",
    "databaseName": "TestDB",
    "connectionString": "Server=blue.local;Database=TestDB;User Id=sa;Password=A123_Z321!;TrustServerCertificate=true",
    "maxFileSize": 100,
    "eventsToCapture": ["sql_statement_completed"]
  }'
```

### Execute Complete Workflow

```bash
curl -X POST "https://localhost:7000/api/testworkflow/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "workflowName": "Integration Test",
    "databaseName": "TestDB",
    "connectionString": "Server=blue.local;Database=TestDB;User Id=sa;Password=A123_Z321!;TrustServerCertificate=true",
    "traceConnectionString": "Host=blue.local;Database=cdc_tracedb;Username=postgres;Password=A123_Z321!",
    "baselineSnapshotName": "baseline_snap",
    "testSnapshotName": "test_snap",
    "traceSessionName": "workflow_trace",
    "enableCdc": true,
    "cdcTables": ["dbo.Orders", "dbo.Customers"]
  }'
```

## Testing and Validation

### Run Integration Tests

```bash
cd cdc-api.Tests
dotnet test
```

### Build and Run API

```bash
cd cdc-api
dotnet build
dotnet run
```

Access Swagger UI at: `https://localhost:7000`

## Configuration

### appsettings.json

```json
{
  "TraceProvider": "SqlServer",
  "ConnectionStrings": {
    "DefaultConnection": "Server=blue.local;Database=TestDB;User Id=sa;Password=A123_Z321!;TrustServerCertificate=true",
    "TraceConnection": "Host=blue.local;Database=cdc_tracedb;Username=postgres;Password=A123_Z321!"
  }
}
```

## Dependencies

### NuGet Packages Added

- **Swashbuckle.AspNetCore.Annotations** - Enhanced Swagger documentation
- **Npgsql** - PostgreSQL connectivity
- **System.Data.SqlClient** - SQL Server connectivity

### Test Dependencies

- **Microsoft.AspNetCore.Mvc.Testing** - Integration testing framework
- **xUnit** - Test framework
- **Moq** - Mocking framework
- **FluentAssertions** - Assertion library

## Security Considerations

- Connection strings should be stored in secure configuration
- API endpoints should implement authentication/authorization in production
- Input validation and sanitization implemented throughout
- Comprehensive error handling prevents information disclosure

## Performance Features

- Async/await throughout for non-blocking operations
- Configurable trace file sizes and retention
- Efficient database connection management
- Streaming support for large trace data exports

## Monitoring and Logging

- Structured logging with ILogger throughout
- Request/response logging for API operations
- Performance metrics for workflow execution
- Error tracking and alerting capabilities

## Next Steps

Phase 4 completes the core implementation. Future enhancements could include:

1. **Authentication & Authorization** - Secure API access
2. **Real-time Monitoring** - SignalR for live workflow updates
3. **Batch Processing** - Queue-based workflow execution
4. **Advanced Reporting** - Rich HTML/PDF test reports
5. **Performance Optimization** - Caching and connection pooling
6. **Cloud Integration** - Azure/AWS deployment support

## Conclusion

Phase 4 successfully delivers a complete Web API interface for the CDC Testing Framework, providing:

- ✅ RESTful API endpoints for all functionality
- ✅ Comprehensive integration test coverage
- ✅ Complete workflow automation
- ✅ Production-ready error handling and logging
- ✅ Swagger documentation for easy API exploration
- ✅ Configurable multi-database support

The framework now provides both CLI and Web API interfaces, making it suitable for both interactive use and programmatic integration into larger testing pipelines.
