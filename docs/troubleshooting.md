# Troubleshooting Guide

## Overview

This guide provides solutions to common issues encountered when using the CDC Testing Framework. Issues are organized by component and include detailed diagnostic steps and solutions.

## General Diagnostics

### System Information Collection

Before troubleshooting, collect the following information:

```bash
# .NET Version
dotnet --version
dotnet --info

# SQL Server Version
sqlcmd -Q "SELECT @@VERSION"

# CDC Framework Version
dotnet run --project cdc-proto -- --version

# System Resources
# Windows
systeminfo | findstr /C:"Total Physical Memory"
# Linux/macOS
free -h
df -h
```

### Log Collection

Enable detailed logging for better diagnostics:

```csharp
// In Program.cs or startup code
services.AddLogging(builder =>
{
    builder.AddConsole()
           .AddDebug()
           .SetMinimumLevel(LogLevel.Debug);
});
```

## Database Connection Issues

### Issue: "Login failed for user"

**Symptoms:**

- Connection timeouts
- Authentication errors
- "Login failed" messages

**Diagnostic Steps:**

```sql
-- Check SQL Server authentication mode
SELECT SERVERPROPERTY('IsIntegratedSecurityOnly') AS [Is Windows Auth Only];
-- 0 = Mixed Mode, 1 = Windows Authentication Only

-- Check if login exists
SELECT name, type_desc, is_disabled
FROM sys.server_principals
WHERE name = 'your_username';

-- Check database user mapping
USE YourDatabase;
SELECT name, type_desc, authentication_type_desc
FROM sys.database_principals
WHERE name = 'your_username';
```

**Solutions:**

1. **Enable Mixed Mode Authentication:**

```sql
-- Enable SQL Server and Windows Authentication mode
-- Requires SQL Server restart
EXEC xp_instance_regwrite N'HKEY_LOCAL_MACHINE',
    N'Software\Microsoft\MSSQLServer\MSSQLServer',
    N'LoginMode', REG_DWORD, 2;
```

2. **Create/Fix User Login:**

```sql
-- Create SQL Server login
CREATE LOGIN cdc_user WITH PASSWORD = 'YourStrong@Passw0rd';

-- Create database user
USE YourDatabase;
CREATE USER cdc_user FOR LOGIN cdc_user;

-- Grant permissions
ALTER ROLE db_owner ADD MEMBER cdc_user;
```

3. **Update Connection String:**

```csharp
// Add TrustServerCertificate for local development
var connectionString = "Server=localhost;Database=TestDB;User Id=sa;Password=YourPassword;TrustServerCertificate=true;";
```

### Issue: "Network-related or instance-specific error"

**Symptoms:**

- Cannot connect to SQL Server
- Timeout errors
- "Server not found" errors

**Diagnostic Steps:**

```bash
# Test network connectivity
telnet localhost 1433
# or
Test-NetConnection -ComputerName localhost -Port 1433

# Check SQL Server services
# Windows
sc query MSSQLSERVER
# or
Get-Service -Name "*SQL*"

# Check SQL Server configuration
sqlcmd -L  # List SQL Server instances
```

**Solutions:**

1. **Enable TCP/IP Protocol:**

   - Open SQL Server Configuration Manager
   - Navigate to SQL Server Network Configuration
   - Enable TCP/IP protocol
   - Restart SQL Server service

2. **Configure Firewall:**

```bash
# Windows Firewall
netsh advfirewall firewall add rule name="SQL Server" dir=in action=allow protocol=TCP localport=1433

# Check if port is listening
netstat -an | findstr 1433
```

3. **Docker Container Issues:**

```bash
# Check container status
docker ps -a

# Check container logs
docker logs sql-server-container

# Restart container
docker restart sql-server-container
```

## CDC-Specific Issues

### Issue: "CDC is not enabled for database"

**Symptoms:**

- CDC operations fail
- "CDC is not enabled" error messages

