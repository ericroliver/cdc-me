# ERP Test Database Scripts

This directory contains SQL scripts for creating and managing a simplified ERP test database designed specifically for CDC (Change Data Capture) testing with the CDC Testing Framework.

## Overview

The ERP test database simulates a basic order-to-cash business flow with essential Accounts Receivable (AR) and General Ledger (GL) integration. It's designed to generate rich CDC data for comprehensive testing of the CDC Testing Framework's capabilities.

## Files

### Core Scripts

- **`create-erp-test-objects.sql`** - Complete setup script that creates all ERP test objects
- **`cleanup-erp-test-objects.sql`** - Complete teardown script that removes all ERP test objects
- **`test-erp-scripts.sql`** - Validation script to test the setup and cleanup functionality

### Documentation

- **`../docs/erp-test-database.md`** - Comprehensive documentation of the ERP database design
- **`README-erp-test-scripts.md`** - This file with usage instructions

## Quick Start

### Prerequisites

1. SQL Server 2016+ (Standard/Enterprise Edition for CDC support)
2. Database named `cdctest` (as configured in TEST_DB_CONNECTION)
3. Appropriate permissions (db_owner recommended)

### Basic Usage

1. **Setup ERP Test Objects:**

   ```sql
   -- Execute against cdctest database
   :r create-erp-test-objects.sql
   ```

2. **Test the Installation:**

   ```sql
   -- Execute against cdctest database
   :r test-erp-scripts.sql
   ```

3. **Clean Up When Done:**
   ```sql
   -- Execute against cdctest database
   :r cleanup-erp-test-objects.sql
   ```

## Database Objects Created

### Tables (7 total)

- `ChartOfAccounts` - GL account master data
- `SalesOrder` - Sales order headers
- `SalesOrderDetail` - Sales order line items
- `ArTransaction` - AR transaction headers (invoices, payments, credits)
- `ArTransactionDetail` - AR transaction line items
- `GlTransaction` - GL transaction headers
- `GlTransactionDetail` - GL transaction line items

### Stored Procedures (5 total)

- `usp_CreateSalesOrder` - Create new sales orders
- `usp_DeleteSalesOrder` - Delete sales orders (with validation)
- `usp_InvoiceSalesOrder` - Convert orders to invoices (creates AR and GL transactions)
- `usp_PostGlTransaction` - Post GL transactions
- `usp_GetAccountBalance` - Calculate account balances

### Additional Objects

- 15 Performance indexes
- 7 Update triggers for audit trail
- Sample data for immediate testing

## CDC Testing Scenarios

The ERP database supports these CDC testing scenarios:

### Scenario 1: Order Creation

```sql
DECLARE @OrderId INT, @OrderNum VARCHAR(20);
EXEC usp_CreateSalesOrder
    @CustomerName = 'Test Customer',
    @CustomerEmail = 'test@example.com',
    @Notes = 'CDC Test Order',
    @SalesOrderId = @OrderId OUTPUT,
    @OrderNumber = @OrderNum OUTPUT;
```

**CDC Impact:** Inserts into SalesOrder table

### Scenario 2: Order Invoicing

```sql
DECLARE @ArId INT, @GlId INT;
EXEC usp_InvoiceSalesOrder
    @SalesOrderId = 1,
    @ArTransactionId = @ArId OUTPUT,
    @GlTransactionId = @GlId OUTPUT;
```

**CDC Impact:** Updates SalesOrder, inserts into ArTransaction, ArTransactionDetail, GlTransaction, GlTransactionDetail

### Scenario 3: GL Posting

```sql
EXEC usp_PostGlTransaction @GlTransactionId = 1;
```

**CDC Impact:** Updates GlTransaction (status change)

### Scenario 4: Order Deletion

```sql
EXEC usp_DeleteSalesOrder @SalesOrderId = 1;
```

**CDC Impact:** Updates SalesOrder (status), deletes SalesOrderDetail, deletes SalesOrder

## Integration with CDC Testing Framework

### Step 1: Setup ERP Objects

```bash
# Execute the setup script
sqlcmd -S blue.local -d cdctest -U sa -P "A123_Z321!" -i create-erp-test-objects.sql
```

### Step 2: Enable CDC

```bash
# Use the CDC Testing Framework CLI
cdc-proto init --connection-string "Server=blue.local;Database=cdctest;User Id=sa;Password=A123_Z321!;TrustServerCertificate=true;"
```

### Step 3: Create Baseline Snapshot

```bash
# Create initial snapshot
cdc-proto snapshot create --name "erp-baseline"
```

### Step 4: Execute Business Scenarios

```sql
-- Run various stored procedures to generate CDC data
EXEC usp_CreateSalesOrder @CustomerName = 'CDC Test Customer', ...
EXEC usp_InvoiceSalesOrder @SalesOrderId = 1, ...
```

