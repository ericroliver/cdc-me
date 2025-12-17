-- Create trace database schema for PostgreSQL - PART 2
--
-- USAGE INSTRUCTIONS:
-- 1. Make sure you have already run create-trace-database-postgresql-part1.sql
-- 2. Connect to the 'cdcme' database (NOT the 'postgres' database)
-- 3. Run this script to create the tables and schema
--
-- IMPORTANT: You must be connected to the 'cdcme' database when running this script!

-- Create tables
CREATE TABLE
IF NOT EXISTS trace_sessions
(
    session_id UUID PRIMARY KEY DEFAULT gen_random_uuid
(),
    session_name VARCHAR
(255) NOT NULL UNIQUE,
    test_database VARCHAR
(128) NOT NULL,
    snapshot_name VARCHAR
(128),
    start_time TIMESTAMP
WITH TIME ZONE NOT NULL DEFAULT NOW
(),
    end_time TIMESTAMP
WITH TIME ZONE,
    status VARCHAR
(50) NOT NULL DEFAULT 'Active',
    created_by VARCHAR
(128) NOT NULL DEFAULT current_user,
    description TEXT,
    configuration JSONB -- JSON configuration
);

CREATE TABLE
IF NOT EXISTS trace_events
(
    event_id BIGSERIAL PRIMARY KEY,
    session_id UUID NOT NULL REFERENCES trace_sessions
(session_id) ON
DELETE CASCADE,
    event_time TIMESTAMP
WITH TIME ZONE NOT NULL,
    event_name VARCHAR
(128) NOT NULL,
    database_name VARCHAR
(128),
    login_name VARCHAR
(128),
    application_name VARCHAR
(256),
    host_name VARCHAR
(128),
    spid INTEGER,
    duration BIGINT,
    cpu_time BIGINT,
    reads BIGINT,
    writes BIGINT,
    sql_text TEXT,
    execution_order BIGINT NOT NULL,
    is_replayable BOOLEAN NOT NULL DEFAULT true
);

CREATE INDEX
IF NOT EXISTS idx_trace_events_session_execution ON trace_events
(session_id, execution_order);
CREATE INDEX
IF NOT EXISTS idx_trace_events_event_time ON trace_events
(event_time);

-- CDC Capture Headers table (parent)
CREATE TABLE
IF NOT EXISTS cdc_capture_headers
(
    capture_header_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL REFERENCES trace_sessions(session_id) ON DELETE CASCADE,
    capture_name VARCHAR(255) NOT NULL,
    capture_type VARCHAR(50) NOT NULL, -- Baseline, Replay, Optimized
    capture_time TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    tables_enabled JSONB,
    tables_skipped JSONB,
    total_records INTEGER NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'InProgress',
    created_by VARCHAR(128) NOT NULL DEFAULT current_user,
    UNIQUE(session_id, capture_name)
);

CREATE INDEX IF NOT EXISTS idx_cdc_capture_headers_session ON cdc_capture_headers(session_id);
CREATE INDEX IF NOT EXISTS idx_cdc_capture_headers_name ON cdc_capture_headers(capture_name);

-- CDC Captures table (details)
CREATE TABLE
IF NOT EXISTS cdc_captures
(
    capture_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    capture_header_id UUID REFERENCES cdc_capture_headers(capture_header_id) ON DELETE CASCADE,
    session_id UUID NOT NULL REFERENCES trace_sessions(session_id) ON DELETE CASCADE,
    capture_type VARCHAR(50) NOT NULL, -- Baseline, Replay, Optimized
    capture_time TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    table_name VARCHAR(256) NOT NULL,
    capture_data JSONB NOT NULL, -- JSON data
    record_count INTEGER NOT NULL,
    data_hash VARCHAR(64) -- SHA256 hash for quick comparison
);

CREATE INDEX IF NOT EXISTS idx_cdc_captures_header ON cdc_captures(capture_header_id);
CREATE INDEX IF NOT EXISTS idx_cdc_captures_session_type ON cdc_captures(session_id, capture_type);

CREATE TABLE
IF NOT EXISTS comparison_results
(
    comparison_id UUID PRIMARY KEY DEFAULT gen_random_uuid
(),
    session_id UUID NOT NULL REFERENCES trace_sessions
(session_id) ON
DELETE CASCADE,
    left_capture_id UUID
NOT NULL REFERENCES cdc_captures
(capture_id),
    right_capture_id UUID NOT NULL REFERENCES cdc_captures
(capture_id),
    comparison_time TIMESTAMP
WITH TIME ZONE NOT NULL DEFAULT NOW
(),
    table_name VARCHAR
(256) NOT NULL,
    is_match BOOLEAN NOT NULL,
    difference_count INTEGER NOT NULL,
    difference_data JSONB, -- JSON diff data
    comparison_notes TEXT
);

-- Grant permissions to postgres user
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO postgres;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO postgres;

-- Display success message
SELECT 'CDC Trace Database schema created successfully!' as result;