**Diagnostic Steps:**

```sql
-- Check if CDC is enabled on database
SELECT name, is_cdc_enabled
FROM sys.databases
WHERE name = 'YourDatabase';

-- Check SQL Server edition
SELECT SERVERPROPERTY('Edition') AS Edition;

-- Check if SQL Server Agent is running
SELECT
    servicename,
    status_desc,
    startup_type_desc
FROM sys.dm_server_services
WHERE servicename LIKE '%Agent%';
```

**Solutions:**

1. **Enable CDC on Database:**

```sql
USE YourDatabase;
EXEC sys.sp_cdc_enable_db;
```

2. **Start SQL Server Agent:**

```bash
# Windows Command Prompt (as Administrator)
NET START SQLSERVERAGENT

# Or use SQL Server Configuration Manager
```

3. **Check SQL Server Edition:**

```sql
-- CDC requires Standard/Enterprise/Developer Edition
-- If using Express Edition, upgrade to Developer Edition (free)
```

### Issue: "Table does not have a primary key"

**Symptoms:**

- CDC enablement fails for specific tables
- "Primary key required" errors

**Diagnostic Steps:**

```sql
-- Check tables without primary keys
SELECT
    t.name AS table_name,
    t.object_id
FROM sys.tables t
LEFT JOIN sys.key_constraints kc ON t.object_id = kc.parent_object_id
    AND kc.type = 'PK'
WHERE kc.parent_object_id IS NULL
    AND t.name NOT LIKE 'sys%';

-- Check existing indexes
SELECT
    t.name AS table_name,
    i.name AS index_name,
    i.type_desc,
    i.is_primary_key,
    i.is_unique
FROM sys.tables t
INNER JOIN sys.indexes i ON t.object_id = i.object_id
WHERE t.name = 'YourTableName';
```

**Solutions:**

1. **Add Primary Key:**

```sql
-- Add identity column as primary key
ALTER TABLE YourTable
ADD ID int IDENTITY(1,1) PRIMARY KEY;

-- Or use existing unique column
ALTER TABLE YourTable
ADD CONSTRAINT PK_YourTable PRIMARY KEY (ExistingUniqueColumn);
```

2. **Use Unique Index Alternative:**

```sql
-- Enable CDC with unique index instead of primary key
EXEC sys.sp_cdc_enable_table
    @source_schema = N'dbo',
    @source_name = N'YourTable',
    @index_name = N'IX_YourTable_UniqueIndex',
    @role_name = NULL;
```

### Issue: "CDC capture job is not running"

**Symptoms:**

- No CDC data being captured
- CDC tables remain empty
- Job history shows failures

**Diagnostic Steps:**

```sql
-- Check CDC jobs
SELECT
    j.name,
    j.enabled,
    j.description,
    ja.run_status,
    ja.run_date,
    ja.run_time
FROM msdb.dbo.sysjobs j
LEFT JOIN msdb.dbo.sysjobactivity ja ON j.job_id = ja.job_id
WHERE j.name LIKE '%cdc%'
ORDER BY ja.run_date DESC, ja.run_time DESC;

-- Check job step details
SELECT
    js.step_name,
    js.command,
    jh.run_status,
    jh.message
FROM msdb.dbo.sysjobsteps js
INNER JOIN msdb.dbo.sysjobhistory jh ON js.job_id = jh.job_id
    AND js.step_id = jh.step_id
INNER JOIN msdb.dbo.sysjobs j ON js.job_id = j.job_id
WHERE j.name LIKE '%cdc%'
ORDER BY jh.run_date DESC, jh.run_time DESC;
```

**Solutions:**

1. **Enable and Start CDC Jobs:**

```sql
-- Enable CDC capture job
EXEC msdb.dbo.sp_update_job
    @job_name = N'cdc.YourDatabase_capture',
    @enabled = 1;

-- Start job manually
EXEC msdb.dbo.sp_start_job
    @job_name = N'cdc.YourDatabase_capture';
```

