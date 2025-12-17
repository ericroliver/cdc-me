-- Add cdc_capture_headers table to cdcme database
-- This table is required by the API endpoints but was missing from the original schema
--
-- USAGE INSTRUCTIONS:
-- 1. Connect to the 'cdcme' database
-- 2. Run this script to create the cdc_capture_headers table
-- 3. Run the ALTER TABLE statement to update cdc_captures schema

-- Create cdc_capture_headers table
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

CREATE INDEX IF NOT EXISTS idx_cdc_capture_headers_session 
    ON cdc_capture_headers(session_id);
CREATE INDEX IF NOT EXISTS idx_cdc_capture_headers_capture_type
    ON cdc_capture_headers(capture_type);

-- Check if cdc_captures already has capture_header_id column
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'cdc_captures' 
        AND column_name = 'capture_header_id'
    ) THEN
        -- Add capture_header_id column to cdc_captures
        ALTER TABLE cdc_captures 
            ADD COLUMN capture_header_id UUID NOT NULL REFERENCES cdc_capture_headers(capture_header_id) ON DELETE CASCADE;
        
        -- Create index on the new column
        CREATE INDEX idx_cdc_captures_header ON cdc_captures(capture_header_id);
        
        RAISE NOTICE 'Added capture_header_id column to cdc_captures table';
    ELSE
        RAISE NOTICE 'capture_header_id column already exists in cdc_captures table';
    END IF;
    
    -- Remove old columns if they exist (session_id, capture_type, capture_time)
    IF EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'cdc_captures' 
        AND column_name = 'session_id'
    ) THEN
        ALTER TABLE cdc_captures DROP COLUMN IF EXISTS session_id;
        RAISE NOTICE 'Removed session_id column from cdc_captures table';
    END IF;
    
    IF EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'cdc_captures' 
        AND column_name = 'capture_type'
    ) THEN
        ALTER TABLE cdc_captures DROP COLUMN IF EXISTS capture_type;
        RAISE NOTICE 'Removed capture_type column from cdc_captures table';
    END IF;
    
    IF EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'cdc_captures' 
        AND column_name = 'capture_time'
    ) THEN
        ALTER TABLE cdc_captures DROP COLUMN IF EXISTS capture_time;
        RAISE NOTICE 'Removed capture_time column from cdc_captures table';
    END IF;
END $$;

-- Grant permissions
GRANT ALL PRIVILEGES ON TABLE cdc_capture_headers TO postgres;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO postgres;

-- Display success message
SELECT 'cdc_capture_headers table created/updated successfully!' as result;