-- =============================================
-- ERP Business Workflow Simulation Script
-- =============================================
-- Simulates realistic business operations for CDC testing
-- Includes Order Management, AR, and GL workflows
-- 
-- Usage: Execute this script against the cdctest database after running:
--   1. create-erp-database-objects.sql
--   2. initialize-erp-sample-data.sql
-- 
-- This script can be run multiple times with database state restoration between
-- runs to test CDC capture and replay functionality
-- 
-- Workflow Simulated:
-- 1. Create new sales orders with line items
-- 2. Invoice sales orders (creates AR and GL transactions)
-- 3. Post GL transactions to the general ledger
-- 4. Process customer payments (AR adjustments)
-- 5. Handle order modifications and cancellations
-- 6. Generate account balance reports
-- =============================================


PRINT 'Starting ERP Business Workflow Simulation...';
PRINT 'Timestamp: ' + CONVERT(VARCHAR(23), GETDATE(), 121);

-- =============================================
-- ENSURE CHART OF ACCOUNTS EXISTS
-- =============================================
PRINT '';
PRINT 'Ensuring Chart of Accounts is initialized...';

-- Insert Chart of Accounts if it doesn't exist
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

    PRINT 'Chart of Accounts initialized with ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' accounts';
END
ELSE
BEGIN
    PRINT 'Chart of Accounts already exists';
END

-- =============================================
-- VARIABLE DECLARATIONS FOR ENTIRE SCRIPT
-- =============================================
-- Declare all variables at the top to ensure proper scope throughout the script

-- Sales Order variables
DECLARE @NewOrderId1 INT, @NewOrderNumber1 VARCHAR(20);
DECLARE @NewOrderId2 INT, @NewOrderNumber2 VARCHAR(20);
DECLARE @NewOrderId3 INT, @NewOrderNumber3 VARCHAR(20);

-- AR and GL transaction variables
DECLARE @ArId1 INT, @GlId1 INT;
DECLARE @ArId2 INT, @GlId2 INT;
DECLARE @ArId3 INT, @GlId3 INT;

-- Payment variables
DECLARE @PaymentArId1 INT, @PaymentGlId1 INT;
DECLARE @PaymentArId2 INT, @PaymentGlId2 INT;

-- Account balance variables
DECLARE @CashBalance DECIMAL(18,2);
DECLARE @ArBalance DECIMAL(18,2);
DECLARE @SalesBalance DECIMAL(18,2);

-- =============================================
-- SCENARIO 1: CREATE NEW SALES ORDERS
-- =============================================

PRINT '';
PRINT '=== SCENARIO 1: Creating New Sales Orders ===';

-- Create Order 1: Technology Company
EXEC [dbo].[usp_CreateSalesOrder]
    @CustomerName = 'TechCorp Solutions',
    @CustomerEmail = 'orders@techcorp.com',
    @RequiredDate = '2024-12-31',
    @Notes = 'Year-end technology upgrade project',
    @CreatedBy = 'SimulationUser',
    @SalesOrderId = @NewOrderId1 OUTPUT,
    @OrderNumber = @NewOrderNumber1 OUTPUT;

-- Add line items to Order 1
INSERT INTO [dbo].[SalesOrderDetail]
    ([SalesOrderId], [LineNumber], [ProductCode], [ProductDescription], [Quantity], [UnitPrice])
VALUES
    (@NewOrderId1, 1, 'WIDGET-ENTERPRISE', 'Enterprise Widget Solution', 3.0000, 800.0000),
    (@NewOrderId1, 2, 'SERVICE-PREMIUM', 'Premium Support Package', 1.0000, 1200.0000),
    (@NewOrderId1, 3, 'TRAINING-BASIC', 'Basic Training Package', 5.0000, 150.0000);

-- Update order totals for Order 1
UPDATE [dbo].[SalesOrder]
SET SubTotal = 4350.00,
    TaxAmount = 348.00,
    TotalAmount = 4698.00
WHERE SalesOrderId = @NewOrderId1;

-- Create Order 2: Manufacturing Company
EXEC [dbo].[usp_CreateSalesOrder]
    @CustomerName = 'Industrial Manufacturing Inc',
    @CustomerEmail = 'procurement@industrial.com',
    @RequiredDate = '2024-11-15',
    @Notes = 'Production line enhancement',
    @CreatedBy = 'SimulationUser',
    @SalesOrderId = @NewOrderId2 OUTPUT,
    @OrderNumber = @NewOrderNumber2 OUTPUT;

