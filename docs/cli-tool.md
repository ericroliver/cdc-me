# CDC Command Line Tool Documentation

## Overview

The `cdc-proto` project provides a command-line interface for CDC operations. Built using the modern `System.CommandLine` library, it offers a clean, extensible command structure for managing Change Data Capture workflows.

## Installation & Setup

### Prerequisites

- .NET 6.0 or later
- SQL Server with CDC capabilities
- Appropriate database permissions (db_owner or CDC-specific permissions)

### Building the Tool

```bash
cd cdc-proto
dotnet build
dotnet run -- --help
```

### Creating an Executable

```bash
dotnet publish -c Release -r win-x64 --self-contained
# Creates executable in bin/Release/net6.0/win-x64/publish/
```

## Command Structure

The CLI tool uses a modern command structure with the following pattern:

```
cdc-proto <command> [options]
```

### Available Commands

#### `init` - Initialize CDC

Enables Change Data Capture on a database and all eligible tables.

**Syntax:**

```bash
cdc-proto init
```

**What it does:**

1. Enables CDC at the database level (`sys.sp_cdc_enable_db`)
2. Discovers all user tables (excluding CDC system tables)
3. Enables CDC on tables that have primary keys
4. Uses primary key as the tracking index for CDC

**Example:**

```bash
cdc-proto init
```

**Output:**

```
[Debug] init command
[Debug] enabling cdc for dbo.Customers, index: PK_Customers : EXEC sys.sp_cdc_enable_table @source_schema = 'dbo',@source_name = 'Customers',@role_name = null,@supports_net_changes =1,@index_name ='PK_Customers';
[Debug] enabling cdc for dbo.Orders, index: PK_Orders : EXEC sys.sp_cdc_enable_table @source_schema = 'dbo',@source_name = 'Orders',@role_name = null,@supports_net_changes =1,@index_name ='PK_Orders';
```

#### `profile` - Generate Data Profile

Creates a JSON profile of all CDC changes captured since CDC was enabled.

**Syntax:**

```bash
cdc-proto profile -out <output-file>
```

**Parameters:**

- `-out` (required): Path to write the profile JSON file

**What it does:**

1. Queries all CDC tables for net changes
2. Uses `cdc.fn_cdc_get_net_changes_*` functions
3. Aggregates changes by table
4. Exports to JSON format

**Example:**

```bash
cdc-proto profile -out baseline-profile.json
```

**Sample Output File Structure:**

```json
{
  "dbo_Customers": [
    {
      "__$start_lsn": "0x00000020000000D0000A",
      "__$operation": 2,
      "CustomerID": 1,
      "CustomerName": "Acme Corp",
      "LastModified": "2024-01-15T10:30:00"
    }
  ],
  "dbo_Orders": [
    {
      "__$start_lsn": "0x00000020000000D0000B",
      "__$operation": 1,
      "OrderID": 100,
      "CustomerID": 1,
      "OrderDate": "2024-01-15T10:35:00"
    }
  ]
}
```

#### `diff` - Compare Profiles

Compares two profile files and generates a difference report.

**Syntax:**

```bash
cdc-proto diff -left <left-profile> -right <right-profile> -out <diff-file>
```

**Parameters:**

- `-left` (required): Path to the first (baseline) profile
- `-right` (required): Path to the second (comparison) profile
- `-out` (required): Path to write the difference report

**What it does:**

1. Loads both profile JSON files
2. Compares records using primary key indexing
3. Identifies New, Changed, and Deleted records
4. Generates detailed field-level change analysis
5. Exports comprehensive difference report

**Example:**

```bash
cdc-proto diff -left baseline.json -right optimized.json -out differences.json
```

**Sample Difference Output:**

