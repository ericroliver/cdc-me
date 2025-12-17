# CDCMe Database Schema Cleanup and Rebuild Guide

## Overview

This guide explains how to clean up and rebuild the `cdcme` trace database schema when you need to update the table structure or start fresh.

## The Two Database Architecture

**Important**: This project uses TWO separate databases:

### 1. `cdcme` Database (Trace Storage)
- **Purpose**: Stores trace metadata, captured CDC data, and comparison results
- **Location**: PostgreSQL or SQL Server (your choice)
- **Tables**:
  - `trace_sessions` - Trace session metadata
  - `trace_events` - Individual captured events
  - `cdc_capture_headers` - Capture metadata (parent)
  - `cdc_captures` - Individual table captures (detail)
  - `comparison_results` - Comparison results

### 2. Test Database (e.g., `erp_test` or your application database)
- **Purpose**: The actual database under test where your application data lives
- **Location**: SQL Server only (requires CDC support)
- **CDC Tables**: `cdc.*` system tables created by SQL Server's CDC feature
- **Note**: This is YOUR existing application database

## When to Clean Up the Schema

You need to rebuild the `cdcme` schema when:
- ✅ You need to add the missing `cdc_capture_headers` table
- ✅ The schema structure has changed
- ✅ You want to start fresh with no data
- ✅ You're experiencing schema-related errors

## Cleanup Options

### Option 1: Complete Rebuild (Recommended)

This drops all tables and recreates them with the latest schema.

**Steps:**
1. Connect to the `cdcme` database (NOT your test database)
2. Run: [`rebuild-cdcme-schema-postgresql.sql`](rebuild-cdcme-schema-postgresql.sql)

**What it does:**
- Drops all existing trace tables
- Recreates all tables with the updated schema including `cdc_capture_headers`
- ⚠️ **Deletes ALL existing trace data**

### Option 2: Drop Only

This only drops the tables without recreating them.

**Steps:**
1. Connect to the `cdcme` database
2. Run: [`drop-cdcme-schema-postgresql.sql`](drop-cdcme-schema-postgresql.sql)
3. Then run: [`create-trace-database-postgresql-part2.sql`](create-trace-database-postgresql-part2.sql)

**Use when:**
- You want more control over the recreation process
- You want to manually verify before recreating

### Option 3: Add Missing Table Only

This adds the `cdc_capture_headers` table without dropping existing data.

**Steps:**
1. Connect to the `cdcme` database
2. Run: [`add-cdc-capture-headers-table.sql`](add-cdc-capture-headers-table.sql)

**Use when:**
- You already have data you want to keep
- You only need to add the missing `cdc_capture_headers` table
- ⚠️ **Note**: Existing `cdc_captures` records won't have `capture_header_id` populated

## Recommended Cleanup Process

### For Fresh Start:
```bash
# 1. Connect to PostgreSQL cdcme database
psql -h your-host -U your-user -d cdcme

# 2. Run the rebuild script
\i scripts/rebuild-cdcme-schema-postgresql.sql

# 3. Verify tables were created
\dt
```

### For Adding Missing Table:
```bash
# 1. Connect to PostgreSQL cdcme database
psql -h your-host -U your-user -d cdcme

# 2. Run the add table script
\i scripts/add-cdc-capture-headers-table.sql

# 3. Verify table was added
\dt
```

## Verification

After running any cleanup script, verify your schema:

```sql
-- Check all tables exist
SELECT table_name 
FROM information_schema.tables 
WHERE table_schema = 'public' 
  AND table_catalog = 'cdcme'
ORDER BY table_name;
```

**Expected tables:**
- `cdc_capture_headers` ✓ (This was missing before!)
- `cdc_captures` ✓
- `comparison_results` ✓
- `trace_events` ✓
- `trace_sessions` ✓

## Schema Changes Summary

### What Changed:
- **Added**: `cdc_capture_headers` table (was missing from PostgreSQL script)
- **Updated**: `cdc_captures` now has `capture_header_id` foreign key
- **Purpose**: Supports the header-detail pattern used by the API endpoints

### Why the Change:
The code in [`CdcController.cs`](../cdc-api/Controllers/CdcController.cs) and [`CdcCaptureComparer.cs`](../cdc-lib/Cdc/CdcCaptureComparer.cs) expects a two-table pattern:
- **Header**: Stores capture metadata (name, type, session info)
- **Detail**: Stores individual table captures (linked to header)

## Important Notes

### ⚠️ Data Loss Warning
- All rebuild operations **DELETE ALL DATA** in the trace tables
- Make sure you have backups if you need to preserve data
- This does NOT affect your test database (e.g., `erp_test`)

### 📝 Development vs Production
- These are development/test databases only
- Never run these scripts on production data
- Always test on a separate environment first

### 🔒 Permissions Required
You need appropriate permissions on the `cdcme` database:
- `DROP TABLE` permission
- `CREATE TABLE` permission
- `GRANT` permission (for the final permission grants)

## Troubleshooting

### "Table does not exist" errors
- You might have already dropped the tables
- This is normal - the scripts use `IF EXISTS` to handle this

### "Permission denied" errors
- Make sure you're connected as the database owner or have admin rights
- Check your PostgreSQL user has appropriate privileges

### "Database does not exist" errors
- You need to create the `cdcme` database first
- Run: [`create-trace-database-postgresql-part1.sql`](create-trace-database-postgresql-part1.sql)

## Next Steps After Cleanup

1. **Verify schema**: Run the verification query above
2. **Update connection strings**: Ensure `.env` file has correct `POSTGRES_CONNECTION_STRING`
3. **Test the API**: Try creating a new trace session via the API
4. **Run tests**: Execute the test suite to verify everything works

## Related Documentation

- [PostgreSQL Setup Guide](README-postgresql-setup.md)
- [Database Setup](../docs/database-setup.md)
- [Architecture Overview](../docs/architecture.md)