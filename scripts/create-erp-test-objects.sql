
-- =============================================
-- ERP Test Database Objects Creation Script
-- =============================================
-- Creates database structure only (tables, indexes, procedures, triggers)
-- No sample data - use initialize-erp-sample-data.sql for that
-- 
-- Usage: Execute this script against the cdctest database
-- Connection: Uses TEST_DB_CONNECTION environment variable
-- 
-- Objects Created:
-- - Tables: ChartOfAccounts, SalesOrder, SalesOrderDetail, ArTransaction, ArTransactionDetail, GlTransaction, GlTransactionDetail
-- - Stored Procedures: usp_CreateSalesOrder, usp_DeleteSalesOrder, usp_InvoiceSalesOrder, usp_PostGlTransaction, usp_GetAccountBalance
-- - Indexes: Performance indexes on all tables
-- - Triggers: Update triggers for ModifiedDate columns
-- =============================================

USE [cdctest];
GO

PRINT 'Starting ERP Test Database Objects Creation...';
GO

-- =============================================
-- 1. CREATE TABLES
-- =============================================

-- Chart of Accounts Table
PRINT 'Creating ChartOfAccounts table...';
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[ChartOfAccounts]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ChartOfAccounts]
    (
        [AccountId] INT IDENTITY(1,1) NOT NULL,
        [AccountNumber] VARCHAR(20) NOT NULL,
        [AccountName] VARCHAR(100) NOT NULL,
        [AccountType] VARCHAR(20) NOT NULL,
        [ParentAccountId] INT NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        [ModifiedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_ChartOfAccounts] PRIMARY KEY CLUSTERED ([AccountId] ASC),
        CONSTRAINT [FK_ChartOfAccounts_Parent] FOREIGN KEY ([ParentAccountId]) REFERENCES [dbo].[ChartOfAccounts]([AccountId]),
        CONSTRAINT [CK_ChartOfAccounts_AccountType] CHECK ([AccountType] IN ('Asset', 'Liability', 'Equity', 'Revenue', 'Expense'))
    );
END
GO

