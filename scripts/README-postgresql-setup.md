# PostgreSQL Database Setup Instructions

## Problem Description

When running the PostgreSQL script `create-trace-database-postgresql.sql`, the database is created but not visible in DBeaver, and tables are created in the wrong database.

## Root Cause

The script needs to be run in two separate steps:

1. Create the database while connected to the `postgres` database
2. Create tables while connected to the newly created `cdcme` database

## Solution

### Option 1: Use the Split Scripts (Recommended)

1. **Step 1**: Connect to your PostgreSQL server using the `postgres` database in DBeaver
2. **Step 2**: Run `create-trace-database-postgresql-part1.sql`
3. **Step 3**: Create a new connection in DBeaver or switch connection to use the `cdcme` database
4. **Step 4**: Run `create-trace-database-postgresql-part2.sql`

### Option 2: Use the Original Script Manually

1. **Step 1**: Connect to your PostgreSQL server using the `postgres` database
2. **Step 2**: Run only lines 18-25 from `create-trace-database-postgresql.sql` (the CREATE DATABASE command)
3. **Step 3**: Connect to the `cdcme` database
4. **Step 4**: Run lines 31-146 from `create-trace-database-postgresql.sql` (the table creation commands)

## DBeaver Connection Setup

After creating the database, you need to:

1. Right-click on your PostgreSQL server in DBeaver
2. Select "Create" → "Connection"
3. Use the same connection details but change the database name to `cdcme`
4. Test the connection and save

Alternatively, you can:

1. Right-click on your existing connection
2. Select "Edit Connection"
3. Change the database name from `postgres` to `cdcme`
4. Save and reconnect

## Verification

After completing the setup, you should see:

- A `cdcme` database in your PostgreSQL server
- Four tables in the `cdcme` database:
  - `trace_sessions`
  - `trace_events`
  - `cdc_captures`
  - `comparison_results`

## Environment Configuration

Make sure your `.env` file contains:

```
POSTGRES_CONNECTION_STRING=Host=your-host;Database=cdcme;Username=your-username;Password=your-password
```

Note: The database name in the connection string should be `cdcme`, not `postgres`.