-- Add line items to Order 2
INSERT INTO [dbo].[SalesOrderDetail]
    ([SalesOrderId], [LineNumber], [ProductCode], [ProductDescription], [Quantity], [UnitPrice])
VALUES
    (@NewOrderId2, 1, 'WIDGET-PRO', 'Professional Widget Suite', 10.0000, 200.0000),
    (@NewOrderId2, 2, 'WIDGET-A', 'Premium Widget Type A', 50.0000, 75.0000),
    (@NewOrderId2, 3, 'SERVICE-INSTALL', 'Installation Service', 2.0000, 500.0000);

-- Update order totals for Order 2
UPDATE [dbo].[SalesOrder]
SET SubTotal = 6750.00,
    TaxAmount = 540.00,
    TotalAmount = 7290.00
WHERE SalesOrderId = @NewOrderId2;

-- Create Order 3: Small Business
EXEC [dbo].[usp_CreateSalesOrder]
    @CustomerName = 'Small Business Solutions LLC',
    @CustomerEmail = 'admin@smallbiz.com',
    @RequiredDate = '2024-10-30',
    @Notes = 'Starter package for new business',
    @CreatedBy = 'SimulationUser',
    @SalesOrderId = @NewOrderId3 OUTPUT,
    @OrderNumber = @NewOrderNumber3 OUTPUT;

-- Add line items to Order 3
INSERT INTO [dbo].[SalesOrderDetail]
    ([SalesOrderId], [LineNumber], [ProductCode], [ProductDescription], [Quantity], [UnitPrice])
VALUES
    (@NewOrderId3, 1, 'WIDGET-B', 'Standard Widget Type B', 8.0000, 50.0000),
    (@NewOrderId3, 2, 'ACCESSORY-1', 'Widget Accessory Kit', 4.0000, 35.0625),
    (@NewOrderId3, 3, 'SERVICE-SUPPORT', 'Annual Support Package', 1.0000, 250.0000);

-- Update order totals for Order 3
UPDATE [dbo].[SalesOrder]
SET SubTotal = 790.25,
    TaxAmount = 63.22,
    TotalAmount = 853.47
WHERE SalesOrderId = @NewOrderId3;

PRINT 'Created 3 new sales orders with line items';

-- =============================================
-- SCENARIO 2: INVOICE EXISTING SALES ORDERS
-- =============================================

PRINT '';
PRINT '=== SCENARIO 2: Invoicing Sales Orders ===';

-- Get some existing orders to invoice
DECLARE @ExistingOrderId1 INT = (SELECT TOP 1
    SalesOrderId
FROM [dbo].[SalesOrder]
WHERE OrderStatus = 'Open' AND OrderNumber LIKE 'SO-2024-%'
ORDER BY SalesOrderId);
DECLARE @ExistingOrderId2 INT = (SELECT TOP 1
    SalesOrderId
FROM [dbo].[SalesOrder]
WHERE OrderStatus = 'Open' AND SalesOrderId > @ExistingOrderId1
ORDER BY SalesOrderId);

-- Variables already declared at script top

-- Verify Chart of Accounts exists
DECLARE @ArAccountExists BIT = 0, @SalesAccountExists BIT = 0;
IF EXISTS (SELECT 1
FROM [dbo].[ChartOfAccounts]
WHERE AccountNumber = '1200')
    SET @ArAccountExists = 1;
IF EXISTS (SELECT 1
FROM [dbo].[ChartOfAccounts]
WHERE AccountNumber = '4000')
    SET @SalesAccountExists = 1;

PRINT 'Chart of Accounts validation: AR Account (1200) exists: ' + CASE WHEN @ArAccountExists = 1 THEN 'YES' ELSE 'NO' END;
PRINT 'Chart of Accounts validation: Sales Account (4000) exists: ' + CASE WHEN @SalesAccountExists = 1 THEN 'YES' ELSE 'NO' END;

-- Invoice the first existing order
IF @ExistingOrderId1 IS NOT NULL
BEGIN
    PRINT 'Invoicing existing order ID: ' + CAST(@ExistingOrderId1 AS VARCHAR(10));
    EXEC [dbo].[usp_InvoiceSalesOrder]
        @SalesOrderId = @ExistingOrderId1,
        @InvoiceDate = NULL,
        @DueDate = NULL,
        @CreatedBy = 'SimulationUser',
        @ArTransactionId = @ArId1 OUTPUT,
        @GlTransactionId = @GlId1 OUTPUT;
