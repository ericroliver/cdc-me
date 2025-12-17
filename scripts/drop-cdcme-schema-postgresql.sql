-- Drop cdcme database schema tables (PostgreSQL)
-- This script will remove all trace-related tables from the cdcme database
--
-- USAGE INSTRUCTIONS:
-- 1. Connect to the 'cdcme' database
-- 2. Run this script to drop all tables
-- 3. Then run create-trace-database-postgresql-part2.sql to recreate with new schema
--
-- WARNING: This will delete ALL data in the trace tables!

-- Drop tables in correct order (child tables first due to foreign keys)
DROP TABLE IF EXISTS comparison_results CASCADE;
DROP TABLE IF EXISTS cdc_captures CASCADE;
DROP TABLE IF EXISTS cdc_capture_headers CASCADE;
DROP TABLE IF EXISTS trace_events CASCADE;
DROP TABLE IF EXISTS trace_sessions CASCADE;

-- Display success message
SELECT 'All cdcme schema tables dropped successfully!' as result;
SELECT 'You can now run create-trace-database-postgresql-part2.sql to recreate the schema' as next_step;