2. **Fix Job Configuration:**

```sql
-- Reconfigure CDC jobs
EXEC sys.sp_cdc_add_job @job_type = N'capture';
EXEC sys.sp_cdc_add_job @job_type = N'cleanup';
```

3. **Check SQL Server Agent Configuration:**

```sql
-- Verify Agent is configured correctly
EXEC msdb.dbo.sp_help_operator;
EXEC msdb.dbo.sp_help_alert;
```

## CLI Tool Issues

### Issue: "Command not found" or "dotnet command failed"

**Symptoms:**

- CLI commands don't execute
- "dotnet not found" errors
- Build failures

**Diagnostic Steps:**

```bash
# Check .NET installation
dotnet --version
dotnet --list-sdks

# Check project file
cat cdc-proto/cdc-utility.csproj

# Check build output
dotnet build cdc-proto/ --verbosity detailed
```

**Solutions:**

1. **Install/Update .NET SDK:**

```bash
# Download from https://dotnet.microsoft.com/download
# Or use package manager
# Windows (Chocolatey)
choco install dotnet-sdk

# macOS (Homebrew)
brew install --cask dotnet-sdk

# Ubuntu
sudo apt-get update
sudo apt-get install -y dotnet-sdk-6.0
```

2. **Fix Project References:**

```bash
# Restore packages
dotnet restore cdc-proto/

# Clean and rebuild
dotnet clean cdc-proto/
dotnet build cdc-proto/
```

3. **Check Working Directory:**

```bash
# Ensure you're in the correct directory
cd cdc-proto
dotnet run -- --help
```

### Issue: "Profile generation produces empty results"

**Symptoms:**

- Profile JSON files are empty or contain no data
- No CDC changes captured

**Diagnostic Steps:**

```sql
-- Check if CDC tables have data
SELECT COUNT(*) FROM cdc.dbo_YourTable_CT;

-- Check LSN ranges
SELECT
    sys.fn_cdc_get_min_lsn('dbo_YourTable') AS min_lsn,
    sys.fn_cdc_get_max_lsn() AS max_lsn;

-- Check recent changes
SELECT TOP 10 * FROM cdc.dbo_YourTable_CT
ORDER BY __$start_lsn DESC;
```

**Solutions:**

1. **Make Test Changes:**

```sql
-- Insert/Update/Delete some data to generate CDC entries
INSERT INTO YourTable (Column1, Column2) VALUES ('Test', 'Data');
UPDATE YourTable SET Column1 = 'Updated' WHERE ID = 1;
DELETE FROM YourTable WHERE ID = 2;
```

2. **Wait for CDC Capture:**

```sql
-- CDC capture runs every few minutes by default
-- Wait 2-5 minutes after making changes, then check again
SELECT COUNT(*) FROM cdc.dbo_YourTable_CT;
```

3. **Manual CDC Job Execution:**

```sql
-- Force CDC capture job to run
EXEC msdb.dbo.sp_start_job
    @job_name = N'cdc.YourDatabase_capture';
```

## Web API Issues

### Issue: "API not accessible" or "Connection refused"

**Symptoms:**

- Cannot access Swagger UI
- HTTP connection errors
- Port binding failures

**Diagnostic Steps:**

```bash
# Check if API is running
netstat -an | findstr 5102
# or
ss -tulpn | grep 5102

# Check process
ps aux | grep cdc-api
# or
Get-Process | Where-Object {$_.ProcessName -like "*cdc*"}

# Test API endpoint
curl -k https://localhost:7297/swagger
```

**Solutions:**

1. **Check Port Configuration:**

```json
// In launchSettings.json
{
  "profiles": {
    "cdc_api": {
      "applicationUrl": "https://localhost:7297;http://localhost:5102"
    }
  }
}
```

2. **Run with Different Port:**

```bash
dotnet run --project cdc-api --urls "http://localhost:5000"
```