END
ELSE
BEGIN
    PRINT 'No existing order found for @ExistingOrderId1';
END

-- Invoice the second existing order
IF @ExistingOrderId2 IS NOT NULL
BEGIN
    PRINT 'Invoicing existing order ID: ' + CAST(@ExistingOrderId2 AS VARCHAR(10));
    EXEC [dbo].[usp_InvoiceSalesOrder]
        @SalesOrderId = @ExistingOrderId2,
        @InvoiceDate = NULL,
        @DueDate = NULL,
        @CreatedBy = 'SimulationUser',
        @ArTransactionId = @ArId2 OUTPUT,
        @GlTransactionId = @GlId2 OUTPUT;
END
ELSE
BEGIN
    PRINT 'No existing order found for @ExistingOrderId2';
END

-- Invoice one of the newly created orders
PRINT 'Invoicing newly created order ID: ' + CAST(@NewOrderId1 AS VARCHAR(10));
EXEC [dbo].[usp_InvoiceSalesOrder]
    @SalesOrderId = @NewOrderId1,
    @InvoiceDate = NULL,
    @DueDate = NULL,
    @CreatedBy = 'SimulationUser',
    @ArTransactionId = @ArId3 OUTPUT,
    @GlTransactionId = @GlId3 OUTPUT;

PRINT 'Invoiced multiple sales orders, created AR and GL transactions';
PRINT 'GL Transaction IDs created: @GlId1=' + ISNULL(CAST(@GlId1 AS VARCHAR(10)), 'NULL') +
      ', @GlId2=' + ISNULL(CAST(@GlId2 AS VARCHAR(10)), 'NULL') +
      ', @GlId3=' + ISNULL(CAST(@GlId3 AS VARCHAR(10)), 'NULL');

-- =============================================
-- SCENARIO 3: POST GL TRANSACTIONS
-- =============================================

PRINT '';
PRINT '=== SCENARIO 3: Posting GL Transactions ===';

-- Post the GL transactions we just created
IF @GlId1 IS NOT NULL
BEGIN
    BEGIN TRY
        PRINT 'Posting GL Transaction ID: ' + CAST(@GlId1 AS VARCHAR(10));
        EXEC [dbo].[usp_PostGlTransaction]
            @GlTransactionId = @GlId1,
            @PostingDate = NULL,
            @PostedBy = 'SimulationUser';
    END TRY
    BEGIN CATCH
        PRINT 'Error posting GL Transaction ID ' + CAST(@GlId1 AS VARCHAR(10)) + ': ' + ERROR_MESSAGE();
    END CATCH
END

IF @GlId2 IS NOT NULL
BEGIN
    BEGIN TRY
        PRINT 'Posting GL Transaction ID: ' + CAST(@GlId2 AS VARCHAR(10));
        EXEC [dbo].[usp_PostGlTransaction]
            @GlTransactionId = @GlId2,
            @PostingDate = NULL,
            @PostedBy = 'SimulationUser';
    END TRY
    BEGIN CATCH
        PRINT 'Error posting GL Transaction ID ' + CAST(@GlId2 AS VARCHAR(10)) + ': ' + ERROR_MESSAGE();
    END CATCH
END

IF @GlId3 IS NOT NULL
BEGIN
    BEGIN TRY
        PRINT 'Posting GL Transaction ID: ' + CAST(@GlId3 AS VARCHAR(10));
        EXEC [dbo].[usp_PostGlTransaction]
            @GlTransactionId = @GlId3,
            @PostingDate = NULL,
            @PostedBy = 'SimulationUser';
    END TRY
    BEGIN CATCH
        PRINT 'Error posting GL Transaction ID ' + CAST(@GlId3 AS VARCHAR(10)) + ': ' + ERROR_MESSAGE();
    END CATCH
END

-- Post any other unposted GL transactions
DECLARE @UnpostedGlId INT;
DECLARE gl_cursor CURSOR FOR
    SELECT GlTransactionId
FROM [dbo].[GlTransaction]
WHERE IsPosted = 0;

OPEN gl_cursor;
FETCH NEXT FROM gl_cursor INTO @UnpostedGlId;

