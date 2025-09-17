-- =============================================
-- ERP Test Database Cleanup Script
-- =============================================
-- Removes all ERP test objects created by create-erp-test-objects.sql
-- 
-- Usage: Execute this script against the cdctest database
-- Connection: Uses TEST_DB_CONNECTION environment variable
-- 
-- Objects Removed:
-- - All stored procedures (usp_*)
-- - All triggers (tr_*)
-- - All tables with data and constraints
-- - All indexes
-- 
-- WARNING: This will permanently delete all ERP test data!
-- =============================================

USE [cdctest];
GO

PRINT 'Starting ERP Test Database Cleanup...';
GO

-- =============================================
-- 1. DROP STORED PROCEDURES
-- =============================================

PRINT 'Dropping stored procedures...';

IF EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[usp_CreateSalesOrder]') AND type in (N'P', N'PC'))
BEGIN
    DROP PROCEDURE [dbo].[usp_CreateSalesOrder];
    PRINT '- Dropped usp_CreateSalesOrder';
END

IF EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[usp_DeleteSalesOrder]') AND type in (N'P', N'PC'))
BEGIN
    DROP PROCEDURE [dbo].[usp_DeleteSalesOrder];
    PRINT '- Dropped usp_DeleteSalesOrder';
END

IF EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[usp_InvoiceSalesOrder]') AND type in (N'P', N'PC'))
BEGIN
    DROP PROCEDURE [dbo].[usp_InvoiceSalesOrder];
    PRINT '- Dropped usp_InvoiceSalesOrder';
END

IF EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[usp_PostGlTransaction]') AND type in (N'P', N'PC'))
BEGIN
    DROP PROCEDURE [dbo].[usp_PostGlTransaction];
    PRINT '- Dropped usp_PostGlTransaction';
END

IF EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[usp_GetAccountBalance]') AND type in (N'P', N'PC'))
BEGIN
    DROP PROCEDURE [dbo].[usp_GetAccountBalance];
    PRINT '- Dropped usp_GetAccountBalance';
END

-- =============================================
-- 2. DROP TRIGGERS
-- =============================================

PRINT 'Dropping triggers...';

IF EXISTS (SELECT *
FROM sys.triggers
WHERE object_id = OBJECT_ID(N'[dbo].[tr_ChartOfAccounts_Update]'))
BEGIN
    DROP TRIGGER [dbo].[tr_ChartOfAccounts_Update];
    PRINT '- Dropped tr_ChartOfAccounts_Update';
END

IF EXISTS (SELECT *
FROM sys.triggers
WHERE object_id = OBJECT_ID(N'[dbo].[tr_SalesOrder_Update]'))
BEGIN
    DROP TRIGGER [dbo].[tr_SalesOrder_Update];
    PRINT '- Dropped tr_SalesOrder_Update';
END

IF EXISTS (SELECT *
FROM sys.triggers
WHERE object_id = OBJECT_ID(N'[dbo].[tr_SalesOrderDetail_Update]'))
BEGIN
    DROP TRIGGER [dbo].[tr_SalesOrderDetail_Update];
    PRINT '- Dropped tr_SalesOrderDetail_Update';
END

IF EXISTS (SELECT *
FROM sys.triggers
WHERE object_id = OBJECT_ID(N'[dbo].[tr_ArTransaction_Update]'))
BEGIN
    DROP TRIGGER [dbo].[tr_ArTransaction_Update];
    PRINT '- Dropped tr_ArTransaction_Update';
END

IF EXISTS (SELECT *
FROM sys.triggers
WHERE object_id = OBJECT_ID(N'[dbo].[tr_ArTransactionDetail_Update]'))
BEGIN
    DROP TRIGGER [dbo].[tr_ArTransactionDetail_Update];
    PRINT '- Dropped tr_ArTransactionDetail_Update';
END

IF EXISTS (SELECT *
FROM sys.triggers
WHERE object_id = OBJECT_ID(N'[dbo].[tr_GlTransaction_Update]'))
BEGIN
    DROP TRIGGER [dbo].[tr_GlTransaction_Update];
    PRINT '- Dropped tr_GlTransaction_Update';
END

IF EXISTS (SELECT *
FROM sys.triggers
WHERE object_id = OBJECT_ID(N'[dbo].[tr_GlTransactionDetail_Update]'))
BEGIN
    DROP TRIGGER [dbo].[tr_GlTransactionDetail_Update];
    PRINT '- Dropped tr_GlTransactionDetail_Update';
END

-- =============================================
-- 3. DROP TABLES (in dependency order)
-- =============================================

PRINT 'Dropping tables and their data...';

-- Drop detail tables first (they have foreign keys to header tables)
IF EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[GlTransactionDetail]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[GlTransactionDetail];
    PRINT '- Dropped GlTransactionDetail table';
END

IF EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[ArTransactionDetail]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[ArTransactionDetail];
    PRINT '- Dropped ArTransactionDetail table';
END

IF EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[SalesOrderDetail]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[SalesOrderDetail];
    PRINT '- Dropped SalesOrderDetail table';
END

-- Drop header tables
IF EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[GlTransaction]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[GlTransaction];
    PRINT '- Dropped GlTransaction table';
END

IF EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[ArTransaction]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[ArTransaction];
    PRINT '- Dropped ArTransaction table';
END

IF EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[SalesOrder]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[SalesOrder];
    PRINT '- Dropped SalesOrder table';
END

-- Drop ChartOfAccounts last (it has self-referencing foreign key)
IF EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[ChartOfAccounts]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[ChartOfAccounts];
    PRINT '- Dropped ChartOfAccounts table';
