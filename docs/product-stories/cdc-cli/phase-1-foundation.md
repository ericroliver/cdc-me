# Phase 1: Foundation - User Stories

## Overview

Phase 1 establishes the foundation for the cdc-cli project including project setup, core services, and infrastructure.

---

## Story 1.1: Create cdc-cli Project Structure

**As a** developer  
**I want** a new .NET console project with proper structure and dependencies  
**So that** I can build the CLI tool on a solid foundation

### Acceptance Criteria

- [ ] New `cdc-cli` directory created at repository root level
- [ ] .NET 8.0 console project created (`cdc-cli.csproj`)
- [ ] Project added to `cdc-me.sln` solution
- [ ] Required NuGet packages added:
  - System.CommandLine (>= 2.0.0)
  - Microsoft.Extensions.Http (>= 8.0.0)
  - Microsoft.Extensions.Logging (>= 8.0.0)
  - Microsoft.Extensions.Configuration (>= 8.0.0)
  - Microsoft.Extensions.Configuration.EnvironmentVariables (>= 8.0.0)
  - System.Text.Json (>= 8.0.0)
- [ ] Project reference to `cdc-lib` added
- [ ] Directory structure created:
  ```
  cdc-cli/
  ├── cdc-cli.csproj
  ├── Program.cs
  ├── Commands/
  ├── Services/
  ├── Models/
  └── Configuration/
  ```
- [ ] Basic `Program.cs` with entry point created
- [ ] Project builds successfully: `dotnet build cdc-cli/cdc-cli.csproj`
- [ ] Can run with `--help`: `dotnet run --project cdc-cli -- --help`

### Technical Notes

- Use .NET 8.0 target framework
- Enable nullable reference types
- Set `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- Follow existing project conventions from cdc-proto

### Definition of Done

- Project created and builds without errors
- All dependencies installed
- Added to solution file
- Basic help command works

---

## Story 1.2: Implement Configuration Management

**As a** user  
**I want** to configure the API base URL via parameter or environment variable  
**So that** I can point the CLI to different API instances

### Acceptance Criteria

- [ ] `CliConfiguration` class created in `Configuration/CliConfiguration.cs`
- [ ] Configuration properties defined:
  - `BaseUrl`: API base URL
  - `OutputFormat`: Output format (json, json-pretty, text)
  - `Verbose`: Enable verbose logging
  - `Quiet`: Suppress non-essential output
- [ ] Configuration loading order implemented:
  1. Command-line parameters (highest priority)
  2. Environment variable `CDC_API_URL`
  3. Default value: `http://localhost:5000`
- [ ] Configuration validation:
  - BaseUrl is valid URI format
  - BaseUrl uses http or https scheme
- [ ] Global options created for all commands:
  - `--base-url <url>`
  - `--output <format>` (json, json-pretty, text)
  - `--verbose`
  - `--quiet`
- [ ] Environment variable `CDC_API_URL` properly loaded
- [ ] Configuration properly injected via DI

### Test Cases

```bash
# Test default URL
dotnet run --project cdc-cli -- --help  # Should use http://localhost:5000

# Test CLI parameter
dotnet run --project cdc-cli -- --base-url http://localhost:8080 --help

# Test environment variable
export CDC_API_URL=http://api.example.com
dotnet run --project cdc-cli -- --help

# Test parameter overrides env var
export CDC_API_URL=http://api.example.com
dotnet run --project cdc-cli -- --base-url http://localhost:5000 --help
```

### Definition of Done

- Configuration class implemented
- Loading order works correctly
- Validation in place
- Global options available on all commands
- Tests verify priority order

---

## Story 1.3: Implement HTTP API Client Service

**As a** developer  
**I want** a reusable HTTP client service  
**So that** all commands can make API calls consistently

### Acceptance Criteria

- [ ] `ICdcApiClient` interface created in `Services/ICdcApiClient.cs`
- [ ] Interface methods defined:
  ```csharp
  Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest request, CancellationToken ct = default);
  Task<TResponse> GetAsync<TResponse>(string endpoint, CancellationToken ct = default);
  Task<TResponse> DeleteAsync<TResponse>(string endpoint, CancellationToken ct = default);
  ```
- [ ] `CdcApiClient` implementation created in `Services/CdcApiClient.cs`
- [ ] HTTP client properly configured:
  - Base address from configuration
  - Timeout: 5 minutes
  - Accept: application/json
  - Content-Type: application/json
- [ ] Request serialization using System.Text.Json:
  - CamelCase property naming
  - Ignore null values
- [ ] Response deserialization with error handling
- [ ] HTTP error handling:
  - 4xx errors: Throw with response body message
  - 5xx errors: Throw with server error message
  - Network errors: Throw with network error message
- [ ] Logging added for all requests/responses (when verbose)
- [ ] Service registered in DI container
- [ ] Proper disposal of HttpClient

### Technical Notes

- Use `IHttpClientFactory` for HttpClient creation
- Follow best practices for HttpClient lifetime management
- Log request/response only in verbose mode
- Don't log sensitive data (future: headers, tokens)