WHILE @@FETCH_STATUS = 0
BEGIN
    -- Validate the GL transaction exists before posting
    IF EXISTS (SELECT 1
    FROM [dbo].[GlTransaction]
    WHERE GlTransactionId = @UnpostedGlId)
    BEGIN
        BEGIN TRY
            PRINT 'Posting additional GL Transaction ID: ' + CAST(@UnpostedGlId AS VARCHAR(10));
            EXEC [dbo].[usp_PostGlTransaction]
                @GlTransactionId = @UnpostedGlId,
                @PostingDate = NULL,
                @PostedBy = 'SimulationUser';
        END TRY
        BEGIN CATCH
            PRINT 'Error posting GL Transaction ID ' + CAST(@UnpostedGlId AS VARCHAR(10)) + ': ' + ERROR_MESSAGE();
        END CATCH
    END
    ELSE
    BEGIN
        PRINT 'Warning: GL Transaction ID ' + CAST(@UnpostedGlId AS VARCHAR(10)) + ' not found, skipping...';
    END

    FETCH NEXT FROM gl_cursor INTO @UnpostedGlId;
END

CLOSE gl_cursor;
DEALLOCATE gl_cursor;

PRINT 'Posted all GL transactions to the general ledger';

-- =============================================
-- SCENARIO 4: PROCESS CUSTOMER PAYMENTS
-- =============================================

PRINT '';
PRINT '=== SCENARIO 4: Processing Customer Payments ===';

-- Create payment transactions for some AR invoices
-- Variables already declared at script top

-- Get some open AR transactions to pay
DECLARE @OpenArId1 INT = (SELECT TOP 1
    ArTransactionId
FROM [dbo].[ArTransaction]
WHERE TransactionStatus = 'Open' AND TransactionType = 'Invoice'
ORDER BY ArTransactionId);
DECLARE @OpenArId2 INT = (SELECT TOP 1
    ArTransactionId
FROM [dbo].[ArTransaction]
WHERE TransactionStatus = 'Open' AND TransactionType = 'Invoice' AND ArTransactionId > @OpenArId1
ORDER BY ArTransactionId);

-- Process partial payment for first invoice
IF @OpenArId1 IS NOT NULL
BEGIN
    DECLARE @PaymentAmount1 DECIMAL(18,2) = (SELECT TransactionAmount * 0.5
    FROM [dbo].[ArTransaction]
    WHERE ArTransactionId = @OpenArId1);
    DECLARE @CustomerName1 VARCHAR(100) = (SELECT CustomerName
    FROM [dbo].[ArTransaction]
    WHERE ArTransactionId = @OpenArId1);

    -- Generate payment transaction number
    DECLARE @NextPaymentNum INT;
    SELECT @NextPaymentNum = ISNULL(MAX(CAST(SUBSTRING(TransactionNumber, 9, 3) AS INT)), 0) + 1
    FROM [dbo].[ArTransaction]
    WHERE TransactionNumber LIKE 'AR-' + CAST(YEAR(GETDATE()) AS VARCHAR(4)) + '-%';

    DECLARE @PaymentTransactionNumber1 VARCHAR(20) = 'AR-' + CAST(YEAR(GETDATE()) AS VARCHAR(4)) + '-' + RIGHT('000' + CAST(@NextPaymentNum AS VARCHAR(3)), 3);

    -- Create payment AR transaction
    INSERT INTO [dbo].[ArTransaction]
        ([TransactionNumber], [TransactionType], [SalesOrderId], [CustomerName],
        [TransactionDate], [DueDate], [TransactionAmount], [OutstandingAmount],
        [TransactionStatus], [Description], [CreatedBy])
    VALUES
        (@PaymentTransactionNumber1, 'Payment', NULL, @CustomerName1,
            GETDATE(), NULL, @PaymentAmount1, 0.00,
            'Paid', 'Partial payment received', 'SimulationUser');

    SET @PaymentArId1 = SCOPE_IDENTITY();

    -- Update original invoice outstanding amount
    UPDATE [dbo].[ArTransaction]
    SET OutstandingAmount = OutstandingAmount - @PaymentAmount1,
        TransactionStatus = CASE 
            WHEN OutstandingAmount - @PaymentAmount1 <= 0 THEN 'Paid'
            ELSE 'Partially Paid'
        END
    WHERE ArTransactionId = @OpenArId1;

    PRINT 'Processed partial payment of $' + CAST(@PaymentAmount1 AS VARCHAR(20)) + ' for customer: ' + @CustomerName1;
END

