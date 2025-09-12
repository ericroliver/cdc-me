-- Create trace database and schema for PostgreSQL
-- Connection: Host=blue.local;Database=postgres;Username=postgres;Password=A123_Z321!

-- Create trace database if it doesn't exist
SELECT 'CREATE DATABASE cdc_tracedb'
WHERE NOT EXISTS (SELECT
FROM pg_database
WHERE datname = 'cdc_tracedb')
\gexec

-- Connect to the trace database
\c cdc_tracedb;

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
    test_connection_string VARCHAR
(1000) NOT NULL,
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

CREATE TABLE
IF NOT EXISTS cdc_captures
(
    capture_id UUID PRIMARY KEY DEFAULT gen_random_uuid
(),
    session_id UUID NOT NULL REFERENCES trace_sessions
(session_id) ON
DELETE CASCADE,
    capture_type VARCHAR(50)
NOT NULL, -- Baseline, Replay, Optimized
    capture_time TIMESTAMP
WITH TIME ZONE NOT NULL DEFAULT NOW
(),
    table_name VARCHAR
(256) NOT NULL,
    capture_data JSONB NOT NULL, -- JSON data
    record_count INTEGER NOT NULL,
    data_hash VARCHAR
(64) -- SHA256 hash for quick comparison
);

CREATE INDEX
IF NOT EXISTS idx_cdc_captures_session_type ON cdc_captures
(session_id, capture_type);

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