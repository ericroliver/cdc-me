# CDC Database Schema Proposal

## Problem Statement

The current [`cdc_captures`](../scripts/create-trace-database-postgresql-part2.sql#L72) table stores one record per table with CDC data, but lacks a header record to represent the overall CDC capture operation with its metadata (capture name, table filters, creation date, etc.).

## Current Schema Issues

```sql
-- Current cdc_captures table - one record per table
CREATE TABLE cdc_captures (
    capture_id UUID PRIMARY KEY,
    session_id UUID NOT NULL REFERENCES trace_sessions(session_id),
    capture_type VARCHAR(50) NOT NULL,
    capture_time TIMESTAMP WITH TIME ZONE NOT NULL,
    table_name VARCHAR(256) NOT NULL,  -- This makes it per-table
    capture_data JSONB NOT NULL,
    record_count INTEGER NOT NULL,
    data_hash VARCHAR(64)
);
```

**Issues:**

- No single record represents the overall CDC capture operation
- No place to store `tablesToInclude` and `tablesToExclude` filters
- No capture-level metadata (capture name, description, etc.)
- Difficult to query for "all tables in a capture"

## Proposed Solution

### Option 1: Add CDC Capture Headers Table (Recommended)

Create a new `cdc_capture_headers` table to represent the overall capture operation:

```sql
-- New header table for CDC capture operations
CREATE TABLE IF NOT EXISTS cdc_capture_headers (
    capture_header_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL REFERENCES trace_sessions(session_id) ON DELETE CASCADE,
    capture_name VARCHAR(255) NOT NULL,
    capture_type VARCHAR(50) NOT NULL, -- Baseline, Replay, Optimized
    capture_time TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    tables_to_include JSONB, -- Array of table names that were included
    tables_to_exclude JSONB, -- Array of table names that were excluded
    tables_enabled JSONB NOT NULL, -- Array of tables that actually had CDC enabled
    tables_skipped JSONB, -- Array of tables that were skipped (errors, etc.)
    total_records INTEGER NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'Completed', -- Completed, Failed, InProgress
    error_messages JSONB, -- Array of any errors that occurred
    created_by VARCHAR(128) NOT NULL DEFAULT current_user,
    description TEXT
);

-- Modified cdc_captures table - now references the header
CREATE TABLE IF NOT EXISTS cdc_captures (
    capture_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    capture_header_id UUID NOT NULL REFERENCES cdc_capture_headers(capture_header_id) ON DELETE CASCADE,
    table_name VARCHAR(256) NOT NULL,
    capture_data JSONB NOT NULL,
    record_count INTEGER NOT NULL,
    data_hash VARCHAR(64) -- SHA256 hash for quick comparison
);

-- Indexes
CREATE INDEX IF NOT EXISTS idx_cdc_capture_headers_session ON cdc_capture_headers(session_id, capture_name);
CREATE INDEX IF NOT EXISTS idx_cdc_captures_header ON cdc_captures(capture_header_id);
```

### Option 2: Modify Existing Schema

Alternatively, we could modify the existing approach by using the session mechanism differently, but Option 1 is cleaner.

## Data Flow with New Schema

### 1. Start CDC Operation

- Create or update record in `trace_sessions`
- Store session-level information

### 2. Stop CDC Operation (Capture Data)

- Create record in `cdc_capture_headers` with:
  - `capture_name` from request
  - `tables_to_include` and `tables_to_exclude` from original start request
  - `tables_enabled` - actual tables that had CDC enabled
  - `tables_skipped` - tables that were skipped due to errors
- For each table with CDC data:
  - Create record in `cdc_captures` linked to the header
  - Store table-specific data and hash

### 3. Query Operations

- Get all captures for a session: Query `cdc_capture_headers`
- Get specific capture details: Join `cdc_capture_headers` with `cdc_captures`
- Compare captures: Use `data_hash` from `cdc_captures` records

## API Response Updates

With this schema, the stop endpoint response becomes more meaningful:

```json
{
  "success": true,
  "sessionName": "my-cdc-session",
  "captureName": "baseline-capture",
  "captureHeaderId": "uuid-for-header",
  "message": "CDC data captured and CDC disabled successfully",
  "tablesEnabled": ["dbo.Orders", "dbo.Customers"],
  "tablesSkipped": ["dbo.AuditLog"],
  "tablesWithChanges": ["dbo.Orders", "dbo.Customers"],
  "totalRecords": 150,
  "tableDetails": [
    {
      "tableName": "dbo.Orders",
      "recordCount": 100,
      "captureId": "uuid-for-table-capture"
    },
    {
      "tableName": "dbo.Customers",
      "recordCount": 50,
      "captureId": "uuid-for-table-capture"
    }
  ]
}
```

## Benefits of This Approach

1. **Clear Hierarchy**: Header → Table Details relationship
2. **Complete Metadata**: Capture name, filters, timestamps all stored
3. **Easy Querying**: Simple to get all captures or drill down to table details
4. **Audit Trail**: Full history of what was included/excluded/skipped
5. **Error Tracking**: Per-capture error storage
6. **Comparison Ready**: Easy to compare captures by header ID

## Migration Considerations

- This would require a database schema update
- Existing `cdc_captures` data would need migration if any exists
- API responses would be enhanced but remain backward compatible

## Questions for Review

1. Do you approve of the `cdc_capture_headers` approach?
2. Should we store the original `tablesToInclude`/`tablesToExclude` filters in the header?
3. Any additional metadata you'd like captured at the header level?
4. Should we implement this as a new schema or modify existing tables?