```json
{
  "dbo_Customers": {
    "table": {
      "Catalog": "TestDB",
      "Schema": "dbo",
      "Name": "Customers"
    },
    "index": {
      "IndexName": "PK_Customers",
      "IndexType": "clustered, unique, primary key",
      "IndexKeys": "CustomerID"
    },
    "diff": [
      {
        "Action": 2,
        "Key": "1",
        "Left": {
          "CustomerID": 1,
          "CustomerName": "Acme Corp",
          "LastModified": "2024-01-15T10:30:00"
        },
        "Right": {
          "CustomerID": 1,
          "CustomerName": "Acme Corporation",
          "LastModified": "2024-01-15T11:30:00"
        },
        "Changes": {
          "CustomerName": {
            "Action": 2,
            "Left": "Acme Corp",
            "Right": "Acme Corporation"
          }
        }
      }
    ]
  }
}
```

#### `teardown` - Cleanup CDC

Disables Change Data Capture on the database.

**Syntax:**

```bash
cdc-proto teardown
```

**What it does:**

1. Disables CDC at the database level (`sys.sp_cdc_disable_db`)
2. Removes all CDC tables and functions
3. Cleans up CDC metadata

**Example:**

```bash
cdc-proto teardown
```

## Configuration

### Connection String

The connection string is currently hardcoded in the application. To modify it, update the `BuildServiceProvider` method in `Program.cs`:

```csharp
var connectionString = "Server=192.168.1.76,5433;Database=sbcrm;User Id=sa;Password=A123_Z321!;";
```

**Future Enhancement:** Move to configuration file or command-line parameter.

### Logging Configuration

The tool uses Microsoft.Extensions.Logging with console and debug output:

```csharp
services.AddLogging(c => c.AddConsole().AddDebug());
```

**Log Levels:**

- `Debug`: Detailed operation information
- `Error`: Error conditions and exceptions
- `Warning`: Non-fatal issues

## Command Implementation Details

### Command Registration

Commands are automatically discovered and registered using reflection:

```csharp
public static IServiceCollection AddCliCommands(this IServiceCollection services)
{
    Type commandType = typeof(InitCommand);
    Type baseCommandType = typeof(Command);

    IEnumerable<Type> commands = commandType
        .Assembly
        .GetExportedTypes()
        .Where(x => x.Namespace == commandType.Namespace && baseCommandType.IsAssignableFrom(x));

    foreach (Type command in commands)
    {
        services.AddSingleton(baseCommandType, command);
    }

    return services;
}
```

### Command Base Structure

All commands inherit from `System.CommandLine.Command`:

```csharp
public class InitCommand : Command
{
    private readonly SimpleDac _dac;
    private readonly ILogger _logger;

    public InitCommand(SimpleDac dac, ILoggerFactory factory)
       : base("init", "initialize a database with cdc")
    {
        _dac = dac;
        _logger = factory.CreateLogger<InitCommand>();
        this.Handler = CommandHandler.Create(() => this.HandleCommand());
    }

    private int HandleCommand()
    {
        // Command implementation
        return 1; // Success
    }
}
```

### Error Handling

Commands implement consistent error handling:

```csharp
private int HandleCommand()
{
    try
    {
        // Command logic
        return 1; // Success
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "error message");
        return 0; // Failure
    }
}
```

## Usage Workflows

### Basic Testing Workflow

```bash
# 1. Initialize CDC on database
cdc-proto init

# 2. Run your test scenario (external to this tool)
# ... execute your application workflows ...

# 3. Capture baseline profile
cdc-proto profile -out baseline.json

# 4. Reset database state (external to this tool)
# ... restore database snapshot ...

# 5. Re-initialize CDC
cdc-proto init

# 6. Run optimized scenario (external to this tool)
# ... execute optimized workflows ...

# 7. Capture comparison profile
cdc-proto profile -out optimized.json

# 8. Compare profiles
cdc-proto diff -left baseline.json -right optimized.json -out comparison.json

# 9. Clean up
cdc-proto teardown
```

### Automated Testing Integration

