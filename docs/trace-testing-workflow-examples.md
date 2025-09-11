# SQL Tracing and Replicatable Testing - Workflow Examples

## Overview

This document provides practical examples of how to use the SQL tracing and replicatable testing environment. It includes complete workflows, command examples, and expected outputs.

## Complete Testing Workflow Example

### Scenario: Testing a Stored Procedure Optimization

Let's walk through a complete example where we want to test an optimized version of a stored procedure to ensure it produces the same data changes as the original.

#### Step 1: Initial Setup

```bash
# Set up environment variables
export TEST_DB_CONN="Server=test-server;Database=SalesDB;User Id=testuser;Password=testpass;"
export TRACE_DB_CONN="Server=trace-server;Database=CDC_TraceDB;User Id=traceuser;Password=tracepass;"

# Initialize CDC on the test database
cdc-proto init --connection "$TEST_DB_CONN"
```

#### Step 2: Create Baseline Snapshot

```bash
# Create a named snapshot of the current database state
cdc-proto snapshot create --database SalesDB --name baseline_snapshot_v1

# Verify snapshot was created
cdc-proto snapshot list
```

Expected output:

```
Successfully created snapshot: baseline_snapshot_v1

Available Snapshots:
- baseline_snapshot_v1 (Source: SalesDB, Created: 2024-01-15 10:30:00, Size: 2.1 GB)
```

#### Step 3: Start Tracing Session

```bash
# Start trace session with specific configuration
cdc-proto trace start \
  --database SalesDB \
  --session "proc_optimization_test_v1" \
  --trace-db "$TRACE_DB_CONN" \
  --description "Testing optimized ProcessMonthlyOrders procedure" \
  --exclude-patterns "SELECT%,sys.%,INFORMATION_SCHEMA%"
```

Expected output:

```
Starting trace session: proc_optimization_test_v1
Session ID: 12345678-1234-1234-1234-123456789012
Trace session started successfully
```

#### Step 4: Enable CDC and Run Original Scenario

```bash
# CDC should already be enabled from step 1, but let's verify
cdc-proto init --connection "$TEST_DB_CONN"

# Run the original test scenario (this would be your application or test script)
# For this example, let's assume we have a test script
./run-monthly-processing-test.sh original_procedure
```

#### Step 5: Stop Trace and Capture CDC Data

```bash
# Stop the trace session
cdc-proto trace stop --session-id 12345678-1234-1234-1234-123456789012

# Generate CDC profile (baseline capture)
cdc-proto profile --connection "$TEST_DB_CONN" --out baseline_cdc_profile.json

# Export trace data to trace database
cdc-proto trace export --session-id 12345678-1234-1234-1234-123456789012
```

Expected output:

```
Trace session stopped successfully
Captured 1,247 trace events
CDC profile generated: baseline_cdc_profile.json (15 tables, 3,421 changes)
Trace data exported to trace database
```

#### Step 6: Restore Snapshot and Replay

```bash
# Restore database to baseline state
cdc-proto snapshot restore --database SalesDB --snapshot baseline_snapshot_v1

# Re-enable CDC after restore
cdc-proto init --connection "$TEST_DB_CONN"

# Replay the captured trace
cdc-proto replay execute \
  --session-id 12345678-1234-1234-1234-123456789012 \
  --target-database SalesDB \
  --connection "$TEST_DB_CONN" \
  --continue-on-error false
```

Expected output:

```
Database restored from snapshot: baseline_snapshot_v1
CDC re-enabled on database
Replaying trace session: proc_optimization_test_v1
Processed 1,247 statements
- Successful: 1,245
- Failed: 0
- Skipped: 2 (SELECT statements)
Replay completed successfully
```

#### Step 7: Capture CDC Data from Replay

```bash
# Generate CDC profile from replay
cdc-proto profile --connection "$TEST_DB_CONN" --out replay_cdc_profile.json
```

#### Step 8: Compare CDC Captures

```bash
# Compare baseline and replay CDC data
cdc-proto compare cdc \
  --left baseline_cdc_profile.json \
  --right replay_cdc_profile.json \
  --out baseline_vs_replay_comparison.json \
  --exclude-columns "LastModified,CreatedDate,__$start_lsn,__$end_lsn"
```

Expected output:

```
Comparing CDC captures...
Tables compared: 15
Matches: 15
Differences: 0
Overall result: MATCH ✓
Comparison saved to: baseline_vs_replay_comparison.json
```

