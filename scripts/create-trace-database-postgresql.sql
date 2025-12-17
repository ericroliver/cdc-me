-- Create trace database and schema for PostgreSQL
--
-- USAGE INSTRUCTIONS:
-- 1. Connect to your PostgreSQL server using the 'postgres' database
-- 2. Run the CREATE DATABASE section of this script
-- 3. Connect to the newly created 'cdcme' database
-- 4. Run the rest of this script to create tables and schema
--
-- IMPORTANT: This script requires a connection string environment variable.
-- Create a .env file in the project root with:
-- POSTGRES_CONNECTION_STRING=Host=your-host;Database=cdcme;Username=your-username;Password=your-password

-- ========================================
-- PART 1: CREATE DATABASE (Run while connected to 'postgres' database)
-- ========================================
-- STOP HERE after running this section and connect to 'cdcme' database before continuing

CREATE DATABASE cdcme
    WITH
    OWNER = postgres
    ENCODING = 'UTF8'
    LC_COLLATE = 'en_US.utf8'
    LC_CTYPE = 'en_US.utf8'
    TABLESPACE = pg_default
    CONNECTION LIMIT = -1;

-- ========================================
-- IMPORTANT: Now connect to the 'cdcme' database before running the rest!
-- In DBeaver: Right-click server -> Create -> Connection -> Use 'cdcme' as database name
-- In psql: \c cdcme
-- ========================================

-- ========================================
-- PART 2: CREATE TABLES AND SCHEMA (Run while connected to 'cdcme' database)
-- ========================================

-- Create trace_sessions table
CREATE TABLE IF NOT EXISTS trace_sessions
(
    session_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_name VARCHAR(255) NOT NULL UNIQUE,
    test_database VARCHAR(128) NOT NULL,
    snapshot_name VARCHAR(128),
    start_time TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    end_time TIMESTAMP WITH TIME ZONE,
    status VARCHAR(50) NOT NULL DEFAULT 'Active',
    created_by VARCHAR(128) NOT NULL DEFAULT current_user,
    description TEXT,
    configuration JSONB
);

-- Create trace_events table
CREATE TABLE IF NOT EXISTS trace_events
(
    event_id BIGSERIAL PRIMARY KEY,
    session_id UUID NOT NULL REFERENCES trace_sessions(session_id) ON DELETE CASCADE,
    event_time TIMESTAMP WITH TIME ZONE NOT NULL,
    event_name VARCHAR(128) NOT NULL,
    database_name VARCHAR(128),
    login_name VARCHAR(128),
    application_name VARCHAR(256),
    host_name VARCHAR(128),
    spid INTEGER,
    duration BIGINT,
    cpu_time BIGINT,
    reads BIGINT,
    writes BIGINT,
    sql_text TEXT,
    execution_order BIGINT NOT NULL,
    is_replayable BOOLEAN NOT NULL DEFAULT true
);

CREATE INDEX IF NOT EXISTS idx_trace_events_session_execution ON trace_events(session_id, execution_order);
CREATE INDEX IF NOT EXISTS idx_trace_events_event_time ON trace_events(event_time);

-- Create cdc_capture_headers table (header for capture operations)
CREATE TABLE IF NOT EXISTS cdc_capture_headers
(
    capture_header_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL REFERENCES trace_sessions(session_id) ON DELETE CASCADE,
    capture_name VARCHAR(255) NOT NULL,
    capture_type VARCHAR(50) NOT NULL,
    capture_time TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    tables_to_include JSONB,
    tables_to_exclude JSONB,
    tables_enabled JSONB NOT NULL,
    tables_skipped JSONB,
    total_records INTEGER NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'Completed',
    error_messages JSONB,
    created_by VARCHAR(128) NOT NULL DEFAULT current_user,
    description TEXT
);

CREATE INDEX IF NOT EXISTS idx_cdc_capture_headers_session ON cdc_capture_headers(session_id);
CREATE INDEX IF NOT EXISTS idx_cdc_capture_headers_capture_type ON cdc_capture_headers(capture_type);

-- Create cdc_captures table (detail records for captured CDC data)
CREATE TABLE IF NOT EXISTS cdc_captures
(
    capture_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    capture_header_id UUID NOT NULL REFERENCES cdc_capture_headers(capture_header_id) ON DELETE CASCADE,
    table_name VARCHAR(256) NOT NULL,
    capture_data JSONB NOT NULL,
    record_count INTEGER NOT NULL,
    data_hash VARCHAR(64)
);

CREATE INDEX IF NOT EXISTS idx_cdc_captures_header ON cdc_captures(capture_header_id);
CREATE INDEX IF NOT EXISTS idx_cdc_captures_table_name ON cdc_captures(table_name);

-- Create comparison_results table
CREATE TABLE IF NOT EXISTS comparison_results
(
    comparison_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL REFERENCES trace_sessions(session_id) ON DELETE CASCADE,
    left_capture_id UUID NOT NULL REFERENCES cdc_captures(capture_id),
    right_capture_id UUID NOT NULL REFERENCES cdc_captures(capture_id),
    comparison_time TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    table_name VARCHAR(256) NOT NULL,
    is_match BOOLEAN NOT NULL,
    difference_count INTEGER NOT NULL,
    difference_data JSONB,
    comparison_notes TEXT
);

CREATE INDEX IF NOT EXISTS idx_comparison_results_session ON comparison_results(session_id);
CREATE INDEX IF NOT EXISTS idx_comparison_results_captures ON comparison_results(left_capture_id, right_capture_id);

-- Grant permissions to postgres user
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO postgres;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO postgres;

-- Display success message
SELECT 'CDC Trace Database schema created successfully!' as result;