### Definition of Done

- Interface and implementation created
- All HTTP methods work correctly
- Error handling comprehensive
- Logging in place
- Registered in DI
- Unit tests written

---

## Story 1.4: Implement JSON Input/Output Handler

**As a** user  
**I want** to provide JSON input via file, stdin, or inline string  
**So that** I can use the CLI flexibly in different scenarios

### Acceptance Criteria

- [ ] `IJsonHandler` interface created in `Services/IJsonHandler.cs`
- [ ] Interface methods defined:
  ```csharp
  Task<T?> ReadInputAsync<T>(string? dataString, string? filePath, bool allowStdin = true);
  Task WriteOutputAsync<T>(T data, OutputFormat format = OutputFormat.Json);
  Task WriteErrorAsync(string message, int exitCode);
  ```
- [ ] `JsonHandler` implementation created in `Services/JsonHandler.cs`
- [ ] Input reading priority implemented:
  1. `dataString` (inline JSON with `--data`)
  2. `filePath` (file with `--file`)
  3. stdin (if `allowStdin` is true and data available)
- [ ] File reading:
  - Validate file exists
  - Read entire file content
  - Parse as JSON
- [ ] Stdin reading:
  - Check if stdin has data
  - Read until EOF
  - Parse as JSON
- [ ] Output writing:
  - `OutputFormat.Json`: Compact JSON to stdout
  - `OutputFormat.JsonPretty`: Indented JSON to stdout
  - `OutputFormat.Text`: Human-readable summary to stdout
- [ ] Error writing:
  - Write to stderr
  - Set exit code properly
  - Format consistently
- [ ] JSON parsing error handling with helpful messages
- [ ] Service registered in DI

### Test Cases

```bash
# Test inline JSON
dotnet run -- command --data '{"key":"value"}'

# Test file input
echo '{"key":"value"}' > test.json
dotnet run -- command --file test.json

# Test stdin
echo '{"key":"value"}' | dotnet run -- command

# Test output formats
dotnet run -- command --output json
dotnet run -- command --output json-pretty
dotnet run -- command --output text
```

### Definition of Done

- Interface and implementation created
- All input methods work
- All output formats work
- Error handling comprehensive
- Proper exit codes
- Unit tests for all scenarios

---

## Story 1.5: Create Base Command Class

**As a** developer  
**I want** a base class for all commands  
**So that** I can reuse common functionality

### Acceptance Criteria

- [ ] `ApiCommandBase` abstract class created in `Commands/ApiCommandBase.cs`
- [ ] Base class constructor accepts:
  - Command name
  - Command description
  - `ICdcApiClient`
  - `IJsonHandler`
  - `ILogger`
- [ ] Common option factory methods:
  ```csharp
  protected Option<string> CreateDataOption();
  protected Option<string> CreateFileOption();
  protected Option<string> CreateSessionOption();
  ```
- [ ] Helper methods for common operations:
  ```csharp
  protected async Task<TRequest?> GetRequestAsync<TRequest>(string? data, string? file);
  protected async Task<TResponse> ExecuteApiCallAsync<TRequest, TResponse>(string endpoint, TRequest request);
  protected async Task HandleErrorAsync(Exception ex);
  ```
- [ ] Error handling patterns:
  - `HttpRequestException`: API errors (exit code 1)
  - `IOException`: File errors (exit code 2)
  - `JsonException`: JSON parsing errors (exit code 3)
  - Other exceptions: Unexpected errors (exit code 1)
- [ ] Logging support (verbose mode)
- [ ] Global options properly inherited by all commands

### Technical Notes

- Inherit from `System.CommandLine.Command`
- Use protected members for derived classes
- Follow SOLID principles
- Keep base class focused on common functionality

### Definition of Done

- Base class created
- Common functionality extracted
- Error handling patterns established
- Well documented with XML comments
- Ready for command implementations

---

## Story 1.6: Setup Dependency Injection and Program Entry Point

**As a** developer  
**I want** proper DI configuration  
**So that** all services are available to commands

### Acceptance Criteria

- [ ] `Program.cs` implements service registration:
  - Configuration services
  - Logging services (Console, Debug)
  - HttpClient factory
  - `ICdcApiClient` as singleton
  - `IJsonHandler` as singleton
  - All commands as singletons
- [ ] Command-line parser configured:
  - Root command created
  - All command groups added
  - Global options configured
  - Help text configured
- [ ] Environment variable loading configured
- [ ] Logging configuration:
  - Console logger for errors
  - Debug logger for verbose mode
  - Log level based on `--verbose` flag
- [ ] Parser uses DI to resolve commands
- [ ] Exit code handling:
  - 0: Success
  - 1: API/request errors
  - 2: File I/O errors
  - 3: Validation errors
- [ ] Clean error messages for unhandled exceptions

### Program Flow