#### Step 9: Test Optimized Procedure

```bash
# Restore snapshot again for optimized test
cdc-proto snapshot restore --database SalesDB --snapshot baseline_snapshot_v1

# Deploy optimized procedure
sqlcmd -S test-server -d SalesDB -i deploy-optimized-procedure.sql

# Re-enable CDC
cdc-proto init --connection "$TEST_DB_CONN"

# Run test with optimized procedure
./run-monthly-processing-test.sh optimized_procedure

# Generate CDC profile for optimized run
cdc-proto profile --connection "$TEST_DB_CONN" --out optimized_cdc_profile.json
```

#### Step 10: Final Comparison

```bash
# Compare baseline with optimized procedure results
cdc-proto compare cdc \
  --left baseline_cdc_profile.json \
  --right optimized_cdc_profile.json \
  --out baseline_vs_optimized_comparison.json \
  --exclude-columns "LastModified,CreatedDate,__$start_lsn,__$end_lsn"
```

Expected output:

```
Comparing CDC captures...
Tables compared: 15
Matches: 15
Differences: 0
Overall result: MATCH ✓
Comparison saved to: baseline_vs_optimized_comparison.json

✓ Optimized procedure produces identical data changes!
```

#### Step 11: Cleanup

```bash
# Drop the snapshot when testing is complete
cdc-proto snapshot drop --name baseline_snapshot_v1

# Optionally disable CDC if no longer needed
cdc-proto teardown --connection "$TEST_DB_CONN"
```

## API-Based Workflow Example

### Using REST API for Automated Testing

```bash
# Start trace session via API
curl -X POST "http://api-server/api/traces/start" \
  -H "Content-Type: application/json" \
  -d '{
    "databaseName": "SalesDB",
    "sessionName": "automated_test_001",
    "description": "Automated regression test",
    "eventTypes": ["sql_batch_completed", "rpc_completed"],
    "excludePatterns": ["SELECT%", "sys.%"],
    "ringBufferSizeMB": 64
  }'
```

Response:

```json
{
  "sessionId": "12345678-1234-1234-1234-123456789012",
  "sessionName": "automated_test_001",
  "status": "Active",
  "startTime": "2024-01-15T10:30:00Z"
}
```

```bash
# Check trace status
curl "http://api-server/api/traces/status/12345678-1234-1234-1234-123456789012"
```

Response:

```json
{
  "sessionId": "12345678-1234-1234-1234-123456789012",
  "state": "Running",
  "startedAt": "2024-01-15T10:30:00Z",
  "eventCount": 1247
}
```

```bash
# Stop trace and export data
curl -X POST "http://api-server/api/traces/stop/12345678-1234-1234-1234-123456789012"

# Execute replay
curl -X POST "http://api-server/api/test-workflow/replay/12345678-1234-1234-1234-123456789012" \
  -H "Content-Type: application/json" \
  -d '{
    "skipSelectStatements": true,
    "continueOnError": false,
    "maxConcurrentConnections": 1
  }'
```

## Advanced Workflow Examples

### Multi-Environment Testing

```bash
# Test across development, staging, and production-like environments
for env in dev staging prod; do
  echo "Testing environment: $env"

  # Create environment-specific snapshot
  cdc-proto snapshot create --database "SalesDB_$env" --name "baseline_$env"

  # Start trace
  session_id=$(cdc-proto trace start --database "SalesDB_$env" --session "test_$env" --json | jq -r '.sessionId')

  # Run tests
  ./run-tests.sh $env

  # Stop trace and capture
  cdc-proto trace stop --session-id $session_id
  cdc-proto profile --connection "$env_connection" --out "profile_$env.json"

  # Compare with baseline if not first environment
  if [ "$env" != "dev" ]; then
    cdc-proto compare cdc --left profile_dev.json --right "profile_$env.json" --out "dev_vs_$env.json"
  fi
done
```

### Performance Regression Testing

