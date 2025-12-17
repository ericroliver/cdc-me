-- Rebuild cdcme database schema (PostgreSQL)
-- This script will drop and recreate all trace-related tables with the updated schema
--
-- USAGE INSTRUCTIONS:
-- 1. Connect to the 'cdcme' database
-- 2. Run this script to drop old tables and create new ones
--
-- WARNING: This will delete ALL existing data in the trace tables!

-- ========================================
-- STEP 1: Drop existing tables
-- ========================================

DROP TABLE IF EXISTS comparison_results CASCADE;
DROP TABLE IF EXISTS cdc_captures CASCADE;
DROP TABLE IF EXISTS cdc_capture_headers CASCADE;
DROP TABLE IF EXISTS trace_events CASCADE;
DROP TABLE IF EXISTS trace_sessions CASCADE;

SELECT 'Old schema dropped successfully' as step1_result;

-- ========================================
-- STEP 2: Create tables with new schema
-- ========================================

-- Create trace_sessions table
CREATE TABLE trace_sessions
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
CREATE TABLE trace_events
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

CREATE INDEX idx_trace_events_session_execution ON trace_events(session_id, execution_order);
CREATE INDEX idx_trace_events_event_time ON trace_events(event_time);

-- Create cdc_capture_headers table (header for capture operations)
CREATE TABLE cdc_capture_headers
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

CREATE INDEX idx_cdc_capture_headers_session ON cdc_capture_headers(session_id);
CREATE INDEX idx_cdc_capture_headers_capture_type ON cdc_capture_headers(capture_type);

-- Create cdc_captures table (detail records for captured CDC data)
CREATE TABLE cdc_captures
(
    capture_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    capture_header_id UUID NOT NULL REFERENCES cdc_capture_headers(capture_header_id) ON DELETE CASCADE,
    table_name VARCHAR(256) NOT NULL,
    capture_data JSONB NOT NULL,
    record_count INTEGER NOT NULL,
    data_hash VARCHAR(64)
);

CREATE INDEX idx_cdc_captures_header ON cdc_captures(capture_header_id);
CREATE INDEX idx_cdc_captures_table_name ON cdc_captures(table_name);

-- Create comparison_results table
CREATE TABLE comparison_results
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

CREATE INDEX idx_comparison_results_session ON comparison_results(session_id);
CREATE INDEX idx_comparison_results_captures ON comparison_results(left_capture_id, right_capture_id);

-- Grant permissions to postgres user
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO postgres;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO postgres;

-- Display success message
SELECT 'CDC Trace Database schema rebuilt successfully!' as final_result;
SELECT 'Schema now includes cdc_capture_headers table with proper structure' as note;