-- Sales Order Table
PRINT 'Creating SalesOrder table...';
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[SalesOrder]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[SalesOrder]
    (
        [SalesOrderId] INT IDENTITY(1,1) NOT NULL,
        [OrderNumber] VARCHAR(20) NOT NULL,
        [CustomerName] VARCHAR(100) NOT NULL,
        [CustomerEmail] VARCHAR(100) NULL,
        [OrderDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        [RequiredDate] DATETIME2(7) NULL,
        [OrderStatus] VARCHAR(20) NOT NULL DEFAULT 'Open',
        [SubTotal] DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        [TaxAmount] DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        [TotalAmount] DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        [Notes] VARCHAR(500) NULL,
        [CreatedBy] VARCHAR(50) NOT NULL DEFAULT SYSTEM_USER,
        [CreatedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        [ModifiedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_SalesOrder] PRIMARY KEY CLUSTERED ([SalesOrderId] ASC),
        CONSTRAINT [CK_SalesOrder_OrderStatus] CHECK ([OrderStatus] IN ('Open', 'Invoiced', 'Cancelled', 'Deleted'))
    );
END
GO

-- Sales Order Detail Table
PRINT 'Creating SalesOrderDetail table...';
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[SalesOrderDetail]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[SalesOrderDetail]
    (
        [SalesOrderDetailId] INT IDENTITY(1,1) NOT NULL,
        [SalesOrderId] INT NOT NULL,
        [LineNumber] INT NOT NULL,
        [ProductCode] VARCHAR(50) NOT NULL,
        [ProductDescription] VARCHAR(200) NOT NULL,
        [Quantity] DECIMAL(18,4) NOT NULL,
        [UnitPrice] DECIMAL(18,4) NOT NULL,
        [LineTotal] AS ([Quantity] * [UnitPrice]) PERSISTED,
        [CreatedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        [ModifiedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_SalesOrderDetail] PRIMARY KEY CLUSTERED ([SalesOrderDetailId] ASC),
        CONSTRAINT [FK_SalesOrderDetail_SalesOrder] FOREIGN KEY ([SalesOrderId]) REFERENCES [dbo].[SalesOrder]([SalesOrderId]) ON DELETE CASCADE
    );
END
GO

-- AR Transaction Table
PRINT 'Creating ArTransaction table...';
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[ArTransaction]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ArTransaction]
    (
        [ArTransactionId] INT IDENTITY(1,1) NOT NULL,
        [TransactionNumber] VARCHAR(20) NOT NULL,
        [TransactionType] VARCHAR(20) NOT NULL,
        [SalesOrderId] INT NULL,
        [CustomerName] VARCHAR(100) NOT NULL,
        [TransactionDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        [DueDate] DATETIME2(7) NULL,
        [TransactionAmount] DECIMAL(18,2) NOT NULL,
        [OutstandingAmount] DECIMAL(18,2) NOT NULL,
        [TransactionStatus] VARCHAR(20) NOT NULL DEFAULT 'Open',
        [Description] VARCHAR(200) NULL,
        [CreatedBy] VARCHAR(50) NOT NULL DEFAULT SYSTEM_USER,
        [CreatedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        [ModifiedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_ArTransaction] PRIMARY KEY CLUSTERED ([ArTransactionId] ASC),
        CONSTRAINT [FK_ArTransaction_SalesOrder] FOREIGN KEY ([SalesOrderId]) REFERENCES [dbo].[SalesOrder]([SalesOrderId]),
        CONSTRAINT [CK_ArTransaction_TransactionType] CHECK ([TransactionType] IN ('Invoice', 'Payment', 'Credit', 'Adjustment')),
        CONSTRAINT [CK_ArTransaction_TransactionStatus] CHECK ([TransactionStatus] IN ('Open', 'Paid', 'Partially Paid', 'Cancelled'))
    );
END
GO

-- AR Transaction Detail Table
PRINT 'Creating ArTransactionDetail table...';
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[ArTransactionDetail]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ArTransactionDetail]
    (
        [ArTransactionDetailId] INT IDENTITY(1,1) NOT NULL,
        [ArTransactionId] INT NOT NULL,
        [LineNumber] INT NOT NULL,
        [SalesOrderDetailId] INT NULL,
        [ProductCode] VARCHAR(50) NOT NULL,
        [ProductDescription] VARCHAR(200) NOT NULL,
        [Quantity] DECIMAL(18,4) NOT NULL,
        [UnitPrice] DECIMAL(18,4) NOT NULL,
        [LineTotal] DECIMAL(18,2) NOT NULL,
        [CreatedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        [ModifiedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_ArTransactionDetail] PRIMARY KEY CLUSTERED ([ArTransactionDetailId] ASC),
        CONSTRAINT [FK_ArTransactionDetail_ArTransaction] FOREIGN KEY ([ArTransactionId]) REFERENCES [dbo].[ArTransaction]([ArTransactionId]) ON DELETE CASCADE,
        CONSTRAINT [FK_ArTransactionDetail_SalesOrderDetail] FOREIGN KEY ([SalesOrderDetailId]) REFERENCES [dbo].[SalesOrderDetail]([SalesOrderDetailId])
    );
END
GO

-- GL Transaction Table
PRINT 'Creating GlTransaction table...';
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[GlTransaction]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[GlTransaction]
    (
        [GlTransactionId] INT IDENTITY(1,1) NOT NULL,
        [TransactionNumber] VARCHAR(20) NOT NULL,
        [TransactionType] VARCHAR(20) NOT NULL,
        [SourceModule] VARCHAR(20) NOT NULL,
        [SourceTransactionId] INT NOT NULL,
        [TransactionDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        [PostingDate] DATETIME2(7) NULL,
        [Description] VARCHAR(200) NOT NULL,
        [TotalDebitAmount] DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        [TotalCreditAmount] DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        [IsPosted] BIT NOT NULL DEFAULT 0,
        [CreatedBy] VARCHAR(50) NOT NULL DEFAULT SYSTEM_USER,
        [CreatedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        [ModifiedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_GlTransaction] PRIMARY KEY CLUSTERED ([GlTransactionId] ASC),
        CONSTRAINT [CK_GlTransaction_TransactionType] CHECK ([TransactionType] IN ('Sales', 'Payment', 'Adjustment', 'Accrual', 'Reversal')),
        CONSTRAINT [CK_GlTransaction_SourceModule] CHECK ([SourceModule] IN ('AR', 'AP', 'Inventory', 'Payroll', 'Manual'))
    );
END
GO

-- GL Transaction Detail Table
PRINT 'Creating GlTransactionDetail table...';
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[GlTransactionDetail]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[GlTransactionDetail]
    (
        [GlTransactionDetailId] INT IDENTITY(1,1) NOT NULL,
        [GlTransactionId] INT NOT NULL,
        [LineNumber] INT NOT NULL,
        [AccountId] INT NOT NULL,
        [DebitAmount] DECIMAL(18,2) NULL,
        [CreditAmount] DECIMAL(18,2) NULL,
        [Description] VARCHAR(200) NOT NULL,
        [CreatedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        [ModifiedDate] DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_GlTransactionDetail] PRIMARY KEY CLUSTERED ([GlTransactionDetailId] ASC),
        CONSTRAINT [FK_GlTransactionDetail_GlTransaction] FOREIGN KEY ([GlTransactionId]) REFERENCES [dbo].[GlTransaction]([GlTransactionId]) ON DELETE CASCADE,
        CONSTRAINT [FK_GlTransactionDetail_ChartOfAccounts] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[ChartOfAccounts]([AccountId]),
        CONSTRAINT [CK_GlTransactionDetail_DebitOrCredit] CHECK (([DebitAmount] IS NOT NULL AND [CreditAmount] IS NULL) OR ([DebitAmount] IS NULL AND [CreditAmount] IS NOT NULL))
    );
END
GO

-- =============================================
-- 2. CREATE INDEXES
-- =============================================

PRINT 'Creating indexes...';

-- ChartOfAccounts Indexes
IF NOT EXISTS (SELECT *
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[dbo].[ChartOfAccounts]') AND name = N'IX_ChartOfAccounts_AccountNumber')
    CREATE UNIQUE NONCLUSTERED INDEX [IX_ChartOfAccounts_AccountNumber] ON [dbo].[ChartOfAccounts] ([AccountNumber] ASC);

IF NOT EXISTS (SELECT *
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[dbo].[ChartOfAccounts]') AND name = N'IX_ChartOfAccounts_ParentAccountId')
    CREATE NONCLUSTERED INDEX [IX_ChartOfAccounts_ParentAccountId] ON [dbo].[ChartOfAccounts] ([ParentAccountId] ASC);

-- SalesOrder Indexes
IF NOT EXISTS (SELECT *
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[dbo].[SalesOrder]') AND name = N'IX_SalesOrder_OrderNumber')
    CREATE UNIQUE NONCLUSTERED INDEX [IX_SalesOrder_OrderNumber] ON [dbo].[SalesOrder] ([OrderNumber] ASC);

IF NOT EXISTS (SELECT *
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[dbo].[SalesOrder]') AND name = N'IX_SalesOrder_CustomerName')
    CREATE NONCLUSTERED INDEX [IX_SalesOrder_CustomerName] ON [dbo].[SalesOrder] ([CustomerName] ASC);

IF NOT EXISTS (SELECT *
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[dbo].[SalesOrder]') AND name = N'IX_SalesOrder_OrderDate')
    CREATE NONCLUSTERED INDEX [IX_SalesOrder_OrderDate] ON [dbo].[SalesOrder] ([OrderDate] ASC);

-- SalesOrderDetail Indexes
IF NOT EXISTS (SELECT *
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[dbo].[SalesOrderDetail]') AND name = N'IX_SalesOrderDetail_SalesOrderId')
    CREATE NONCLUSTERED INDEX [IX_SalesOrderDetail_SalesOrderId] ON [dbo].[SalesOrderDetail] ([SalesOrderId] ASC);

-- ArTransaction Indexes
IF NOT EXISTS (SELECT *
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[dbo].[ArTransaction]') AND name = N'IX_ArTransaction_TransactionNumber')
    CREATE UNIQUE NONCLUSTERED INDEX [IX_ArTransaction_TransactionNumber] ON [dbo].[ArTransaction] ([TransactionNumber] ASC);

IF NOT EXISTS (SELECT *
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[dbo].[ArTransaction]') AND name = N'IX_ArTransaction_SalesOrderId')
    CREATE NONCLUSTERED INDEX [IX_ArTransaction_SalesOrderId] ON [dbo].[ArTransaction] ([SalesOrderId] ASC);

IF NOT EXISTS (SELECT *
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[dbo].[ArTransaction]') AND name = N'IX_ArTransaction_CustomerName')
    CREATE NONCLUSTERED INDEX [IX_ArTransaction_CustomerName] ON [dbo].[ArTransaction] ([CustomerName] ASC);

-- ArTransactionDetail Indexes
IF NOT EXISTS (SELECT *
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[dbo].[ArTransactionDetail]') AND name = N'IX_ArTransactionDetail_ArTransactionId')
    CREATE NONCLUSTERED INDEX [IX_ArTransactionDetail_ArTransactionId] ON [dbo].[ArTransactionDetail] ([ArTransactionId] ASC);

-- GlTransaction Indexes
IF NOT EXISTS (SELECT *
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[dbo].[GlTransaction]') AND name = N'IX_GlTransaction_TransactionNumber')
    CREATE UNIQUE NONCLUSTERED INDEX [IX_GlTransaction_TransactionNumber] ON [dbo].[GlTransaction] ([TransactionNumber] ASC);

IF NOT EXISTS (SELECT *
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[dbo].[GlTransaction]') AND name = N'IX_GlTransaction_TransactionDate')
    CREATE NONCLUSTERED INDEX [IX_GlTransaction_TransactionDate] ON [dbo].[GlTransaction] ([TransactionDate] ASC);

-- GlTransactionDetail Indexes
IF NOT EXISTS (SELECT *
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[dbo].[GlTransactionDetail]') AND name = N'IX_GlTransactionDetail_GlTransactionId')
    CREATE NONCLUSTERED INDEX [IX_GlTransactionDetail_GlTransactionId] ON [dbo].[GlTransactionDetail] ([GlTransactionId] ASC);

IF NOT EXISTS (SELECT *
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'[dbo].[GlTransactionDetail]') AND name = N'IX_GlTransactionDetail_AccountId')
    CREATE NONCLUSTERED INDEX [IX_GlTransactionDetail_AccountId] ON [dbo].[GlTransactionDetail] ([AccountId] ASC);

GO

-- =============================================
-- 3. CREATE STORED PROCEDURES
-- =============================================

PRINT 'Creating stored procedures...';

-- Create Sales Order Procedure
IF EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[usp_CreateSalesOrder]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[usp_CreateSalesOrder];
GO

CREATE PROCEDURE [dbo].[usp_CreateSalesOrder]
    @CustomerName VARCHAR(100),
    @CustomerEmail VARCHAR(100) = NULL,
    @RequiredDate DATETIME2(7) = NULL,
    @Notes VARCHAR(500) = NULL,
    @CreatedBy VARCHAR(50) = NULL,
    @SalesOrderId INT OUTPUT,
    @OrderNumber VARCHAR(20) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Generate order number
        DECLARE @NextOrderNum INT;
        SELECT @NextOrderNum = ISNULL(MAX(CAST(SUBSTRING(OrderNumber, 9, 3) AS INT)), 0) + 1
    FROM [dbo].[SalesOrder]
    WHERE OrderNumber LIKE 'SO-' + CAST(YEAR(GETDATE()) AS VARCHAR(4)) + '-%';
        
        SET @OrderNumber = 'SO-' + CAST(YEAR(GETDATE()) AS VARCHAR(4)) + '-' + RIGHT('000' + CAST(@NextOrderNum AS VARCHAR(3)), 3);
        SET @CreatedBy = ISNULL(@CreatedBy, SYSTEM_USER);
        
        -- Insert sales order
        INSERT INTO [dbo].[SalesOrder]
        (
        [OrderNumber], [CustomerName], [CustomerEmail], [RequiredDate],
        [Notes], [CreatedBy]
        )
    VALUES
        (
            @OrderNumber, @CustomerName, @CustomerEmail, @RequiredDate,
            @Notes, @CreatedBy
        );
        
        SET @SalesOrderId = SCOPE_IDENTITY();
        
        COMMIT TRANSACTION;
        
        PRINT 'Sales Order ' + @OrderNumber + ' created successfully with ID: ' + CAST(@SalesOrderId AS VARCHAR(10));
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
GO

-- Delete Sales Order Procedure
IF EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[usp_DeleteSalesOrder]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[usp_DeleteSalesOrder];
GO

CREATE PROCEDURE [dbo].[usp_DeleteSalesOrder]
    @SalesOrderId INT,
    @DeletedBy VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Check if order exists and can be deleted
        DECLARE @OrderStatus VARCHAR(20);
        DECLARE @OrderNumber VARCHAR(20);
        
        SELECT @OrderStatus = OrderStatus, @OrderNumber = OrderNumber
    FROM [dbo].[SalesOrder]
    WHERE SalesOrderId = @SalesOrderId;
        
        IF @OrderStatus IS NULL
        BEGIN
        RAISERROR('Sales Order ID %d not found.', 16, 1, @SalesOrderId);
        RETURN;
    END
        
        IF @OrderStatus = 'Invoiced'
        BEGIN
        RAISERROR('Cannot delete invoiced Sales Order %s.', 16, 1, @OrderNumber);
        RETURN;
    END
        
        -- Mark as deleted first (for audit trail)
        UPDATE [dbo].[SalesOrder]
        SET OrderStatus = 'Deleted',
            ModifiedDate = SYSUTCDATETIME()
        WHERE SalesOrderId = @SalesOrderId;
        
        -- Delete order details (cascade will handle this, but explicit for clarity)
        DELETE FROM [dbo].[SalesOrderDetail]
        WHERE SalesOrderId = @SalesOrderId;
        
        -- Delete the order
        DELETE FROM [dbo].[SalesOrder]
        WHERE SalesOrderId = @SalesOrderId;
        
        COMMIT TRANSACTION;
        
        PRINT 'Sales Order ' + @OrderNumber + ' deleted successfully.';
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
GO

-- Invoice Sales Order Procedure
IF EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[usp_InvoiceSalesOrder]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[usp_InvoiceSalesOrder];
GO

CREATE PROCEDURE [dbo].[usp_InvoiceSalesOrder]
    @SalesOrderId INT,
    @InvoiceDate DATETIME2(7) = NULL,
    @DueDate DATETIME2(7) = NULL,
    @CreatedBy VARCHAR(50) = NULL,
    @ArTransactionId INT OUTPUT,
    @GlTransactionId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Validate sales order
        DECLARE @OrderStatus VARCHAR(20), @OrderNumber VARCHAR(20), @CustomerName VARCHAR(100), @TotalAmount DECIMAL(18,2);
        
        SELECT @OrderStatus = OrderStatus, @OrderNumber = OrderNumber,
        @CustomerName = CustomerName, @TotalAmount = TotalAmount
    FROM [dbo].[SalesOrder]
    WHERE SalesOrderId = @SalesOrderId;
        
        IF @OrderStatus IS NULL
        BEGIN
        RAISERROR('Sales Order ID %d not found.', 16, 1, @SalesOrderId);
        RETURN;
    END
        
        IF @OrderStatus != 'Open'
        BEGIN
        RAISERROR('Sales Order %s cannot be invoiced. Current status: %s', 16, 1, @OrderNumber, @OrderStatus);
        RETURN;
    END
        
        -- Set defaults
        SET @InvoiceDate = ISNULL(@InvoiceDate, SYSUTCDATETIME());
        SET @DueDate = ISNULL(@DueDate, DATEADD(day, 30, @InvoiceDate));
        SET @CreatedBy = ISNULL(@CreatedBy, SYSTEM_USER);
        
        -- Generate AR transaction number
        DECLARE @NextArNum INT;
        SELECT @NextArNum = ISNULL(MAX(CAST(SUBSTRING(TransactionNumber, 9, 3) AS INT)), 0) + 1
    FROM [dbo].[ArTransaction]
    WHERE TransactionNumber LIKE 'AR-' + CAST(YEAR(@InvoiceDate) AS VARCHAR(4)) + '-%';
        
        DECLARE @ArTransactionNumber VARCHAR(20) = 'AR-' + CAST(YEAR(@InvoiceDate) AS VARCHAR(4)) + '-' + RIGHT('000' + CAST(@NextArNum AS VARCHAR(3)), 3);
        
        -- Create AR Transaction
        INSERT INTO [dbo].[ArTransaction]
        (
        [TransactionNumber], [TransactionType], [SalesOrderId], [CustomerName],
        [TransactionDate], [DueDate], [TransactionAmount], [OutstandingAmount],
        [Description], [CreatedBy]
        )
    VALUES
        (
            @ArTransactionNumber, 'Invoice', @SalesOrderId, @CustomerName,
            @InvoiceDate, @DueDate, @TotalAmount, @TotalAmount,
            'Invoice for Sales Order ' + @OrderNumber, @CreatedBy
        );
        
        SET @ArTransactionId = SCOPE_IDENTITY();
        -- Create AR Transaction Details
        INSERT INTO [dbo].[ArTransactionDetail]
        (
        [ArTransactionId], [LineNumber], [SalesOrderDetailId], [ProductCode],
        [ProductDescription], [Quantity], [UnitPrice], [LineTotal]
        )
    SELECT
        @ArTransactionId,
        sod.LineNumber,
        sod.SalesOrderDetailId,
        sod.ProductCode,
        sod.ProductDescription,
        sod.Quantity,
        sod.UnitPrice,
        sod.LineTotal
    FROM [dbo].[SalesOrderDetail] sod
    WHERE sod.SalesOrderId = @SalesOrderId;
        
        -- Generate GL transaction number
        DECLARE @NextGlNum INT;
        SELECT @NextGlNum = ISNULL(MAX(CAST(SUBSTRING(TransactionNumber, 9, 3) AS INT)), 0) + 1
    FROM [dbo].[GlTransaction]
    WHERE TransactionNumber LIKE 'GL-' + CAST(YEAR(@InvoiceDate) AS VARCHAR(4)) + '-%';
        
        DECLARE @GlTransactionNumber VARCHAR(20) = 'GL-' + CAST(YEAR(@InvoiceDate) AS VARCHAR(4)) + '-' + RIGHT('000' + CAST(@NextGlNum AS VARCHAR(3)), 3);
        
        -- Create GL Transaction
        INSERT INTO [dbo].[GlTransaction]
        (
        [TransactionNumber], [TransactionType], [SourceModule], [SourceTransactionId],
        [TransactionDate], [Description], [TotalDebitAmount], [TotalCreditAmount], [CreatedBy]
        )
    VALUES
        (
            @GlTransactionNumber, 'Sales', 'AR', @ArTransactionId,
            @InvoiceDate, 'Sales Invoice ' + @ArTransactionNumber, @TotalAmount, @TotalAmount, @CreatedBy
        );
        
        SET @GlTransactionId = SCOPE_IDENTITY();
        
        -- Get account IDs for GL posting
        DECLARE @ArAccountId INT = (SELECT AccountId
    FROM [dbo].[ChartOfAccounts]
    WHERE AccountNumber = '1200'); -- Accounts Receivable
        DECLARE @SalesAccountId INT = (SELECT AccountId
    FROM [dbo].[ChartOfAccounts]
    WHERE AccountNumber = '4000'); -- Sales Revenue
        
        -- Validate that required accounts exist
        IF @ArAccountId IS NULL
        BEGIN
        RAISERROR('Accounts Receivable account (1200) not found in Chart of Accounts.', 16, 1);
        RETURN;
    END
        
        IF @SalesAccountId IS NULL
        BEGIN
        RAISERROR('Sales Revenue account (4000) not found in Chart of Accounts.', 16, 1);
        RETURN;
    END
        
        -- Create GL Transaction Details
        INSERT INTO [dbo].[GlTransactionDetail]
        (
        [GlTransactionId], [LineNumber], [AccountId], [DebitAmount], [CreditAmount], [Description]
        )
    VALUES
        (@GlTransactionId, 1, @ArAccountId, @TotalAmount, NULL, 'AR for Invoice ' + @ArTransactionNumber),
        (@GlTransactionId, 2, @SalesAccountId, NULL, @TotalAmount, 'Sales Revenue for Invoice ' + @ArTransactionNumber);
        
        -- Update sales order status
        UPDATE [dbo].[SalesOrder]
        SET OrderStatus = 'Invoiced',
            ModifiedDate = SYSUTCDATETIME()
        WHERE SalesOrderId = @SalesOrderId;
        
        COMMIT TRANSACTION;
        
        PRINT 'Sales Order ' + @OrderNumber + ' invoiced successfully.';
        PRINT 'AR Transaction ID: ' + CAST(@ArTransactionId AS VARCHAR(10)) + ' (' + @ArTransactionNumber + ')';
        PRINT 'GL Transaction ID: ' + CAST(@GlTransactionId AS VARCHAR(10)) + ' (' + @GlTransactionNumber + ')';
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
GO

-- Post GL Transaction Procedure
IF EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[usp_PostGlTransaction]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[usp_PostGlTransaction];
GO

CREATE PROCEDURE [dbo].[usp_PostGlTransaction]
    @GlTransactionId INT,
    @PostingDate DATETIME2(7) = NULL,
    @PostedBy VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Validate GL transaction
        DECLARE @IsPosted BIT, @TransactionNumber VARCHAR(20);
        
        SELECT @IsPosted = IsPosted, @TransactionNumber = TransactionNumber
    FROM [dbo].[GlTransaction]
    WHERE GlTransactionId = @GlTransactionId;
        
        IF @TransactionNumber IS NULL
        BEGIN
        RAISERROR('GL Transaction ID %d not found.', 16, 1, @GlTransactionId);
        RETURN;
    END
        
        IF @IsPosted = 1
        BEGIN
        RAISERROR('GL Transaction %s is already posted.', 16, 1, @TransactionNumber);
        RETURN;
    END
        
        -- Validate that debits equal credits
        DECLARE @TotalDebits DECIMAL(18,2), @TotalCredits DECIMAL(18,2);
        
        SELECT
        @TotalDebits = SUM(ISNULL(DebitAmount, 0)),
        @TotalCredits = SUM(ISNULL(CreditAmount, 0))
    FROM [dbo].[GlTransactionDetail]
    WHERE GlTransactionId = @GlTransactionId;
        
        IF @TotalDebits != @TotalCredits
        BEGIN
        DECLARE @DebitStr VARCHAR(20) = CAST(@TotalDebits AS VARCHAR(20));
        DECLARE @CreditStr VARCHAR(20) = CAST(@TotalCredits AS VARCHAR(20));
        RAISERROR('GL Transaction %s is out of balance. Debits: %s, Credits: %s', 16, 1,
                @TransactionNumber, @DebitStr, @CreditStr);
        RETURN;
    END
        
        -- Set defaults
        SET @PostingDate = ISNULL(@PostingDate, SYSUTCDATETIME());
        SET @PostedBy = ISNULL(@PostedBy, SYSTEM_USER);
        
        -- Post the transaction
        UPDATE [dbo].[GlTransaction]
        SET IsPosted = 1,
            PostingDate = @PostingDate,
            ModifiedDate = SYSUTCDATETIME()
        WHERE GlTransactionId = @GlTransactionId;
        
        COMMIT TRANSACTION;
        
        PRINT 'GL Transaction ' + @TransactionNumber + ' posted successfully.';
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
GO

-- Get Account Balance Procedure
IF EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[usp_GetAccountBalance]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[usp_GetAccountBalance];
GO

CREATE PROCEDURE [dbo].[usp_GetAccountBalance]
    @AccountId INT,
    @AsOfDate DATETIME2(7) = NULL,
    @Balance DECIMAL(18,2) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Set default date to current date
    SET @AsOfDate = ISNULL(@AsOfDate, SYSUTCDATETIME());

    -- Calculate balance based on account type
    DECLARE @AccountType VARCHAR(20);
    SELECT @AccountType = AccountType
    FROM [dbo].[ChartOfAccounts]
    WHERE AccountId = @AccountId;

    IF @AccountType IS NULL
    BEGIN
        RAISERROR('Account ID %d not found.', 16, 1, @AccountId);
        RETURN;
    END

    -- Calculate balance (Assets and Expenses are debit normal, others are credit normal)
    IF @AccountType IN ('Asset', 'Expense')
    BEGIN
        SELECT @Balance = ISNULL(SUM(ISNULL(gtd.DebitAmount, 0) - ISNULL(gtd.CreditAmount, 0)), 0)
        FROM [dbo].[GlTransactionDetail] gtd
            INNER JOIN [dbo].[GlTransaction] gt ON gtd.GlTransactionId = gt.GlTransactionId
        WHERE gtd.AccountId = @AccountId
            AND gt.IsPosted = 1
            AND gt.PostingDate <= @AsOfDate;
    END
    ELSE
    BEGIN
        SELECT @Balance = ISNULL(SUM(ISNULL(gtd.CreditAmount, 0) - ISNULL(gtd.DebitAmount, 0)), 0)
        FROM [dbo].[GlTransactionDetail] gtd
            INNER JOIN [dbo].[GlTransaction] gt ON gtd.GlTransactionId = gt.GlTransactionId
        WHERE gtd.AccountId = @AccountId
            AND gt.IsPosted = 1
            AND gt.PostingDate <= @AsOfDate;
    END

    PRINT 'Account balance calculated: ' + CAST(@Balance AS VARCHAR(20));
END
GO

-- =============================================
-- 4. CREATE UPDATE TRIGGERS FOR MODIFIED DATE
-- =============================================

PRINT 'Creating update triggers for ModifiedDate columns...';

-- ChartOfAccounts Update Trigger
IF EXISTS (SELECT *
FROM sys.triggers
WHERE object_id = OBJECT_ID(N'[dbo].[tr_ChartOfAccounts_Update]'))
    DROP TRIGGER [dbo].[tr_ChartOfAccounts_Update];
GO

CREATE TRIGGER [dbo].[tr_ChartOfAccounts_Update]
ON [dbo].[ChartOfAccounts]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[ChartOfAccounts]
    SET ModifiedDate = SYSUTCDATETIME()
    FROM [dbo].[ChartOfAccounts] coa
        INNER JOIN inserted i ON coa.AccountId = i.AccountId;
END
GO

-- SalesOrder Update Trigger
IF EXISTS (SELECT *
FROM sys.triggers
WHERE object_id = OBJECT_ID(N'[dbo].[tr_SalesOrder_Update]'))
    DROP TRIGGER [dbo].[tr_SalesOrder_Update];
GO

CREATE TRIGGER [dbo].[tr_SalesOrder_Update]
ON [dbo].[SalesOrder]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[SalesOrder]
    SET ModifiedDate = SYSUTCDATETIME()
    FROM [dbo].[SalesOrder] so
        INNER JOIN inserted i ON so.SalesOrderId = i.SalesOrderId;
END
GO

-- SalesOrderDetail Update Trigger
IF EXISTS (SELECT *
FROM sys.triggers
WHERE object_id = OBJECT_ID(N'[dbo].[tr_SalesOrderDetail_Update]'))
    DROP TRIGGER [dbo].[tr_SalesOrderDetail_Update];
GO

CREATE TRIGGER [dbo].[tr_SalesOrderDetail_Update]
ON [dbo].[SalesOrderDetail]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[SalesOrderDetail]
    SET ModifiedDate = SYSUTCDATETIME()
    FROM [dbo].[SalesOrderDetail] sod
        INNER JOIN inserted i ON sod.SalesOrderDetailId = i.SalesOrderDetailId;
END
GO

-- ArTransaction Update Trigger
IF EXISTS (SELECT *
FROM sys.triggers
WHERE object_id = OBJECT_ID(N'[dbo].[tr_ArTransaction_Update]'))
    DROP TRIGGER [dbo].[tr_ArTransaction_Update];
GO

CREATE TRIGGER [dbo].[tr_ArTransaction_Update]
ON [dbo].[ArTransaction]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[ArTransaction]
    SET ModifiedDate = SYSUTCDATETIME()
    FROM [dbo].[ArTransaction] art
        INNER JOIN inserted i ON art.ArTransactionId = i.ArTransactionId;
END
GO

-- ArTransactionDetail Update Trigger
IF EXISTS (SELECT *
FROM sys.triggers
WHERE object_id = OBJECT_ID(N'[dbo].[tr_ArTransactionDetail_Update]'))
    DROP TRIGGER [dbo].[tr_ArTransactionDetail_Update];
GO

CREATE TRIGGER [dbo].[tr_ArTransactionDetail_Update]
ON [dbo].[ArTransactionDetail]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[ArTransactionDetail]
    SET ModifiedDate = SYSUTCDATETIME()
    FROM [dbo].[ArTransactionDetail] artd
        INNER JOIN inserted i ON artd.ArTransactionDetailId = i.ArTransactionDetailId;
END
GO

-- GlTransaction Update Trigger
IF EXISTS (SELECT *
FROM sys.triggers
WHERE object_id = OBJECT_ID(N'[dbo].[tr_GlTransaction_Update]'))
    DROP TRIGGER [dbo].[tr_GlTransaction_Update];
GO

CREATE TRIGGER [dbo].[tr_GlTransaction_Update]
ON [dbo].[GlTransaction]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[GlTransaction]
    SET ModifiedDate = SYSUTCDATETIME()
    FROM [dbo].[GlTransaction] gt
        INNER JOIN inserted i ON gt.GlTransactionId = i.GlTransactionId;
END
GO

-- GlTransactionDetail Update Trigger
IF EXISTS (SELECT *
FROM sys.triggers
WHERE object_id = OBJECT_ID(N'[dbo].[tr_GlTransactionDetail_Update]'))
    DROP TRIGGER [dbo].[tr_GlTransactionDetail_Update];
GO

CREATE TRIGGER [dbo].[tr_GlTransactionDetail_Update]
ON [dbo].[GlTransactionDetail]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[GlTransactionDetail]
    SET ModifiedDate = SYSUTCDATETIME()
    FROM [dbo].[GlTransactionDetail] gtd
        INNER JOIN inserted i ON gtd.GlTransactionDetailId = i.GlTransactionDetailId;
END
GO

-- =============================================
-- 5. FINAL VERIFICATION AND SUMMARY
-- =============================================

PRINT 'Verifying created objects...';

-- Display created tables
    SELECT 'Tables Created:' as ObjectType, name as ObjectName
    FROM sys.tables
    WHERE name IN ('ChartOfAccounts', 'SalesOrder', 'SalesOrderDetail', 'ArTransaction', 'ArTransactionDetail', 'GlTransaction', 'GlTransactionDetail')
UNION ALL
    -- Display created procedures
    SELECT 'Stored Procedures Created:' as ObjectType, name as ObjectName
    FROM sys.procedures
    WHERE name LIKE 'usp_%'
UNION ALL
    -- Display created triggers
    SELECT 'Triggers Created:' as ObjectType, name as ObjectName
    FROM sys.triggers
    WHERE name LIKE 'tr_%'
ORDER BY ObjectType, ObjectName;

PRINT '';
PRINT '=============================================';
PRINT 'ERP Test Database Objects Creation Complete!';
PRINT '=============================================';
PRINT '';
PRINT 'Objects Created:';
PRINT '- 7 Tables with proper relationships and constraints';
PRINT '- 15 Indexes for optimal performance';
PRINT '- 5 Stored procedures for business operations';
PRINT '- 7 Update triggers for audit trail';
PRINT '';
PRINT 'Next Steps:';
PRINT '1. Run initialize-erp-sample-data.sql to add sample data';
PRINT '2. Enable CDC on all tables using the CDC Testing Framework';
PRINT '3. Run simulate-erp-business-workflow.sql for testing scenarios';
PRINT '4. Use cleanup-erp-test-objects.sql to remove all objects when done';
PRINT '';
PRINT 'Available Procedures:';
PRINT '- EXEC usp_CreateSalesOrder @CustomerName=''Test Customer'', @SalesOrderId=@OrderId OUTPUT, @OrderNumber=@OrderNum OUTPUT';
PRINT '- EXEC usp_InvoiceSalesOrder @SalesOrderId=1, @ArTransactionId=@ArId OUTPUT, @GlTransactionId=@GlId OUTPUT';
PRINT '- EXEC usp_PostGlTransaction @GlTransactionId=1';
PRINT '- EXEC usp_DeleteSalesOrder @SalesOrderId=1';
PRINT '- EXEC usp_GetAccountBalance @AccountId=1, @Balance=@Bal OUTPUT';
PRINT '';

GO