```bash
#!/bin/bash
# performance-regression-test.sh

# Configuration
TEST_NAME="performance_regression_$(date +%Y%m%d_%H%M%S)"
BASELINE_SNAPSHOT="perf_baseline"
DATABASE="PerformanceTestDB"

echo "Starting performance regression test: $TEST_NAME"

# Create baseline snapshot
cdc-proto snapshot create --database $DATABASE --name $BASELINE_SNAPSHOT

# Test original implementation
echo "Testing original implementation..."
session_id_original=$(cdc-proto trace start --database $DATABASE --session "${TEST_NAME}_original" --json | jq -r '.sessionId')

# Run performance test suite
./run-performance-tests.sh original

# Capture results
cdc-proto trace stop --session-id $session_id_original
cdc-proto profile --out "profile_original.json"

# Restore and test optimized implementation
echo "Testing optimized implementation..."
cdc-proto snapshot restore --database $DATABASE --snapshot $BASELINE_SNAPSHOT

# Deploy optimized code
./deploy-optimized-code.sh

session_id_optimized=$(cdc-proto trace start --database $DATABASE --session "${TEST_NAME}_optimized" --json | jq -r '.sessionId')

# Run same performance test suite
./run-performance-tests.sh optimized

# Capture results
cdc-proto trace stop --session-id $session_id_optimized
cdc-proto profile --out "profile_optimized.json"

# Compare results
echo "Comparing results..."
cdc-proto compare cdc \
  --left profile_original.json \
  --right profile_optimized.json \
  --out comparison_results.json

# Generate report
if [ $? -eq 0 ]; then
  echo "✓ Performance optimization maintains data consistency"

  # Extract performance metrics from trace data
  echo "Performance Metrics:"
  echo "Original - Total Duration: $(get_trace_duration $session_id_original)"
  echo "Optimized - Total Duration: $(get_trace_duration $session_id_optimized)"
else
  echo "✗ Performance optimization changed data results"
  exit 1
fi

# Cleanup
cdc-proto snapshot drop --name $BASELINE_SNAPSHOT
```

### Continuous Integration Integration

```yaml
# .github/workflows/database-regression-test.yml
name: Database Regression Test

on:
  pull_request:
    paths:
      - "database/**"
      - "stored-procedures/**"

jobs:
  database-regression:
    runs-on: ubuntu-latest

    services:
      sqlserver:
        image: mcr.microsoft.com/mssql/server:2019-latest
        env:
          SA_PASSWORD: ${{ secrets.SA_PASSWORD }}
          ACCEPT_EULA: Y
        ports:
          - 1433:1433

    steps:
      - uses: actions/checkout@v2

      - name: Setup .NET
        uses: actions/setup-dotnet@v1
        with:
          dotnet-version: 6.0.x

      - name: Build CDC Tools
        run: |
          dotnet build cdc-proto/cdc-utility.csproj
          dotnet publish cdc-proto/cdc-utility.csproj -o ./cdc-tools

      - name: Setup Test Database
        run: |
          sqlcmd -S localhost -U sa -P $SA_PASSWORD -Q "CREATE DATABASE TestDB"
          sqlcmd -S localhost -U sa -P $SA_PASSWORD -d TestDB -i database/schema.sql
          sqlcmd -S localhost -U sa -P $SA_PASSWORD -d TestDB -i database/test-data.sql

      - name: Run Regression Test
        run: |
          export TEST_CONNECTION="Server=localhost;Database=TestDB;User Id=sa;Password=$SA_PASSWORD;"
          export TRACE_CONNECTION="Server=localhost;Database=TraceDB;User Id=sa;Password=$SA_PASSWORD;"

          # Initialize CDC
          ./cdc-tools/cdc-utility init --connection "$TEST_CONNECTION"

          # Create baseline snapshot
          ./cdc-tools/cdc-utility snapshot create --database TestDB --name ci_baseline

          # Test original procedures
          ./cdc-tools/cdc-utility trace start --database TestDB --session "ci_original"
          ./run-integration-tests.sh original
          ./cdc-tools/cdc-utility trace stop --session-id $ORIGINAL_SESSION_ID
          ./cdc-tools/cdc-utility profile --out profile_original.json

          # Test modified procedures
          ./cdc-tools/cdc-utility snapshot restore --database TestDB --snapshot ci_baseline
          sqlcmd -S localhost -U sa -P $SA_PASSWORD -d TestDB -i stored-procedures/modified/*.sql

          ./cdc-tools/cdc-utility trace start --database TestDB --session "ci_modified"
          ./run-integration-tests.sh modified
          ./cdc-tools/cdc-utility trace stop --session-id $MODIFIED_SESSION_ID
          ./cdc-tools/cdc-utility profile --out profile_modified.json

          # Compare results
          ./cdc-tools/cdc-utility compare cdc \
            --left profile_original.json \
            --right profile_modified.json \
            --out comparison.json

          if [ $? -ne 0 ]; then
            echo "Database changes affect data consistency!"
            exit 1
          fi

      - name: Upload Test Results
        uses: actions/upload-artifact@v2
        if: always()
        with:
          name: regression-test-results
          path: |
            profile_*.json
            comparison.json
```