3. **Check Firewall/Security:**

```bash
# Windows
netsh http show urlacl
netsh http add urlacl url=http://+:5102/ user=Everyone

# Check SSL certificate issues
dotnet dev-certs https --trust
```

### Issue: "Swagger UI not loading"

**Symptoms:**

- Blank Swagger page
- JavaScript errors
- API documentation not visible

**Solutions:**

1. **Enable Swagger in Development:**

```csharp
// In Program.cs
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "CDC API V1");
        c.RoutePrefix = "swagger";
    });
}
```

2. **Check Browser Console:**

   - Open browser developer tools (F12)
   - Check for JavaScript errors
   - Clear browser cache

3. **Access Direct URL:**

```
https://localhost:7297/swagger/index.html
```

## MAUI Application Issues

### Issue: "Application won't start" or "Build failures"

**Symptoms:**

- MAUI app crashes on startup
- Build errors related to platforms
- Missing workload errors

**Diagnostic Steps:**

```bash
# Check MAUI workloads
dotnet workload list

# Check project targets
cat cdc-maui/cdc-maui.csproj | grep TargetFrameworks

# Build with detailed output
dotnet build cdc-maui/ --verbosity detailed
```

**Solutions:**

1. **Install MAUI Workloads:**

```bash
# Install required workloads
dotnet workload install maui
dotnet workload install android
dotnet workload install ios
dotnet workload install maccatalyst
```

2. **Update Visual Studio:**

   - Ensure Visual Studio 2022 17.3 or later
   - Install ".NET Multi-platform App UI development" workload

3. **Platform-Specific Issues:**

**Android:**

```bash
# Check Android SDK
$ANDROID_HOME/tools/bin/sdkmanager --list

# Install required components
$ANDROID_HOME/tools/bin/sdkmanager "platforms;android-31"
```

**iOS/macOS:**

```bash
# Check Xcode installation
xcode-select --print-path

# Install Xcode command line tools
xcode-select --install
```

**Windows:**

```bash
# Check Windows SDK
reg query "HKLM\SOFTWARE\WOW6432Node\Microsoft\Microsoft SDKs\Windows\v10.0" /v InstallationFolder
```

### Issue: "Database connection fails in MAUI app"

**Symptoms:**

- Connection timeouts in mobile app
- Different behavior than CLI/API

**Solutions:**

1. **Platform-Specific Connection Strings:**

```csharp
private string GetConnectionString()
{
#if ANDROID
    return "Server=10.0.2.2,1433;Database=TestDB;User Id=sa;Password=YourPassword;TrustServerCertificate=true;";
#elif IOS
    return "Server=localhost,1433;Database=TestDB;User Id=sa;Password=YourPassword;TrustServerCertificate=true;";
#else
    return "Server=localhost;Database=TestDB;User Id=sa;Password=YourPassword;TrustServerCertificate=true;";
#endif
}
```

2. **Network Security Configuration (Android):**

```xml
<!-- In Platforms/Android/Resources/xml/network_security_config.xml -->
<?xml version="1.0" encoding="utf-8"?>
<network-security-config>
    <domain-config cleartextTrafficPermitted="true">
        <domain includeSubdomains="true">10.0.2.2</domain>
        <domain includeSubdomains="true">localhost</domain>
    </domain-config>
</network-security-config>
```

## Performance Issues

### Issue: "Slow profile generation"

**Symptoms:**

- Profile generation takes very long
- High CPU/memory usage
- Timeouts

**Diagnostic Steps:**

```sql
-- Check CDC table sizes
SELECT
    t.name,
    p.rows,
    (p.reserved * 8) / 1024 AS reserved_mb
FROM sys.tables t
INNER JOIN sys.dm_db_partition_stats p ON t.object_id = p.object_id
WHERE t.schema_id = SCHEMA_ID('cdc')
ORDER BY p.reserved DESC;

-- Check query execution plans
SET STATISTICS IO ON;
SELECT * FROM cdc.fn_cdc_get_net_changes_dbo_YourTable(
    sys.fn_cdc_get_min_lsn('dbo_YourTable'),
    sys.fn_cdc_get_max_lsn(),
    'all'
);
```