-- Process full payment for second invoice
IF @OpenArId2 IS NOT NULL
BEGIN
    DECLARE @PaymentAmount2 DECIMAL(18,2) = (SELECT OutstandingAmount
    FROM [dbo].[ArTransaction]
    WHERE ArTransactionId = @OpenArId2);
    DECLARE @CustomerName2 VARCHAR(100) = (SELECT CustomerName
    FROM [dbo].[ArTransaction]
    WHERE ArTransactionId = @OpenArId2);

    -- Generate payment transaction number
    SELECT @NextPaymentNum = ISNULL(MAX(CAST(SUBSTRING(TransactionNumber, 9, 3) AS INT)), 0) + 1
    FROM [dbo].[ArTransaction]
    WHERE TransactionNumber LIKE 'AR-' + CAST(YEAR(GETDATE()) AS VARCHAR(4)) + '-%';

    DECLARE @PaymentTransactionNumber2 VARCHAR(20) = 'AR-' + CAST(YEAR(GETDATE()) AS VARCHAR(4)) + '-' + RIGHT('000' + CAST(@NextPaymentNum AS VARCHAR(3)), 3);

    -- Create payment AR transaction
    INSERT INTO [dbo].[ArTransaction]
        ([TransactionNumber], [TransactionType], [SalesOrderId], [CustomerName],
        [TransactionDate], [DueDate], [TransactionAmount], [OutstandingAmount],
        [TransactionStatus], [Description], [CreatedBy])
    VALUES
        (@PaymentTransactionNumber2, 'Payment', NULL, @CustomerName2,
            GETDATE(), NULL, @PaymentAmount2, 0.00,
            'Paid', 'Full payment received', 'SimulationUser');

    SET @PaymentArId2 = SCOPE_IDENTITY();

    -- Update original invoice
    UPDATE [dbo].[ArTransaction]
    SET OutstandingAmount = 0.00,
        TransactionStatus = 'Paid'
    WHERE ArTransactionId = @OpenArId2;

    PRINT 'Processed full payment of $' + CAST(@PaymentAmount2 AS VARCHAR(20)) + ' for customer: ' + @CustomerName2;
END

-- =============================================
-- SCENARIO 5: ORDER MODIFICATIONS AND CANCELLATIONS
-- =============================================

PRINT '';
PRINT '=== SCENARIO 5: Order Modifications and Cancellations ===';

-- Cancel one of the newly created orders (before invoicing)
UPDATE [dbo].[SalesOrder]
SET OrderStatus = 'Cancelled',
    Notes = ISNULL(Notes, '') + ' [CANCELLED: Customer requested cancellation]'
WHERE SalesOrderId = @NewOrderId3;

PRINT 'Cancelled order for Small Business Solutions LLC';

-- Modify an existing open order (add a line item)
DECLARE @ModifyOrderId INT = (SELECT TOP 1
    SalesOrderId
FROM [dbo].[SalesOrder]
WHERE OrderStatus = 'Open'
ORDER BY SalesOrderId DESC);

IF @ModifyOrderId IS NOT NULL
BEGIN
    DECLARE @NextLineNum INT = (SELECT ISNULL(MAX(LineNumber), 0) + 1
    FROM [dbo].[SalesOrderDetail]
    WHERE SalesOrderId = @ModifyOrderId);

    INSERT INTO [dbo].[SalesOrderDetail]
        ([SalesOrderId], [LineNumber], [ProductCode], [ProductDescription], [Quantity], [UnitPrice])
    VALUES
        (@ModifyOrderId, @NextLineNum, 'RUSH-DELIVERY', 'Rush Delivery Service', 1.0000, 75.0000);

    -- Update order totals
    DECLARE @AdditionalAmount DECIMAL(18,2) = 75.00;
    DECLARE @AdditionalTax DECIMAL(18,2) = 6.00;

    UPDATE [dbo].[SalesOrder]
    SET SubTotal = SubTotal + @AdditionalAmount,
        TaxAmount = TaxAmount + @AdditionalTax,
        TotalAmount = TotalAmount + @AdditionalAmount + @AdditionalTax,
        Notes = ISNULL(Notes, '') + ' [MODIFIED: Added rush delivery service]'
    WHERE SalesOrderId = @ModifyOrderId;

    PRINT 'Modified existing order - added rush delivery service';
END

-- =============================================
-- SCENARIO 6: ACCOUNT BALANCE REPORTING
-- =============================================

PRINT '';
PRINT '=== SCENARIO 6: Account Balance Reporting ===';

