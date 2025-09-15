# ERP Test Scripts Documentation

This directory contains SQL scripts for setting up and testing a simplified ERP system for CDC (Change Data Capture) testing. The scripts have been organized into separate concerns for better maintainability and testing flexibility.

## Script Overview

### 1. Database Structure Setup

- **[`create-erp-database-objects.sql`](create-erp-database-objects.sql)** - Creates all database objects (tables, indexes, stored procedures, triggers) without any data

### 2. Sample Data Initialization

- **[`initialize-erp-sample-data.sql`](initialize-erp-sample-data.sql)** - Inserts sample data for testing (Chart of Accounts, sample sales orders)

### 3. Business Workflow Simulation

- **[`simulate-erp-business-workflow.sql`](simulate-erp-business-workflow.sql)** - Simulates realistic business operations for CDC testing

### 4. Cleanup and Testing

- **[`cleanup-erp-test-objects.sql`](cleanup-erp-test-objects.sql)** - Removes all ERP test objects and data
- **[`test-erp-scripts.sql`](test-erp-scripts.sql)** - Basic validation tests for the ERP system

### 5. Legacy Scripts

- **[`create-erp-test-objects.sql`](create-erp-test-objects.sql)** - Original combined script (deprecated - use separated scripts above)

## Usage Instructions

### Initial Setup (Run Once)

1. **Create Database Objects**

   ```sql
   -- Run against cdctest database
   -- Creates tables, indexes, stored procedures, triggers
   :r create-erp-test-objects.sql
   ```

2. **Initialize Sample Data**
   ```sql
   -- Run after database objects are created
   -- Adds Chart of Accounts and sample sales orders
   :r initialize-erp-sample-data.sql
   ```

### CDC Testing Workflow (Repeatable)

3. **Enable CDC** (using CDC Testing Framework)

   ```bash
   # Enable CDC on all ERP tables
   cdc-utility init --provider sqlserver --connection-string "your-connection-string"
   ```

4. **Create Database Snapshot**

   ```bash
   # Create baseline snapshot
   cdc-utility snapshot create --name "erp-baseline"
   ```

5. **Run Business Simulation**

   ```sql
   -- Simulate business operations (can be run multiple times)
   :r simulate-erp-business-workflow.sql
   ```

6. **Capture and Analyze Changes**

   ```bash
   # Profile CDC data
   cdc-utility profile --output-file "erp-changes.json"

   # Create comparison snapshot
   cdc-utility snapshot create --name "erp-after-simulation"
   ```

7. **Test Replay Functionality**

   ```bash
   # Restore to baseline
   cdc-utility snapshot restore --name "erp-baseline"

   # Replay captured changes
   cdc-utility replay --input-file "erp-changes.json"

   # Verify results match
   cdc-utility diff --snapshot1 "erp-after-simulation" --snapshot2 "current"
   ```

### Cleanup (When Done)

8. **Remove All Objects**
   ```sql
   -- Clean up all ERP test objects and data
   :r cleanup-erp-test-objects.sql
   ```

## Database Schema

### Tables Created

| Table                 | Purpose                | Key Features                          |
| --------------------- | ---------------------- | ------------------------------------- |
| `ChartOfAccounts`     | GL account master      | Hierarchical structure, account types |
| `SalesOrder`          | Order headers          | Status tracking, customer info        |
| `SalesOrderDetail`    | Order line items       | Calculated line totals                |
| `ArTransaction`       | AR transaction headers | Invoice/payment tracking              |
| `ArTransactionDetail` | AR transaction details | Links to order details                |
| `GlTransaction`       | GL transaction headers | Posting status, balanced entries      |
| `GlTransactionDetail` | GL transaction details | Debit/credit entries                  |

### Stored Procedures

| Procedure               | Purpose                    | Key Operations                    |
| ----------------------- | -------------------------- | --------------------------------- |
| `usp_CreateSalesOrder`  | Create new sales orders    | Auto-generates order numbers      |
| `usp_DeleteSalesOrder`  | Delete sales orders        | Validates status, audit trail     |
| `usp_InvoiceSalesOrder` | Convert orders to invoices | Creates AR and GL transactions    |
| `usp_PostGlTransaction` | Post GL transactions       | Validates balance, updates status |
| `usp_GetAccountBalance` | Calculate account balances | Handles normal balance types      |

## Business Workflow Simulation

