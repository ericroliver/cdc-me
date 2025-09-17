-- =============================================
-- ERP Sample Data Initialization Script
-- =============================================
-- Inserts sample data for testing ERP workflows
-- Run this after create-erp-database-objects.sql
-- 
-- Usage: Execute this script against the cdctest database
-- Connection: Uses TEST_DB_CONNECTION environment variable
-- 
-- Data Inserted:
-- - Chart of Accounts (basic GL account structure)
-- - Sample Sales Orders with line items
-- =============================================

USE [cdctest];
GO

PRINT 'Starting ERP Sample Data Initialization...';
GO

-- =============================================
-- 1. INSERT CHART OF ACCOUNTS
-- =============================================

PRINT 'Inserting Chart of Accounts data...';

-- Insert Chart of Accounts (only if empty)
IF NOT EXISTS (SELECT 1
FROM [dbo].[ChartOfAccounts])
BEGIN
    INSERT INTO [dbo].[ChartOfAccounts]
        ([AccountNumber], [AccountName], [AccountType], [ParentAccountId])
    VALUES
        ('1000', 'Cash and Cash Equivalents', 'Asset', NULL),
        ('1200', 'Accounts Receivable', 'Asset', NULL),
        ('1300', 'Inventory', 'Asset', NULL),
        ('1400', 'Prepaid Expenses', 'Asset', NULL),
        ('2000', 'Accounts Payable', 'Liability', NULL),
        ('2100', 'Accrued Expenses', 'Liability', NULL),
        ('3000', 'Retained Earnings', 'Equity', NULL),
        ('3100', 'Common Stock', 'Equity', NULL),
        ('4000', 'Sales Revenue', 'Revenue', NULL),
        ('4100', 'Service Revenue', 'Revenue', NULL),
        ('5000', 'Cost of Goods Sold', 'Expense', NULL),
        ('6000', 'Operating Expenses', 'Expense', NULL);

    PRINT 'Chart of Accounts inserted: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' records';
END
ELSE
BEGIN
    PRINT 'Chart of Accounts already exists, skipping...';
END

-- =============================================
-- 2. INSERT SAMPLE SALES ORDERS
-- =============================================

PRINT 'Inserting sample Sales Order data...';

