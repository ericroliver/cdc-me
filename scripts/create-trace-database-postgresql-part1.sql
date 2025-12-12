-- Create trace database for PostgreSQL - PART 1
--
-- USAGE INSTRUCTIONS:
-- 1. Connect to your PostgreSQL server using the 'postgres' database
-- 2. Run this script to create the 'cdcme' database
-- 3. Then run create-trace-database-postgresql-part2.sql while connected to 'cdcme'
--
-- IMPORTANT: This script requires a connection string environment variable.
-- Create a .env file in the project root with:
-- POSTGRES_CONNECTION_STRING=Host=your-host;Database=cdcme;Username=your-username;Password=your-password

-- Create trace database
CREATE DATABASE cdcme
    WITH
    OWNER = postgres
    ENCODING = 'UTF8'
    LC_COLLATE = 'en_US.utf8'
    LC_CTYPE = 'en_US.utf8'
    TABLESPACE = pg_default
    CONNECTION LIMIT = -1;

-- Display success message
SELECT 'CDC Trace Database created successfully! Now connect to the cdcme database and run part 2.' as result;