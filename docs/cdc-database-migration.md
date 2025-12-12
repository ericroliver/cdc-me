# CDC Database Schema Migration

## Overview

This document provides the SQL migration script to update the CdcMe database schema to support the new header-detail pattern for CDC captures.

## Migration Script

The following SQL should be executed against the CdcMe PostgreSQL database:

```sql
-- CDC Capture Schema Migration Script
--
-- This script adds the new cdc_capture_headers table and modifies the existing
-- cdc_captures table to support the header-detail pattern for CDC operations.
--
-- IMPORTANT: Run this script while connected to the 'cdcme' database

-- Step 1: Create the new cdc_capture_headers table
CREATE TABLE IF NOT EXISTS cdc_capture_headers (
    capture_header_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL REFERENCES trace_sessions(session_id) ON DELETE CASCADE,
    capture_name VARCHAR(255) NOT NULL,
    capture_type VARCHAR(50) NOT NULL, -- Baseline, Replay, Optimized, Intermediate
    capture_time TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    tables_to_include JSONB, -- Array of table names that were requested to include
    tables_to_exclude JSONB, -- Array of table names that were requested to exclude
    tables_enabled JSONB NOT NULL, -- Array of tables that actually had CDC enabled
    tables_skipped JSONB, -- Array of tables that were skipped (errors, no primary key, etc.)
    total_records INTEGER NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'Completed', -- Completed, Failed, InProgress
    error_messages JSONB, -- Array of any errors that occurred during capture
    created_by VARCHAR(128) NOT NULL DEFAULT current_user,
    description TEXT
);

-- Step 2: Create indexes for the new table
CREATE INDEX IF NOT EXISTS idx_cdc_capture_headers_session ON cdc_capture_headers(session_id, capture_name);
CREATE INDEX IF NOT EXISTS idx_cdc_capture_headers_time ON cdc_capture_headers(capture_time);
CREATE INDEX IF NOT EXISTS idx_cdc_capture_headers_type ON cdc_capture_headers(capture_type);

-- Step 3: Backup existing cdc_captures data (if any exists)
-- Note: This creates a backup table - remove this step if no existing data needs to be preserved
CREATE TABLE IF NOT EXISTS cdc_captures_backup AS SELECT * FROM cdc_captures;

-- Step 4: Drop the existing cdc_captures table
-- WARNING: This will delete all existing CDC capture data
-- Make sure you have backed up any important data before running this
DROP TABLE IF EXISTS cdc_captures CASCADE;

-- Step 5: Recreate cdc_captures table with new schema
CREATE TABLE IF NOT EXISTS cdc_captures (
    capture_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    capture_header_id UUID NOT NULL REFERENCES cdc_capture_headers(capture_header_id) ON DELETE CASCADE,
    table_name VARCHAR(256) NOT NULL,
    capture_data JSONB NOT NULL, -- JSON data containing the CDC changes
    record_count INTEGER NOT NULL,
    data_hash VARCHAR(64) -- SHA256 hash for quick comparison
);

-- Step 6: Create indexes for the modified table
CREATE INDEX IF NOT EXISTS idx_cdc_captures_header ON cdc_captures(capture_header_id);
CREATE INDEX IF NOT EXISTS idx_cdc_captures_table ON cdc_captures(table_name);
CREATE INDEX IF NOT EXISTS idx_cdc_captures_hash ON cdc_captures(data_hash);

-- Step 7: Grant permissions
GRANT ALL PRIVILEGES ON cdc_capture_headers TO postgres;
GRANT ALL PRIVILEGES ON cdc_captures TO postgres;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO postgres;

-- Step 8: Verify the migration
SELECT 'CDC Capture schema migration completed successfully!' as result;

-- Optional: Display the new table structures
\d cdc_capture_headers;
\d cdc_captures;
```

## Migration Steps

1. **Backup**: Ensure you have a backup of your CdcMe database before running this migration
2. **Connect**: Connect to the `cdcme` PostgreSQL database (not the `postgres` database)
3. **Execute**: Run the migration script above
4. **Verify**: Check that both tables were created successfully
5. **Test**: Run a simple CDC capture operation to verify the new schema works

## Data Migration (If Needed)

If you have existing data in the old `cdc_captures` table that needs to be preserved, you'll need to:

1. Create header records for existing captures
2. Update the detail records to reference the new headers
3. This would require custom migration logic based on your existing data

## Rollback Plan

If you need to rollback this migration:

1. Drop the new tables: `DROP TABLE cdc_captures CASCADE; DROP TABLE cdc_capture_headers CASCADE;`
2. Restore from backup: `CREATE TABLE cdc_captures AS SELECT * FROM cdc_captures_backup;`
3. Recreate original indexes and constraints

## Testing the New Schema

After migration, test the new schema with a simple insert:

```sql
-- Test insert into header table
INSERT INTO cdc_capture_headers (
    session_id,
    capture_name,
    capture_type,
    tables_enabled,
    total_records
) VALUES (
    (SELECT session_id FROM trace_sessions LIMIT 1),
    'test-capture',
    'Baseline',
    '["dbo.Orders", "dbo.Customers"]'::jsonb,
    0
);

-- Test insert into detail table
INSERT INTO cdc_captures (
    capture_header_id,
    table_name,
    capture_data,
    record_count
) VALUES (
    (SELECT capture_header_id FROM cdc_capture_headers WHERE capture_name = 'test-capture'),
    'dbo.Orders',
    '{"test": "data"}'::jsonb,
    1
);

-- Verify the relationship works
SELECT
    h.capture_name,
    h.capture_type,
    c.table_name,
    c.record_count
FROM cdc_capture_headers h
JOIN cdc_captures c ON h.capture_header_id = c.capture_header_id
WHERE h.capture_name = 'test-capture';

-- Clean up test data
DELETE FROM cdc_capture_headers WHERE capture_name = 'test-capture';
```

## Notes

- The migration drops and recreates the `cdc_captures` table, so existing data will be lost unless backed up
- The new schema provides much richer metadata and better organization of CDC captures
- All CDC API operations will now create both header and detail records
- The header-detail relationship enables better querying and comparison of captures