-- Insert Sample Sales Orders (only if empty)
IF NOT EXISTS (SELECT 1
FROM [dbo].[SalesOrder])
BEGIN
    INSERT INTO [dbo].[SalesOrder]
        ([OrderNumber], [CustomerName], [CustomerEmail], [RequiredDate], [SubTotal], [TaxAmount], [TotalAmount], [Notes], [CreatedBy])
    VALUES
        ('SO-2024-001', 'Acme Corporation', 'orders@acme.com', DATEADD(day, 7, GETDATE()), 1500.00, 120.00, 1620.00, 'Rush order for Q1 delivery', 'TestUser'),
        ('SO-2024-002', 'Beta Industries', 'purchasing@beta.com', DATEADD(day, 14, GETDATE()), 2750.00, 220.00, 2970.00, 'Standard delivery terms', 'TestUser'),
        ('SO-2024-003', 'Gamma Solutions', 'orders@gamma.com', DATEADD(day, 10, GETDATE()), 980.50, 78.44, 1058.94, 'Net 30 payment terms', 'TestUser'),
        ('SO-2024-004', 'Delta Systems', 'procurement@delta.com', DATEADD(day, 21, GETDATE()), 3200.00, 256.00, 3456.00, 'Volume discount applied', 'TestUser'),
        ('SO-2024-005', 'Epsilon Corp', 'buying@epsilon.com', DATEADD(day, 5, GETDATE()), 1875.75, 150.06, 2025.81, 'Express shipping required', 'TestUser');

    PRINT 'Sales Orders inserted: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' records';

    -- Get the inserted SalesOrderIds for detail records
    DECLARE @SalesOrderId1 INT = (SELECT SalesOrderId
    FROM [dbo].[SalesOrder]
    WHERE OrderNumber = 'SO-2024-001');
    DECLARE @SalesOrderId2 INT = (SELECT SalesOrderId
    FROM [dbo].[SalesOrder]
    WHERE OrderNumber = 'SO-2024-002');
    DECLARE @SalesOrderId3 INT = (SELECT SalesOrderId
    FROM [dbo].[SalesOrder]
    WHERE OrderNumber = 'SO-2024-003');
    DECLARE @SalesOrderId4 INT = (SELECT SalesOrderId
    FROM [dbo].[SalesOrder]
    WHERE OrderNumber = 'SO-2024-004');
    DECLARE @SalesOrderId5 INT = (SELECT SalesOrderId
    FROM [dbo].[SalesOrder]
    WHERE OrderNumber = 'SO-2024-005');

    -- Insert Sales Order Details
    INSERT INTO [dbo].[SalesOrderDetail]
        ([SalesOrderId], [LineNumber], [ProductCode], [ProductDescription], [Quantity], [UnitPrice])
    VALUES
        -- Order 1 Details
        (@SalesOrderId1, 1, 'WIDGET-A', 'Premium Widget Type A', 10.0000, 75.0000),
        (@SalesOrderId1, 2, 'WIDGET-B', 'Standard Widget Type B', 15.0000, 50.0000),
        (@SalesOrderId1, 3, 'SERVICE-INSTALL', 'Installation Service', 1.0000, 500.0000),

        -- Order 2 Details
        (@SalesOrderId2, 1, 'WIDGET-PRO', 'Professional Widget Suite', 5.0000, 200.0000),
        (@SalesOrderId2, 2, 'WIDGET-A', 'Premium Widget Type A', 20.0000, 75.0000),
        (@SalesOrderId2, 3, 'SERVICE-SUPPORT', 'Annual Support Package', 1.0000, 250.0000),

        -- Order 3 Details
        (@SalesOrderId3, 1, 'WIDGET-B', 'Standard Widget Type B', 12.0000, 50.0000),
        (@SalesOrderId3, 2, 'ACCESSORY-1', 'Widget Accessory Kit', 8.0000, 35.0625),

        -- Order 4 Details
        (@SalesOrderId4, 1, 'WIDGET-ENTERPRISE', 'Enterprise Widget Solution', 2.0000, 800.0000),
        (@SalesOrderId4, 2, 'WIDGET-PRO', 'Professional Widget Suite', 8.0000, 200.0000),
        (@SalesOrderId4, 3, 'SERVICE-PREMIUM', 'Premium Support Package', 1.0000, 800.0000),

        -- Order 5 Details
        (@SalesOrderId5, 1, 'WIDGET-A', 'Premium Widget Type A', 25.0000, 75.0000),
        (@SalesOrderId5, 2, 'SERVICE-EXPRESS', 'Express Delivery Service', 1.0000, 125.75);

    PRINT 'Sales Order Details inserted: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' records';
END
ELSE
BEGIN
    PRINT 'Sales Orders already exist, skipping...';
END

-- =============================================
-- 3. DISPLAY SUMMARY
-- =============================================

PRINT 'Displaying data summary...';

-- Display sample data counts
    SELECT
        'ChartOfAccounts' as TableName, COUNT(*) as RecordCount
    FROM [dbo].[ChartOfAccounts]
UNION ALL
    SELECT
        'SalesOrder' as TableName, COUNT(*) as RecordCount
    FROM [dbo].[SalesOrder]
UNION ALL
    SELECT
        'SalesOrderDetail' as TableName, COUNT(*) as RecordCount
    FROM [dbo].[SalesOrderDetail]
ORDER BY TableName;

PRINT '';
PRINT '=============================================';
PRINT 'ERP Sample Data Initialization Complete!';
PRINT '=============================================';
PRINT '';
PRINT 'Sample Data Inserted:';
PRINT '- Chart of Accounts: Basic GL account structure';
PRINT '- Sales Orders: 5 sample orders with line items';
PRINT '';
PRINT 'Next Steps:';
PRINT '1. Enable CDC on all tables using the CDC Testing Framework';
PRINT '2. Run simulate-erp-business-workflow.sql for testing scenarios';
PRINT '3. Use cleanup-erp-test-objects.sql to remove all objects when done';
PRINT '';

GO