### Step 5: Generate CDC Profile

```bash
# Generate profile of changes
cdc-proto profile --output erp-test-profile.json
```

### Step 6: Compare Profiles

```bash
# Compare different test runs
cdc-proto diff --left baseline.json --right optimized.json --output differences.json
```

### Step 7: Cleanup

```bash
# Remove ERP objects when done
sqlcmd -S blue.local -d cdctest -U sa -P "A123_Z321!" -i cleanup-erp-test-objects.sql
```

## Sample Test Workflow

Here's a complete example of using the ERP test database with the CDC Testing Framework:

```bash
#!/bin/bash
# Complete ERP CDC Testing Workflow

# 1. Setup ERP test objects
echo "Setting up ERP test objects..."
sqlcmd -S blue.local -d cdctest -U sa -P "A123_Z321!" -i create-erp-test-objects.sql

# 2. Initialize CDC
echo "Initializing CDC..."
cdc-proto init --connection-string "Server=blue.local;Database=cdctest;User Id=sa;Password=A123_Z321!;TrustServerCertificate=true;"

# 3. Create baseline snapshot
echo "Creating baseline snapshot..."
cdc-proto snapshot create --name "erp-baseline"

# 4. Generate baseline profile
echo "Generating baseline profile..."
cdc-proto profile --output baseline-profile.json

# 5. Execute test scenario (via SQL script or application)
echo "Executing test scenario..."
sqlcmd -S blue.local -d cdctest -U sa -P "A123_Z321!" -Q "
DECLARE @OrderId INT, @OrderNum VARCHAR(20), @ArId INT, @GlId INT;
EXEC usp_CreateSalesOrder @CustomerName = 'CDC Test Customer', @SalesOrderId = @OrderId OUTPUT, @OrderNumber = @OrderNum OUTPUT;
INSERT INTO SalesOrderDetail (SalesOrderId, LineNumber, ProductCode, ProductDescription, Quantity, UnitPrice) VALUES (@OrderId, 1, 'TEST-WIDGET', 'Test Widget', 10, 50.00);
UPDATE SalesOrder SET SubTotal = 500.00, TaxAmount = 40.00, TotalAmount = 540.00 WHERE SalesOrderId = @OrderId;
EXEC usp_InvoiceSalesOrder @SalesOrderId = @OrderId, @ArTransactionId = @ArId OUTPUT, @GlTransactionId = @GlId OUTPUT;
EXEC usp_PostGlTransaction @GlTransactionId = @GlId;
"

# 6. Generate test profile
echo "Generating test profile..."
cdc-proto profile --output test-profile.json

# 7. Compare profiles
echo "Comparing profiles..."
cdc-proto diff --left baseline-profile.json --right test-profile.json --output test-differences.json

# 8. Display results
echo "Test completed. Check test-differences.json for results."

# 9. Cleanup (optional)
# sqlcmd -S blue.local -d cdctest -U sa -P "A123_Z321!" -i cleanup-erp-test-objects.sql
```

## Troubleshooting

### Common Issues

1. **"CDC is not supported on this edition of SQL Server"**

   - Solution: Use SQL Server Standard or Enterprise Edition

2. **"User does not have permission to perform this action"**

   - Solution: Grant db_owner role or specific CDC permissions

3. **"The transaction log for database is full"**

   - Solution: Backup transaction log or switch to SIMPLE recovery model temporarily

4. **Foreign key constraint errors during cleanup**
   - Solution: Run cleanup script again, or manually drop constraints first

### Validation

Use the test script to validate your installation:

```sql
-- Execute against cdctest database
:r test-erp-scripts.sql
```

The test script will:

- Verify all objects were created correctly
- Test stored procedure functionality
- Validate data integrity
- Provide a summary report

## Performance Considerations

- All tables use identity-based primary keys for optimal CDC performance
- Strategic indexing on frequently queried columns
- Minimal computed columns to reduce CDC overhead
- Efficient cascade delete operations

## Security Notes

- Scripts use default SQL Server authentication
- Consider Windows Authentication for production environments
- All procedures include proper error handling and transaction management
- Audit trail maintained through CreatedDate/ModifiedDate columns

## Support

For issues or questions:

1. Check the comprehensive documentation in `../docs/erp-test-database.md`
2. Run the validation script `test-erp-scripts.sql`
3. Review the CDC Testing Framework documentation
4. Check SQL Server error logs for detailed error messages

## Version History

- **v1.0** - Initial release with complete ERP test database
  - 7 tables with proper relationships
  - 5 stored procedures for business operations
  - Comprehensive sample data
  - Full cleanup capability
  - CDC Testing Framework integration
