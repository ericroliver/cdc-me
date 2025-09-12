# Database Setup Scripts

This directory contains SQL scripts for setting up the CDC trace database on different database platforms.

## Prerequisites

Before running these scripts, you must set up your database connection credentials using environment variables.

### Environment Setup

1. Copy the `.env.example` file from the project root to `.env`:

   ```bash
   cp .env.example .env
   ```

2. Edit the `.env` file and replace the placeholder values with your actual database connection strings:

   ```
   # PostgreSQL Connection String
   POSTGRES_CONNECTION_STRING=Host=your-postgres-host;Database=postgres;Username=your-postgres-username;Password=your-postgres-password

   # SQL Server Connection String
   SQLSERVER_CONNECTION_STRING=Server=your-sqlserver-host;Database=master;User Id=your-sqlserver-username;Password=your-sqlserver-password;TrustServerCertificate=true;
   ```

3. **Important**: Never commit the `.env` file to source control. It's already included in `.gitignore`.

## Running the Scripts

### PostgreSQL

Use your PostgreSQL client and connect using the `POSTGRES_CONNECTION_STRING` environment variable:

**Example with psql:**

```bash
# Load environment variables and connect
source .env
psql "$POSTGRES_CONNECTION_STRING" -f create-trace-database-postgresql.sql
```

**Example with other clients:**
Most PostgreSQL clients support environment variable substitution or you can copy the connection string from your `.env` file.

### SQL Server

Use your SQL Server client and connect using the `SQLSERVER_CONNECTION_STRING` environment variable:

**Example with sqlcmd:**

```bash
# Load environment variables and connect
source .env
sqlcmd -G -C "$SQLSERVER_CONNECTION_STRING" -i create-trace-database-sqlserver.sql
```

**Example with other clients:**
Most SQL Server clients support connection strings directly or you can copy the connection string from your `.env` file.

## What These Scripts Do

Both scripts create the following database objects:

1. **CDC_TraceDB** / **cdc_tracedb** - The main trace database
2. **TraceSessions** / **trace_sessions** - Stores trace session metadata
3. **TraceEvents** / **trace_events** - Stores individual trace events
4. **CdcCaptures** / **cdc_captures** - Stores data capture snapshots
5. **ComparisonResults** / **comparison_results** - Stores comparison results between captures

The scripts are idempotent - they can be run multiple times safely as they check for existing objects before creating them.

## Security Notes

- The `.env` file contains sensitive credentials and is excluded from source control
- Always use the `.env.example` template when setting up new environments
- Never hardcode credentials directly in the SQL scripts