```
1. Load configuration (env vars + defaults)
2. Setup DI container
3. Register all services
4. Register all commands
5. Build command-line parser
6. Parse arguments
7. Execute command
8. Return exit code
```

### Definition of Done

- Program.cs fully implemented
- DI properly configured
- All services registered
- Command parser working
- Help command functional
- Exit codes correct

---

## Story 1.7: Create API Model Classes

**As a** developer  
**I want** strongly-typed models for API requests/responses  
**So that** I have type safety and IntelliSense

### Acceptance Criteria

- [ ] Decision made: Create `cdc-models` library OR link files from cdc-api
- [ ] If new library approach:
  - [ ] New `cdc-models` project created
  - [ ] Added to solution
  - [ ] Referenced by both `cdc-api` and `cdc-cli`
  - [ ] Models moved from `cdc-api/Models` to `cdc-models`
  - [ ] `cdc-api` references `cdc-models`
- [ ] If file linking approach:
  - [ ] Create symbolic links or shared project files
  - [ ] Ensure models accessible in cdc-cli
- [ ] All request models defined:
  - CDC: `StartCdcRequest`, `StopCdcRequest`, `CaptureCdcRequest`
  - Snapshot: `CreateSnapshotRequest`, `RestoreSnapshotRequest`
  - Trace: `StartTraceRequest`, `StopTraceRequest`, `ExportTraceRequest`
  - Workflow: `WorkflowExecutionRequest`
- [ ] All response models defined:
  - CDC: `StartCdcResponse`, `StopCdcResponse`, `CaptureCdcResponse`
  - Snapshot: `SnapshotApiResult`, `SnapshotInfo`
  - Trace: `TraceApiResult`, `TraceSessionStatus`, `TraceSessionSummary`
  - Workflow: `WorkflowExecutionResult`, `WorkflowStatus`
- [ ] Models have proper validation attributes
- [ ] Models have XML documentation
- [ ] Models support JSON serialization

### Technical Decision

**Recommendation**: Create new `cdc-models` library because:
- Clearer separation of concerns
- Better for versioning
- Avoids file linking complexity
- Easier to test independently

### Definition of Done

- Models accessible in cdc-cli
- No code duplication
- Type safety in place
- All models documented
- Solution builds successfully

---

## Story 1.8: Create Unit Test Project

**As a** developer  
**I want** a test project with proper setup  
**So that** I can write tests for all components

### Acceptance Criteria

- [ ] Test project created: `cdc-cli.Tests/cdc-cli.Tests.csproj`
- [ ] Using XUnit test framework
- [ ] Test packages installed:
  - xUnit
  - xUnit.runner.visualstudio
  - Microsoft.NET.Test.Sdk
  - Moq (for mocking)
  - FluentAssertions (for assertions)
- [ ] Test project references `cdc-cli`
- [ ] Test project added to solution
- [ ] Directory structure created:
  ```
  cdc-cli.Tests/
  ├── cdc-cli.Tests.csproj
  ├── Services/
  │   ├── CdcApiClientTests.cs
  │   └── JsonHandlerTests.cs
  ├── Commands/
  └── Integration/
  ```
- [ ] Test helper classes created:
  - Mock HTTP message handler
  - Test data builders
- [ ] Tests run with `dotnet test cdc-cli.Tests/cdc-cli.Tests.csproj`
- [ ] Sample tests for configuration and services

### Test Coverage Goals

- Unit tests: >80% coverage
- All services tested
- All commands tested
- Error scenarios covered

### Definition of Done

- Test project created and builds
- Can run tests successfully
- Basic test infrastructure in place
- Ready for test-driven development

---

## Story 1.9: Document Foundation Components

**As a** developer  
**I want** documentation for the foundation components  
**So that** team members can understand the architecture

### Acceptance Criteria

- [ ] README.md created in `cdc-cli/` directory with:
  - Project purpose
  - Getting started guide
  - Build instructions
  - Basic usage examples
  - Development setup
- [ ] XML documentation comments on all public classes/methods
- [ ] Architecture decision records (if needed) documented
- [ ] Code examples for:
  - Adding new commands
  - Using services
  - Writing tests
- [ ] Contributing guidelines

### Documentation Structure

```
cdc-cli/
├── README.md           # Main project readme
└── docs/
    ├── development.md  # Development guide
    ├── testing.md      # Testing guide
    └── architecture.md # Architecture notes
```

### Definition of Done

- README.md comprehensive
- All code documented
- Examples provided
- Documentation reviewed

---

## Phase 1 Completion Criteria

**Phase 1 is complete when:**

✅ Project structure established  
✅ All foundation services implemented  
✅ Configuration management working  
✅ HTTP client functional  
✅ JSON I/O handler complete  
✅ Base command class ready  
✅ DI properly configured  
✅ Models accessible  
✅ Test infrastructure in place  
✅ Basic documentation complete  
✅ Can run `cdc-cli --help` successfully  
✅ All tests pass  
✅ Solution builds without warnings

**Next Phase**: Phase 2 - CDC Commands Implementation
