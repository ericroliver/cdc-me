# Getting Started with CDC Testing Framework

## Overview

This guide will help you set up and start using the CDC Testing Framework for database change validation. The framework provides multiple interfaces (CLI, Web API, and Desktop App) for managing Change Data Capture operations on SQL Server databases.

## Prerequisites

### System Requirements

#### Development Environment

- **Operating System**: Windows 10/11, macOS 10.15+, or Linux (Ubuntu 18.04+)
- **.NET SDK**: .NET 6.0 or later
- **IDE**: Visual Studio 2022, Visual Studio Code, or JetBrains Rider

#### Database Requirements

- **SQL Server**: SQL Server 2016 or later (Standard/Enterprise Edition)
- **CDC Support**: Change Data Capture must be available (not supported in Express Edition)
- **Permissions**: `db_owner` role or specific CDC permissions

#### Optional Components

- **Docker**: For containerized SQL Server instances
- **Git**: For source code management

### Software Installation

#### 1. Install .NET SDK

Download and install the latest .NET 6.0 SDK from [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)

**Verify Installation:**

```bash
dotnet --version
# Should output 6.0.x or later
```

#### 2. Install SQL Server

Choose one of the following options:

**Option A: SQL Server Developer Edition (Recommended for Development)**

1. Download from [Microsoft SQL Server Downloads](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)
2. Install with default settings
3. Enable SQL Server Authentication during setup

**Option B: Docker Container (Quick Setup)**

```bash
# Pull SQL Server 2019 image
docker pull mcr.microsoft.com/mssql/server:2019-latest

# Run SQL Server container
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" \
   -p 1433:1433 --name sql-server-cdc \
   -d mcr.microsoft.com/mssql/server:2019-latest
```

#### 3. Install Development Tools

**Visual Studio 2022 (Windows/Mac):**