END

-- =============================================
-- 4. CLEANUP VERIFICATION
-- =============================================

PRINT 'Verifying cleanup...';

-- Check for any remaining ERP objects
DECLARE @RemainingTables INT = 0;
DECLARE @RemainingProcs INT = 0;
DECLARE @RemainingTriggers INT = 0;

SELECT @RemainingTables = COUNT(*)
FROM sys.tables
WHERE name IN ('ChartOfAccounts', 'SalesOrder', 'SalesOrderDetail', 'ArTransaction', 'ArTransactionDetail', 'GlTransaction', 'GlTransactionDetail');

SELECT @RemainingProcs = COUNT(*)
FROM sys.procedures
WHERE name LIKE 'usp_%';

SELECT @RemainingTriggers = COUNT(*)
FROM sys.triggers
WHERE name LIKE 'tr_%';

IF @RemainingTables > 0 OR @RemainingProcs > 0 OR @RemainingTriggers > 0
BEGIN
    PRINT 'WARNING: Some objects may not have been dropped:';

    IF @RemainingTables > 0
        PRINT '- Remaining tables: ' + CAST(@RemainingTables AS VARCHAR(10));

    IF @RemainingProcs > 0
        PRINT '- Remaining procedures: ' + CAST(@RemainingProcs AS VARCHAR(10));

    IF @RemainingTriggers > 0
        PRINT '- Remaining triggers: ' + CAST(@RemainingTriggers AS VARCHAR(10));

    -- List remaining objects for manual cleanup
    PRINT '';
    PRINT 'Remaining objects that need manual cleanup:';

                SELECT 'Table' as ObjectType, name as ObjectName
        FROM sys.tables
        WHERE name IN ('ChartOfAccounts', 'SalesOrder', 'SalesOrderDetail', 'ArTransaction', 'ArTransactionDetail', 'GlTransaction', 'GlTransactionDetail')
    UNION ALL
        SELECT 'Procedure' as ObjectType, name as ObjectName
        FROM sys.procedures
        WHERE name LIKE 'usp_%'
    UNION ALL
        SELECT 'Trigger' as ObjectType, name as ObjectName
        FROM sys.triggers
        WHERE name LIKE 'tr_%'
    ORDER BY ObjectType, ObjectName;
END
ELSE
BEGIN
    PRINT 'All ERP test objects have been successfully removed.';
END

-- =============================================
-- 5. DISABLE CDC ON TABLES (if enabled)
-- =============================================

PRINT 'Checking for CDC configuration...';

-- Check if CDC is enabled on the database
DECLARE @CdcEnabled BIT = 0;
SELECT @CdcEnabled = is_cdc_enabled
FROM sys.databases
WHERE name = DB_NAME();

IF @CdcEnabled = 1
BEGIN
    PRINT 'CDC is enabled on database. Checking for CDC capture instances...';

    -- List any CDC capture instances that might still exist for our tables
    IF EXISTS (SELECT 1
    FROM cdc.change_tables
    WHERE source_schema = 'dbo'
        AND source_name IN ('ChartOfAccounts', 'SalesOrder', 'SalesOrderDetail', 'ArTransaction', 'ArTransactionDetail', 'GlTransaction', 'GlTransactionDetail'))
    BEGIN
        PRINT 'WARNING: CDC capture instances still exist for ERP tables.';
        PRINT 'You may need to disable CDC on these tables manually using:';
        PRINT 'EXEC sys.sp_cdc_disable_table @source_schema = N''dbo'', @source_name = N''TableName'', @capture_instance = N''dbo_TableName'';';
        PRINT '';

        SELECT
            'EXEC sys.sp_cdc_disable_table @source_schema = N''dbo'', @source_name = N''' + source_name + ''', @capture_instance = N''' + capture_instance + ''';' as DisableCdcCommand
        FROM cdc.change_tables
        WHERE source_schema = 'dbo'
            AND source_name IN ('ChartOfAccounts', 'SalesOrder', 'SalesOrderDetail', 'ArTransaction', 'ArTransactionDetail', 'GlTransaction', 'GlTransactionDetail');
    END
    ELSE
    BEGIN
        PRINT 'No CDC capture instances found for ERP tables.';
    END
END
ELSE
BEGIN
    PRINT 'CDC is not enabled on this database.';
END

-- =============================================
-- 6. FINAL SUMMARY
-- =============================================

PRINT '';
PRINT '=============================================';
PRINT 'ERP Test Database Cleanup Complete!';
PRINT '=============================================';
PRINT '';
PRINT 'Objects Removed:';
PRINT '- 5 Stored procedures';
PRINT '- 7 Update triggers';
PRINT '- 7 Tables with all data and constraints';
PRINT '- All associated indexes';
PRINT '';
PRINT 'The database is now clean and ready for:';
PRINT '1. Fresh ERP test object creation';
PRINT '2. Other testing scenarios';
PRINT '3. Different CDC testing configurations';
PRINT '';

-- Optional: Show current database object counts for reference
SELECT
    'Current Database Objects' as Summary,
    (SELECT COUNT(*)
    FROM sys.tables
    WHERE schema_id = SCHEMA_ID('dbo')) as UserTables,
    (SELECT COUNT(*)
    FROM sys.procedures
    WHERE schema_id = SCHEMA_ID('dbo')) as UserProcedures,
    (SELECT COUNT(*)
    FROM sys.triggers) as UserTriggers;

PRINT 'Cleanup script execution completed successfully.';
GO