## Configuration Examples

### Trace Configuration File

```json
{
  "traceConfiguration": {
    "databaseName": "SalesDB",
    "sessionName": "monthly_processing_test",
    "eventTypes": [
      "sql_batch_completed",
      "rpc_completed",
      "sp_statement_completed"
    ],
    "excludePatterns": [
      "SELECT%",
      "sys.%",
      "INFORMATION_SCHEMA%",
      "%_stats%",
      "sp_reset_connection"
    ],
    "ringBufferSizeMB": 128,
    "captureStatementText": true,
    "capturePerformanceMetrics": true,
    "description": "Testing monthly order processing optimization"
  },
  "comparisonConfiguration": {
    "excludedColumns": [
      "__$start_lsn",
      "__$end_lsn",
      "__$seqval",
      "__$update_mask",
      "LastModified",
      "CreatedDate",
      "Timestamp",
      "ModifiedDate",
      "ProcessedDate"
    ],
    "dateTimeToleranceWindow": "24:00:00",
    "ignoreIdentityColumns": true,
    "ignoreComputedColumns": true,
    "customExcludePatterns": ["*_audit_*", "*_log_*"]
  },
  "replayOptions": {
    "skipSelectStatements": true,
    "skipSystemStatements": true,
    "continueOnError": false,
    "maxConcurrentConnections": 1,
    "statementTimeout": "00:00:30",
    "additionalExcludePatterns": ["BACKUP%", "RESTORE%", "DBCC%"]
  }
}
```

### Environment Configuration

```json
{
  "environments": {
    "development": {
      "testConnection": "Server=dev-sql;Database=SalesDB_Dev;Integrated Security=true;",
      "traceConnection": "Server=dev-sql;Database=TraceDB_Dev;Integrated Security=true;"
    },
    "staging": {
      "testConnection": "Server=staging-sql;Database=SalesDB_Staging;User Id=testuser;Password=testpass;",
      "traceConnection": "Server=trace-sql;Database=TraceDB_Staging;User Id=traceuser;Password=tracepass;"
    },
    "production": {
      "testConnection": "Server=prod-sql;Database=SalesDB;User Id=readonly;Password=readonlypass;",
      "traceConnection": "Server=trace-sql;Database=TraceDB_Prod;User Id=traceuser;Password=tracepass;"
    }
  },
  "defaultConfiguration": {
    "snapshotRetentionDays": 7,
    "traceRetentionDays": 30,
    "maxConcurrentSessions": 5,
    "defaultRingBufferSizeMB": 64
  }
}
```

## Troubleshooting Common Scenarios

### Scenario 1: Trace Replay Fails

```bash
# Check trace session status
cdc-proto trace status --session-id 12345678-1234-1234-1234-123456789012

# If replay fails, examine the error details
cdc-proto replay execute --session-id 12345678-1234-1234-1234-123456789012 --continue-on-error true --verbose

# Export failed statements for analysis
cdc-proto trace export --session-id 12345678-1234-1234-1234-123456789012 --filter-failed-only --output failed_statements.json
```

### Scenario 2: CDC Comparison Shows Unexpected Differences

```bash
# Generate detailed difference report
cdc-proto compare cdc \
  --left profile1.json \
  --right profile2.json \
  --out detailed_diff.json \
  --include-unchanged false \
  --verbose

# Analyze specific table differences
cdc-proto compare cdc \
  --left profile1.json \
  --right profile2.json \
  --table-filter "Orders,OrderDetails" \
  --out orders_diff.json
```

### Scenario 3: Snapshot Restore Issues

```bash
# Check snapshot status
cdc-proto snapshot list --verbose

# Force restore with additional options
cdc-proto snapshot restore \
  --database SalesDB \
  --snapshot baseline_snapshot \
  --force \
  --timeout 300
```

This comprehensive workflow guide provides practical examples for implementing and using the SQL tracing and replicatable testing environment in various scenarios, from simple manual testing to complex CI/CD integration.