```bash
#!/bin/bash
# test-script.sh

# Setup
cdc-proto init

# Run test scenario
./run-test-scenario.sh

# Capture profile
cdc-proto profile -out "profile-$(date +%Y%m%d-%H%M%S).json"

# Cleanup
cdc-proto teardown
```

### Continuous Integration Usage

```yaml
# Azure DevOps Pipeline example
- task: DotNetCoreCLI@2
  displayName: "Initialize CDC"
  inputs:
    command: "run"
    projects: "cdc-proto/cdc-proto.csproj"
    arguments: "init"

- task: PowerShell@2
  displayName: "Run Test Scenarios"
  inputs:
    filePath: "scripts/run-scenarios.ps1"

- task: DotNetCoreCLI@2
  displayName: "Generate Profile"
  inputs:
    command: "run"
    projects: "cdc-proto/cdc-proto.csproj"
    arguments: "profile -out $(Build.ArtifactStagingDirectory)/profile.json"
```

## Troubleshooting

### Common Issues

#### "CDC is not enabled on database"

**Error:** CDC operations fail because CDC is not enabled.
**Solution:** Run `cdc-proto init` first.

#### "Table does not have a primary key"

**Error:** CDC enablement skips tables without primary keys.
**Solution:** Add primary keys to tables or modify the code to handle alternate unique indexes.

#### "Insufficient permissions"

**Error:** Database user lacks CDC permissions.
**Solution:** Grant `db_owner` role or specific CDC permissions:

```sql
GRANT SELECT ON SCHEMA::cdc TO [username];
EXEC sp_addrolemember 'db_ddladmin', 'username';
```

#### "Connection timeout"

**Error:** Database operations timeout.
**Solution:** Increase timeout in `SimpleDac` or optimize queries.

### Debug Mode

Enable detailed logging by setting the log level:

```csharp
services.AddLogging(c => c.AddConsole().SetMinimumLevel(LogLevel.Debug));
```

### Profile File Issues

- **Large Files:** Profiles can be large for databases with many changes
- **JSON Format:** Ensure proper JSON formatting for diff operations
- **File Permissions:** Verify write permissions for output directories

## Extending the CLI

### Adding New Commands

1. Create a new class inheriting from `Command`
2. Place it in the `Commands` namespace
3. Implement the command logic
4. The command will be automatically discovered and registered

**Example:**

```csharp
public class StatusCommand : Command
{
    private readonly SimpleDac _dac;
    private readonly ILogger _logger;

    public StatusCommand(SimpleDac dac, ILoggerFactory factory)
       : base("status", "show CDC status")
    {
        _dac = dac;
        _logger = factory.CreateLogger<StatusCommand>();
        this.Handler = CommandHandler.Create(() => this.HandleCommand());
    }

    private int HandleCommand()
    {
        // Check if CDC is enabled
        var isEnabled = _dac.ExecuteScalar<bool>(
            "SELECT is_cdc_enabled FROM sys.databases WHERE name = DB_NAME()");

        Console.WriteLine($"CDC Enabled: {isEnabled}");
        return 1;
    }
}
```

### Adding Command Options

```csharp
public MyCommand(SimpleDac dac, ILoggerFactory factory)
   : base("mycommand", "description")
{
    var option = new Option<string>("--option")
    {
        Description = "Option description",
        IsRequired = false
    };
    this.AddOption(option);

    this.Handler = CommandHandler.Create<string>((option) => this.HandleCommand(option));
}
```

## Performance Considerations

- **Large Databases:** Profile generation can be slow for databases with many changes
- **Memory Usage:** Large profiles consume significant memory during processing
- **Network Latency:** Remote database connections may impact performance
- **CDC Overhead:** CDC adds overhead to DML operations on monitored tables

## Security Considerations

- **Connection Strings:** Avoid hardcoded credentials in production
- **File Permissions:** Secure profile files containing sensitive data
- **Database Access:** Use least-privilege database accounts
- **Audit Logging:** Consider logging all CDC operations for audit trails