The [`simulate-erp-business-workflow.sql`](simulate-erp-business-workflow.sql) script performs these realistic business operations:

### Scenario 1: Order Creation

- Creates new sales orders for different customer types
- Adds line items with various products and services
- Updates order totals with tax calculations

### Scenario 2: Order Invoicing

- Converts open sales orders to invoices
- Generates AR transactions for customer billing
- Creates corresponding GL transactions for revenue recognition

### Scenario 3: GL Posting

- Posts GL transactions to the general ledger
- Validates debit/credit balance requirements
- Updates posting status and dates

### Scenario 4: Payment Processing

- Processes customer payments (partial and full)
- Updates AR transaction statuses
- Creates payment transaction records

### Scenario 5: Order Modifications

- Cancels orders with audit trail
- Modifies existing orders (adds line items)
- Updates totals and notes

### Scenario 6: Reporting

- Calculates account balances for key accounts
- Generates transaction summary reports
- Displays current system state

## CDC Testing Benefits

This ERP simulation provides excellent CDC testing scenarios because it:

1. **Generates Complex Relationships** - Changes cascade through related tables
2. **Creates Realistic Data Patterns** - Mimics actual business workflows
3. **Produces Varied Change Types** - Inserts, updates, deletes across multiple tables
4. **Tests Transaction Integrity** - Multi-table transactions test CDC consistency
5. **Enables Repeatability** - Can be run multiple times with database restoration

## Sample Data

### Chart of Accounts

- Basic GL account structure with Assets, Liabilities, Equity, Revenue, and Expenses
- Includes common business accounts like Cash, AR, Sales Revenue, etc.

### Sales Orders

- 5 sample orders with different customer types and product mixes
- Realistic pricing, quantities, and delivery dates
- Various order statuses for testing different scenarios

## Error Handling

All stored procedures include:

- Transaction management with rollback on errors
- Comprehensive error messages with context
- Status validation before operations
- Audit trail maintenance

## Performance Considerations

- Indexes on all foreign keys and frequently queried columns
- Computed columns for calculated values
- Efficient cursor usage in batch operations
- Minimal locking through proper transaction scope

## Troubleshooting

### Common Issues

1. **Foreign Key Violations**

   - Ensure scripts are run in correct order
   - Check that sample data exists before running simulations

2. **CDC Not Capturing Changes**

   - Verify CDC is enabled on all tables
   - Check CDC job status and configuration

3. **Balance Validation Errors**

   - GL transactions must have equal debits and credits
   - Check account mappings in stored procedures

4. **Permission Issues**
   - Ensure database user has appropriate permissions
   - CDC requires elevated privileges

### Validation Queries

```sql
-- Check object creation
SELECT 'Tables' as ObjectType, COUNT(*) as Count FROM sys.tables WHERE name LIKE '%[CSG]%'
UNION ALL
SELECT 'Procedures', COUNT(*) FROM sys.procedures WHERE name LIKE 'usp_%'
UNION ALL
SELECT 'Triggers', COUNT(*) FROM sys.triggers WHERE name LIKE 'tr_%';

-- Check data integrity
SELECT
    so.OrderNumber,
    so.OrderStatus,
    so.TotalAmount,
    COUNT(sod.SalesOrderDetailId) as LineItems
FROM SalesOrder so
LEFT JOIN SalesOrderDetail sod ON so.SalesOrderId = sod.SalesOrderId
GROUP BY so.OrderNumber, so.OrderStatus, so.TotalAmount
ORDER BY so.OrderNumber;

-- Check GL balance
SELECT
    gt.TransactionNumber,
    gt.IsPosted,
    SUM(ISNULL(gtd.DebitAmount, 0)) as TotalDebits,
    SUM(ISNULL(gtd.CreditAmount, 0)) as TotalCredits
FROM GlTransaction gt
LEFT JOIN GlTransactionDetail gtd ON gt.GlTransactionId = gtd.GlTransactionId
GROUP BY gt.TransactionNumber, gt.IsPosted
HAVING SUM(ISNULL(gtd.DebitAmount, 0)) != SUM(ISNULL(gtd.CreditAmount, 0));
```

## Next Steps

After running these scripts:

1. Enable CDC on all created tables
2. Run the simulation script multiple times with snapshots
3. Use the CDC Testing Framework to capture and replay changes
4. Analyze the captured data for testing database optimizations
5. Clean up when testing is complete

For more information about the CDC Testing Framework, see the main project documentation.