-- Get account balances for key accounts
-- Variables already declared at script top

DECLARE @CashAccountId INT = (SELECT AccountId
FROM [dbo].[ChartOfAccounts]
WHERE AccountNumber = '1000');
DECLARE @ArAccountId INT = (SELECT AccountId
FROM [dbo].[ChartOfAccounts]
WHERE AccountNumber = '1200');
DECLARE @SalesAccountId INT = (SELECT AccountId
FROM [dbo].[ChartOfAccounts]
WHERE AccountNumber = '4000');

IF @CashAccountId IS NOT NULL
BEGIN
    EXEC [dbo].[usp_GetAccountBalance]
        @AccountId = @CashAccountId,
        @AsOfDate = NULL,
        @Balance = @CashBalance OUTPUT;
END

IF @ArAccountId IS NOT NULL
BEGIN
    EXEC [dbo].[usp_GetAccountBalance]
        @AccountId = @ArAccountId,
        @AsOfDate = NULL,
        @Balance = @ArBalance OUTPUT;
END

IF @SalesAccountId IS NOT NULL
BEGIN
    EXEC [dbo].[usp_GetAccountBalance]
        @AccountId = @SalesAccountId,
        @AsOfDate = NULL,
        @Balance = @SalesBalance OUTPUT;
END

-- =============================================
-- FINAL SUMMARY
-- =============================================

PRINT '';
PRINT '=== WORKFLOW SIMULATION SUMMARY ===';

-- Display transaction counts
    SELECT
        'Sales Orders' as TransactionType,
        COUNT(*) as TotalCount,
        SUM(CASE WHEN OrderStatus = 'Open' THEN 1 ELSE 0 END) as OpenCount,
        SUM(CASE WHEN OrderStatus = 'Invoiced' THEN 1 ELSE 0 END) as InvoicedCount,
        SUM(CASE WHEN OrderStatus = 'Cancelled' THEN 1 ELSE 0 END) as CancelledCount
    FROM [dbo].[SalesOrder]

UNION ALL

    SELECT
        'AR Transactions' as TransactionType,
        COUNT(*) as TotalCount,
        SUM(CASE WHEN TransactionStatus = 'Open' THEN 1 ELSE 0 END) as OpenCount,
        SUM(CASE WHEN TransactionStatus = 'Paid' THEN 1 ELSE 0 END) as PaidCount,
        SUM(CASE WHEN TransactionStatus = 'Partially Paid' THEN 1 ELSE 0 END) as PartiallyPaidCount
    FROM [dbo].[ArTransaction]

UNION ALL

    SELECT
        'GL Transactions' as TransactionType,
        COUNT(*) as TotalCount,
        SUM(CASE WHEN IsPosted = 1 THEN 1 ELSE 0 END) as PostedCount,
        SUM(CASE WHEN IsPosted = 0 THEN 1 ELSE 0 END) as UnpostedCount,
        0 as ExtraCount
    FROM [dbo].[GlTransaction];

-- Display key account balances
SELECT
    coa.AccountNumber,
    coa.AccountName,
    coa.AccountType,
    CASE 
        WHEN coa.AccountNumber = '1000' THEN ISNULL(@CashBalance, 0.00)
        WHEN coa.AccountNumber = '1200' THEN ISNULL(@ArBalance, 0.00)
        WHEN coa.AccountNumber = '4000' THEN ISNULL(@SalesBalance, 0.00)
        ELSE 0.00
    END as CurrentBalance
FROM [dbo].[ChartOfAccounts] coa
WHERE coa.AccountNumber IN ('1000', '1200', '4000')
ORDER BY coa.AccountNumber;

PRINT '';
PRINT '=============================================';
PRINT 'ERP Business Workflow Simulation Complete!';
PRINT '=============================================';
PRINT 'Timestamp: ' + CONVERT(VARCHAR(23), GETDATE(), 121);
PRINT '';
PRINT 'Workflow Operations Performed:';
PRINT '- Created new sales orders with line items';
PRINT '- Invoiced sales orders (generated AR and GL transactions)';
PRINT '- Posted GL transactions to general ledger';
PRINT '- Processed customer payments (partial and full)';
PRINT '- Modified and cancelled orders';
PRINT '- Generated account balance reports';
PRINT '';
PRINT 'This simulation generates comprehensive CDC data for testing';
PRINT 'database change capture and replay functionality.';
PRINT '';

GO