- Download from [Visual Studio](https://visualstudio.microsoft.com/)
- Include "ASP.NET and web development" workload for Web API

**Visual Studio Code (Cross-platform):**

```bash
# Install VS Code extensions
code --install-extension ms-dotnettools.csharp
```

## Project Setup

### 1. Clone the Repository

```bash
git clone <repository-url>
cd cdc-me
```

### 2. Restore Dependencies

```bash
# Restore all projects
dotnet restore

# Or restore individual projects
dotnet restore cdc-lib/cdc-lib.csproj
dotnet restore cdc-proto/cdc-utility.csproj
dotnet restore cdc-api/cdc-api.csproj
```

### 3. Build the Solution

```bash
# Build all projects
dotnet build

# Or build specific projects
dotnet build cdc-lib/cdc-lib.csproj
dotnet build cdc-proto/cdc-utility.csproj
```

## Database Setup

### 1. Create Test Database

Connect to your SQL Server instance and create a test database:

```sql
-- Create test database
CREATE DATABASE CdcTestDB;
GO

USE CdcTestDB;
GO

-- Create sample tables for testing
CREATE TABLE Customers (
    CustomerID int IDENTITY(1,1) PRIMARY KEY,
    CustomerName nvarchar(100) NOT NULL,
    Email nvarchar(100),
    CreatedDate datetime2 DEFAULT GETDATE(),
    LastModified datetime2 DEFAULT GETDATE()
);

CREATE TABLE Orders (
    OrderID int IDENTITY(1,1) PRIMARY KEY,
    CustomerID int NOT NULL,
    OrderDate datetime2 DEFAULT GETDATE(),
    TotalAmount decimal(10,2),
    Status nvarchar(20) DEFAULT 'Pending',
    FOREIGN KEY (CustomerID) REFERENCES Customers(CustomerID)
);

-- Insert sample data
INSERT INTO Customers (CustomerName, Email) VALUES
    ('Acme Corporation', 'contact@acme.com'),
    ('Beta Industries', 'info@beta.com'),
    ('Gamma Solutions', 'hello@gamma.com');

INSERT INTO Orders (CustomerID, TotalAmount, Status) VALUES
    (1, 1500.00, 'Completed'),
    (2, 750.50, 'Pending'),
    (1, 2200.75, 'Processing');
```

### 2. Configure Database Permissions

Grant necessary permissions for CDC operations:

```sql
-- Create CDC user (optional - you can use sa account for testing)
CREATE LOGIN cdc_user WITH PASSWORD = 'YourStrong@Passw0rd';
USE CdcTestDB;
CREATE USER cdc_user FOR LOGIN cdc_user;

-- Grant CDC permissions
ALTER ROLE db_owner ADD MEMBER cdc_user;

-- Or grant specific CDC permissions
GRANT SELECT ON SCHEMA::cdc TO cdc_user;
EXEC sp_addrolemember 'db_ddladmin', 'cdc_user';
```

## Configuration

### 1. Update Connection Strings

**CLI Tool Configuration:**
Edit `cdc-proto/Program.cs` and update the connection string:

```csharp
var connectionString = "Server=localhost;Database=CdcTestDB;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;";
```

**Web API Configuration:**
Create `cdc-api/appsettings.json` with connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=CdcTestDB;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

## First Run

### 1. Test the CLI Tool

**Build and run the CLI:**

```bash
cd cdc-proto
dotnet run -- --help
```

**Initialize CDC:**

```bash
dotnet run -- init
```

**Expected Output:**

```
[Information] init command
[Debug] enabling cdc for dbo.Customers, index: PK__Customer__A4AE64B8... : EXEC sys.sp_cdc_enable_table...
[Debug] enabling cdc for dbo.Orders, index: PK__Orders__C3905BAF... : EXEC sys.sp_cdc_enable_table...
```

### 2. Test Profile Generation

**Make some data changes:**

```sql
USE CdcTestDB;

-- Update existing records
UPDATE Customers SET CustomerName = 'Acme Corp Updated' WHERE CustomerID = 1;
UPDATE Orders SET Status = 'Shipped' WHERE OrderID = 1;

-- Insert new records
INSERT INTO Customers (CustomerName, Email) VALUES ('Delta Corp', 'info@delta.com');
INSERT INTO Orders (CustomerID, TotalAmount) VALUES (4, 999.99);
```

**Generate profile:**

```bash
dotnet run -- profile -out baseline-profile.json
```

**Check the generated profile:**

```bash
# View the profile file
cat baseline-profile.json
```

### 3. Test the Web API

**Start the API:**

```bash
cd ../cdc-api
dotnet run
```

**Access Swagger UI:**
Open your browser and navigate to: `https://localhost:7297/swagger`

**Test the endpoint:**

```bash
curl -X POST https://localhost:7297/Cdc -k
```

## Basic Workflow Example

Here's a complete example of using the CDC Testing Framework:

### 1. Setup Phase

```bash
# Initialize CDC
cd cdc-proto
dotnet run -- init
```

### 2. Baseline Capture

```bash
# Generate baseline profile
dotnet run -- profile -out baseline.json
```

### 3. Make Changes

Execute your test scenarios or application workflows that modify data.

### 4. Comparison Capture

```bash
# Generate comparison profile
dotnet run -- profile -out comparison.json
```

### 5. Analyze Differences

```bash
# Compare profiles
dotnet run -- diff -left baseline.json -right comparison.json -out differences.json

# View differences
cat differences.json
```

### 6. Cleanup

```bash
# Disable CDC
dotnet run -- teardown
```

## Verification

### 1. Verify CDC is Working

Check that CDC is enabled on your database:

```sql
-- Check if CDC is enabled on database
SELECT name, is_cdc_enabled
FROM sys.databases
WHERE name = 'CdcTestDB';

-- Check CDC-enabled tables
SELECT name, is_tracked_by_cdc
FROM sys.tables
WHERE is_tracked_by_cdc = 1;

-- View CDC tables
SELECT * FROM cdc.change_tables;

-- Check CDC data
SELECT * FROM cdc.dbo_Customers_CT;
SELECT * FROM cdc.dbo_Orders_CT;
```

### 2. Verify Profile Generation

Check that profiles contain expected data:

```bash
# Check profile structure
jq . baseline.json

# Count changes per table
jq 'to_entries | map({table: .key, changes: (.value | length)})' baseline.json
```

### 3. Verify Difference Detection

Ensure differences are properly identified:

```bash
# View difference summary
jq 'to_entries | map({table: .key, diffs: (.value.diff | length)})' differences.json
```

## Common Issues and Solutions

### Issue: "CDC is not enabled for database"

**Solution:**

```sql
-- Enable CDC on database
USE CdcTestDB;
EXEC sys.sp_cdc_enable_db;
```

### Issue: "Login failed for user"

**Solution:**

- Verify connection string credentials
- Check SQL Server authentication mode
- Ensure user has necessary permissions

### Issue: "Table does not have a primary key"

**Solution:**

```sql
-- Add primary key to table
ALTER TABLE YourTable ADD CONSTRAINT PK_YourTable PRIMARY KEY (YourColumn);
```

## Next Steps

1. **Explore the Documentation**: Read the detailed component documentation
2. **Customize Configuration**: Adapt connection strings and settings for your environment
3. **Integrate with CI/CD**: Set up automated testing workflows
4. **Extend Functionality**: Add custom analyzers or export formats
5. **Monitor Performance**: Profile your CDC operations for optimization

## Getting Help

- **Documentation**: Check the `docs/` folder for detailed component documentation
- **Issues**: Report bugs and feature requests in the project repository
- **Community**: Join discussions and share experiences with other users

## Development Environment Setup

For contributors and advanced users who want to modify the framework:

### 1. Development Dependencies

```bash
# Install additional development tools
dotnet tool install --global dotnet-ef
dotnet tool install --global dotnet-aspnet-codegenerator
```

### 2. IDE Configuration

**Visual Studio 2022:**

- Install "SQL Server Data Tools" for database project support
- Configure code analysis and formatting rules
- Set up debugging configurations for each project

**VS Code:**

```json
// .vscode/launch.json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Launch CLI",
      "type": "coreclr",
      "request": "launch",
      "program": "${workspaceFolder}/cdc-proto/bin/Debug/net6.0/cdc-utility.dll",
      "args": ["init"],
      "cwd": "${workspaceFolder}/cdc-proto",
      "console": "internalConsole"
    },
    {
      "name": "Launch API",
      "type": "coreclr",
      "request": "launch",
      "program": "${workspaceFolder}/cdc-api/bin/Debug/net6.0/cdc-api.dll",
      "cwd": "${workspaceFolder}/cdc-api",
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  ]
}
```

### 3. Testing Setup

```bash
# Add test projects
dotnet new xunit -n cdc-lib.Tests
dotnet new xunit -n cdc-proto.Tests
dotnet new xunit -n cdc-api.Tests

# Add test references
dotnet add cdc-lib.Tests/cdc-lib.Tests.csproj reference cdc-lib/cdc-lib.csproj
```

This completes the getting started guide. You should now have a fully functional CDC Testing Framework environment ready for database change validation testing.