**Solutions:**

1. **Optimize CDC Queries:**

```sql
-- Add indexes to CDC tables
CREATE INDEX IX_CDC_YourTable_StartLSN
ON cdc.dbo_YourTable_CT(__$start_lsn);

-- Use LSN ranges instead of full table scans
DECLARE @from_lsn binary(10), @to_lsn binary(10);
SELECT @from_lsn = sys.fn_cdc_map_time_to_lsn('smallest greater than or equal',
    DATEADD(hour, -1, GETDATE()));
SELECT @to_lsn = sys.fn_cdc_get_max_lsn();
```

2. **Increase Timeout Values:**

```csharp
// In SimpleDac.cs
const int defaultTimeout = 300; // Increase from 120 to 300 seconds
```

3. **Implement Paging:**

```csharp
// Process CDC data in batches
var batchSize = 1000;
var offset = 0;
var hasMore = true;

while (hasMore)
{
    var batch = GetCdcDataBatch(offset, batchSize);
    ProcessBatch(batch);
    hasMore = batch.Count == batchSize;
    offset += batchSize;
}
```

## Memory Issues

### Issue: "Out of memory" errors

**Symptoms:**

- Application crashes with OutOfMemoryException
- High memory usage during profile generation

**Solutions:**

1. **Implement Streaming:**

```csharp
public static IEnumerable<IDictionary<string, object>> BuildProfileStreaming(
    SimpleDac dac,
    SqlTable table,
    ILogger logger)
{
    var sql = GetNetSqlFromTemplate(table.Schema, table.Name);

    return dac.ExecuteReader(sql, reader =>
    {
        while (reader.Read())
        {
            var record = new Dictionary<string, object>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                record[reader.GetName(i)] = reader.GetValue(i);
            }
            yield return record;
        }
    });
}
```

2. **Dispose Resources Properly:**

```csharp
using var dac = new SimpleDac(connectionString, logger);
// Operations here
// dac will be disposed automatically
```

3. **Process Tables Individually:**

```csharp
// Instead of loading all tables at once
foreach (var table in tables)
{
    var tableProfile = BuildTableProfile(dac, table, logger);
    SaveTableProfile(table.Name, tableProfile);
    // Allow garbage collection between tables
    GC.Collect();
}
```

## Getting Additional Help

### Enable Detailed Logging

```csharp
// Add to Program.cs or startup
services.AddLogging(builder =>
{
    builder.AddConsole()
           .AddDebug()
           .AddEventLog() // Windows only
           .SetMinimumLevel(LogLevel.Trace);
});
```

### Collect Diagnostic Information

```bash
# Create diagnostic report
echo "=== System Information ===" > diagnostic-report.txt
dotnet --info >> diagnostic-report.txt
echo "=== SQL Server Version ===" >> diagnostic-report.txt
sqlcmd -Q "SELECT @@VERSION" >> diagnostic-report.txt
echo "=== CDC Status ===" >> diagnostic-report.txt
sqlcmd -Q "SELECT name, is_cdc_enabled FROM sys.databases" >> diagnostic-report.txt
```

### Contact Support

When reporting issues, include:

- Complete error messages and stack traces
- System configuration (OS, .NET version, SQL Server version)
- Steps to reproduce the issue
- Diagnostic report output
- Relevant log files

### Community Resources

- Check project documentation in the `docs/` folder
- Search existing issues in the project repository
- Review SQL Server CDC documentation
- Consult .NET MAUI troubleshooting guides

This troubleshooting guide covers the most common issues encountered with the CDC Testing Framework. For issues not covered here, enable detailed logging and collect diagnostic information before seeking additional help.
