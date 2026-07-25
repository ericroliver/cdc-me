# CDC CLI - Command-Line HTTP API Client

## Overview

`cdc-cli` is a command-line interface tool for interacting with the CDC Testing Framework REST API. It provides a convenient way to manage CDC (Change Data Capture) operations, snapshots, traces, and test workflows from the command line.

This tool is separate from the existing `cdc-proto` tool, which performs direct database operations. Instead, `cdc-cli` communicates with the CDC REST API (`cdc-api`) over HTTP, making it suitable for remote operations and integration into CI/CD pipelines.

## Features

- **HTTP API Client**: Communicates with CDC REST API endpoints
- **Flexible Input**: Accept JSON via inline string, file, or stdin
- **Multiple Output Formats**: JSON (compact), JSON (pretty-printed), or human-readable text
- **Configuration Management**: Configure via CLI parameters or environment variables
- **Comprehensive Error Handling**: Clear error messages with appropriate exit codes
- **Verbose Logging**: Optional detailed logging for debugging

## Building

Build the project using the .NET CLI:

```bash
# Build the project
dotnet build cdc-cli/cdc-cli.csproj

# Build in release mode
dotnet build cdc-cli/cdc-cli.csproj -c Release

# Build the entire solution
dotnet build cdc-me.sln
```

## Running

Run the CLI tool using the .NET CLI:

```bash
# Show help
dotnet run --project cdc-cli -- --help

# Run a specific command
dotnet run --project cdc-cli -- [command] [options]
```

Or build and run the executable directly:

```bash
# Build release version
dotnet build cdc-cli/cdc-cli.csproj -c Release

# Run the executable
./cdc-cli/bin/Release/net10.0/cdc-cli --help
```

## Configuration

### API Base URL

The CLI needs to know where the CDC API is hosted. Configure this using one of these methods (in priority order):

1. **Command-line parameter** (highest priority):
   ```bash
   cdc-cli --base-url http://api.example.com command
   ```

2. **Environment variable**:
   ```bash
   export CDC_API_URL=http://api.example.com
   cdc-cli command
   ```

3. **Default**: `http://localhost:5000` (if not specified)

### Global Options

Available on all commands:

- `--base-url, -u <url>`: API base URL (overrides CDC_API_URL environment variable)
- `--output, -o <format>`: Output format - `json`, `json-pretty`, or `text` (default: `json`)
- `--verbose, -v`: Enable verbose logging for debugging
- `--quiet, -q`: Suppress non-essential output

### Exit Codes

The CLI uses standard exit codes:

- `0`: Success
- `1`: API or request error
- `2`: File I/O error
- `3`: Validation error

## Architecture

### Project Structure

```
cdc-cli/
├── Commands/           # Command implementations
│   └── ApiCommandBase.cs
├── Configuration/      # Configuration management
│   └── CliConfiguration.cs
├── Services/          # Core services
│   ├── ICdcApiClient.cs
│   ├── CdcApiClient.cs
│   ├── IJsonHandler.cs
│   └── JsonHandler.cs
├── Models/            # (Reserved for future use)
├── Program.cs         # Entry point and DI setup
└── cdc-cli.csproj    # Project file
```

### Key Components

#### 1. Configuration Management (`CliConfiguration`)

Handles configuration from multiple sources:
- Environment variables
- Command-line parameters
- Default values

Validates URLs and settings before use.

#### 2. HTTP API Client (`CdcApiClient`)

Provides a strongly-typed interface for HTTP operations:
- POST, GET, DELETE methods
- JSON serialization/deserialization
- Timeout and error handling
- Uses `IHttpClientFactory` for proper HTTP client lifecycle management

#### 3. JSON Handler (`JsonHandler`)

Manages input/output operations:
- Reads JSON from inline string, file, or stdin (in priority order)
- Writes output in configured format (json, json-pretty, text)
- Writes errors to stderr with appropriate exit codes

#### 4. Base Command Class (`ApiCommandBase`)

Provides common functionality for all commands:
- Standard options (data, file, session)
- Error handling patterns
- Response formatting
- Exit code management

### Dependency Injection

The application uses Microsoft.Extensions.DependencyInjection:

```
Services:
- CliConfiguration (Singleton)
- ILogger<T> (via logging provider)
- IHttpClientFactory -> ICdcApiClient (Scoped per request)
- IJsonHandler (Singleton)
```

## Development

### Adding a New Command

1. Create a new class inheriting from `ApiCommandBase`:

```csharp
public class MyCommand : ApiCommandBase
{
    public MyCommand(
        ICdcApiClient apiClient,
        IJsonHandler jsonHandler,
        ILogger<MyCommand> logger,
        CliConfiguration configuration)
        : base("my-command", "Description", apiClient, jsonHandler, logger, configuration)
    {
        // Add command-specific options
        var myOption = new Option<string>("--option", "Description");
        AddOption(myOption);

        // Set command handler
        this.SetHandler(async (optionValue) =>
        {
            // Command implementation
            await ExecuteAsync(optionValue);
        }, myOption);
    }

    private async Task ExecuteAsync(string optionValue)
    {
        // Use base class helper methods
        var response = await ExecuteApiCallAsync<MyRequest, MyResponse>(
            "api/endpoint",
            new MyRequest { Value = optionValue });

        if (response != null)
        {
            await WriteResponseAsync(response);
            SetSuccessExitCode();
        }
    }
}
```

2. Register the command in `Program.cs`:

```csharp
services.AddSingleton<MyCommand>();
// ...
rootCommand.AddCommand(serviceProvider.GetRequiredService<MyCommand>());
```

### Writing Tests

Tests use XUnit, Moq, and FluentAssertions:

```csharp
public class MyCommandTests
{
    [Fact]
    public async Task MyTest()
    {
        // Arrange
        var mockClient = new Mock<ICdcApiClient>();
        // ... setup mocks

        // Act
        var result = await command.ExecuteAsync();

        // Assert
        result.Should().BeTrue();
    }
}
```

### Running Tests

```bash
# Run all tests
dotnet test cdc-cli.Tests/cdc-cli.Tests.csproj

# Run with coverage
dotnet test cdc-cli.Tests/cdc-cli.Tests.csproj --collect:"XPlat Code Coverage"

# Run specific test
dotnet test --filter "FullyQualifiedName~MySpecificTest"
```

## Dependencies

### Runtime Dependencies

- .NET 10.0
- System.CommandLine (>= 2.0.0-beta4) - Command-line parsing
- Microsoft.Extensions.Http (>= 10.0.0) - HTTP client factory
- Microsoft.Extensions.Logging (>= 10.0.0) - Logging infrastructure
- Microsoft.Extensions.Configuration (>= 10.0.0) - Configuration management
- Microsoft.Extensions.DependencyInjection (>= 10.0.0) - DI container
- System.Text.Json - JSON serialization (included in .NET 10 shared framework)

### Development Dependencies

- XUnit - Test framework
- Moq - Mocking library
- FluentAssertions - Assertion library

## Related Projects

- **cdc-api**: REST API that this CLI communicates with
- **cdc-models**: Shared model classes used by both cdc-api and cdc-cli
- **cdc-proto**: Direct database tool for CDC operations (different approach)
- **cdc-lib**: Core library with CDC utilities and database operations

## License

See LICENSE file in the repository root.
