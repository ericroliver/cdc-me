# Workflow Commands Documentation

## Overview

The Workflow command group provides orchestrated multi-step test workflows that combine CDC operations, snapshots, trace sessions, and data comparison into automated test sequences. These workflows can run for extended periods (minutes to hours) and provide comprehensive database change validation.

## Command Structure

```bash
cdc-cli workflow <subcommand> [options]
```

Available subcommands:
- [`execute`](#workflow-execute) - Execute a complete test workflow
- [`status`](#workflow-status) - Get workflow execution status
- [`list`](#workflow-list) - List workflow executions

## Workflow Execute

Execute a complete test workflow that orchestrates snapshot creation, CDC enablement, trace capture, workload execution, and data comparison.

### Synopsis

```bash
cdc-cli workflow execute --file <path> [--async] [--poll-interval <seconds>]
cdc-cli workflow execute --data '<json>' [--async]
cat workflow.json | cdc-cli workflow execute [--async]
```

### Options

| Option | Alias | Description | Required | Default |
|--------|-------|-------------|----------|---------|
| `--file` | `-f` | Path to workflow JSON file | No* | - |
| `--data` | - | JSON payload as string | No* | - |
| `--async` | - | Return immediately with workflow ID | No | false |
| `--poll-interval` | - | Status polling interval (seconds) | No | 5 |

*At least one input method required (file, data, or stdin). File input is **strongly recommended** for complex workflows.

### Workflow Configuration Schema

```json
{
  "workflowName": "string (required)",
  "databaseName": "string (required)",
  "connectionString": "string (required)",
  "traceConnectionString": "string (required)",
  "baselineSnapshotName": "string (required)",
  "testSnapshotName": "string (required)",
  "traceSessionName": "string (required)",
  "enableCdc": "boolean (optional, default: true)",
  "baselineWorkloadPath": "string (optional)",
  "cdcTables": ["string array (optional)"],
  "traceConfig": {
    "maxFileSize": "number (optional, default: 100)",
    "maxFiles": "number (optional, default: 5)",
    "eventsToCapture": ["string array (optional)"],
    "filterCriteria": {}
  },
  "comparisonConfig": {
    "compareSchema": "boolean (optional)",
    "compareData": "boolean (optional)"
  }
}
```

### Execution Modes

#### Synchronous Mode (Default)

The CLI waits for workflow completion and displays the final result.

```bash
cdc-cli workflow execute --file test-workflow.json --output json-pretty
```

**Behavior:**
- Submits workflow to API
- Polls status at intervals (default: 5 seconds)
- Displays progress updates (in text mode)
- Returns when workflow completes or fails
- Exit code 0 on success, 1 on failure

**Note:** Pressing Ctrl+C during execution will cancel monitoring but the workflow continues running on the server. Use `workflow status` to check progress.

#### Asynchronous Mode

The CLI returns immediately with a workflow ID for later status checks.

```bash
cdc-cli workflow execute --file test-workflow.json --async
```

**Output:**
```json
{
  "workflowId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "status": "Running"
}
```

**Follow-up:**
```bash
# Check status
cdc-cli workflow status a1b2c3d4-e5f6-7890-abcd-ef1234567890

# Watch progress
cdc-cli workflow status a1b2c3d4-e5f6-7890-abcd-ef1234567890 --watch
```

### Workflow Execution Steps

A typical workflow executes these steps:

1. **Create Baseline Snapshot** - Capture initial database state
2. **Enable CDC** (optional) - Enable Change Data Capture
3. **Start Trace Capture** - Begin capturing SQL statements
4. **Execute Baseline Workload** (optional) - Run test workload
5. **Stop Trace Capture** - End statement capture
6. **Export Trace Data** - Save captured statements
7. **Create Test Snapshot** - Capture final database state
8. **Restore Baseline Snapshot** - Reset to initial state
9. **Replay Captured Statements** - Re-execute statements
10. **Compare CDC Data** (optional) - Validate data consistency
11. **Generate Test Report** - Compile results

### Complete Workflow Examples

#### Example 1: Basic Performance Test

Test a stored procedure optimization by comparing original and optimized versions.

```json
{
  "workflowName": "OrderProcessing_Performance_Test",
  "databaseName": "SalesDB",
  "connectionString": "Server=localhost;Database=SalesDB;User Id=cdctest;Password=test123;",
  "traceConnectionString": "Server=localhost;Database=CdcMe;User Id=cdctest;Password=test123;",
  "baselineSnapshotName": "baseline_orders",
  "testSnapshotName": "after_orders",
  "traceSessionName": "order_processing_test",
  "enableCdc": true,
  "baselineWorkloadPath": "/tests/workloads/process_orders.sql",
  "cdcTables": [
    "dbo.Orders",
    "dbo.OrderItems",
    "dbo.Inventory"
  ],
  "traceConfig": {
    "maxFileSize": 100,
    "maxFiles": 5,
    "eventsToCapture": ["sql_statement_completed"],
    "filterCriteria": {
      "database_name": "SalesDB"
    }
  },
  "comparisonConfig": {
    "compareSchema": false,
    "compareData": true
  }
}
```

**Usage:**
```bash
# Save to file
cat > order-test.json << 'EOF'
{
  "workflowName": "OrderProcessing_Performance_Test",
  ...
}
EOF

# Execute synchronously
cdc-cli workflow execute --file order-test.json --output text

# Or execute asynchronously for long-running tests
WORKFLOW_ID=$(cdc-cli workflow execute --file order-test.json --async --output json | jq -r '.workflowId')
echo "Workflow started: $WORKFLOW_ID"
cdc-cli workflow status $WORKFLOW_ID --watch
```

#### Example 2: Multi-Phase Testing Scenario

Test multiple database changes in sequence with validation at each step.

```json
{
  "workflowName": "Multi_Phase_Data_Migration",
  "databaseName": "ERPDB",
  "connectionString": "Server=localhost;Database=ERPDB;User Id=cdctest;Password=test123;",
  "traceConnectionString": "Server=localhost;Database=CdcMe;User Id=cdctest;Password=test123;",
  "baselineSnapshotName": "pre_migration",
  "testSnapshotName": "post_migration",
  "traceSessionName": "migration_trace",
  "enableCdc": true,
  "baselineWorkloadPath": "/tests/migrations/phase1_customer_migration.sql",
  "cdcTables": [
    "dbo.Customers",
    "dbo.CustomerAddresses",
    "dbo.CustomerContacts",
    "dbo.Orders"
  ],
  "traceConfig": {
    "maxFileSize": 200,
    "maxFiles": 10,
    "eventsToCapture": [
      "sql_statement_completed",
      "rpc_completed"
    ],
    "filterCriteria": {
      "database_name": "ERPDB",
      "duration": 1000
    }
  },
  "comparisonConfig": {
    "compareSchema": true,
    "compareData": true
  }
}
```

#### Example 3: Baseline vs Optimized Comparison

Compare original and optimized stored procedures to ensure identical results.

```json
{
  "workflowName": "Stored_Proc_Optimization_Validation",
  "databaseName": "AnalyticsDB",
  "connectionString": "Server=localhost;Database=AnalyticsDB;User Id=cdctest;Password=test123;",
  "traceConnectionString": "Server=localhost;Database=CdcMe;User Id=cdctest;Password=test123;",
  "baselineSnapshotName": "baseline_analytics",
  "testSnapshotName": "optimized_analytics",
  "traceSessionName": "analytics_optimization",
  "enableCdc": true,
  "baselineWorkloadPath": "/tests/workloads/daily_analytics.sql",
  "cdcTables": [
    "dbo.SalesMetrics",
    "dbo.CustomerMetrics",
    "dbo.ProductMetrics"
  ],
  "traceConfig": {
    "maxFileSize": 150,
    "maxFiles": 8
  },
  "comparisonConfig": {
    "compareData": true
  }
}
```

#### Example 4: CI/CD Integration

Workflow configuration for automated testing in CI/CD pipelines.

```json
{
  "workflowName": "CI_Regression_Test",
  "databaseName": "TestDB_${BUILD_NUMBER}",
  "connectionString": "${DB_CONNECTION_STRING}",
  "traceConnectionString": "${TRACE_CONNECTION_STRING}",
  "baselineSnapshotName": "ci_baseline_${BUILD_NUMBER}",
  "testSnapshotName": "ci_test_${BUILD_NUMBER}",
  "traceSessionName": "ci_trace_${BUILD_NUMBER}",
  "enableCdc": true,
  "baselineWorkloadPath": "/tests/regression/full_test_suite.sql",
  "cdcTables": [
    "dbo.Orders",
    "dbo.Products",
    "dbo.Customers"
  ],
  "traceConfig": {
    "maxFileSize": 100,
    "maxFiles": 5
  }
}
```

**CI/CD Script:**
```bash
#!/bin/bash
set -e

# Replace environment variables
envsubst < ci-workflow-template.json > ci-workflow.json

# Execute workflow
WORKFLOW_ID=$(cdc-cli workflow execute \
  --file ci-workflow.json \
  --async \
  --output json | jq -r '.workflowId')

echo "Workflow ID: $WORKFLOW_ID"

# Poll for completion
while true; do
  STATUS=$(cdc-cli workflow status $WORKFLOW_ID --output json | jq -r '.status')
  echo "Current status: $STATUS"
  
  if [ "$STATUS" == "Completed" ]; then
    echo "Workflow completed successfully"
    exit 0
  elif [ "$STATUS" == "Failed" ]; then
    echo "Workflow failed"
    cdc-cli workflow status $WORKFLOW_ID --output json-pretty
    exit 1
  fi
  
  sleep 10
done
```

### Exit Codes

- `0` - Success (workflow completed successfully)
- `1` - API error or workflow execution failed
- `2` - File I/O error
- `3` - Validation error (invalid configuration)

---

## Workflow Status

Check the execution status of a workflow, with optional continuous monitoring (watch mode).

### Synopsis

```bash
cdc-cli workflow status <workflow-id> [--watch] [--interval <seconds>]
cdc-cli workflow status --id <workflow-id> [--watch]
```

### Options

| Option | Alias | Description | Required | Default |
|--------|-------|-------------|----------|---------|
| `workflow-id` | - | Workflow ID (positional) | Yes* | - |
| `--id` | `--workflow` | Workflow ID (option) | Yes* | - |
| `--watch` | `-w` | Continuously poll and display updates | No | false |
| `--interval` | - | Polling interval in seconds | No | 5 |

*Either positional argument or `--id` option required

### Response Fields

- `workflowId` - Unique workflow identifier
- `name` - Workflow name from configuration
- `status` - Current status (pending, running, completed, failed, cancelled)
- `currentPhase` - Current execution phase/step
- `progress` - Progress percentage (0-100)
- `startTime` - Workflow start time (UTC)
- `duration` - Total duration (if completed)
- `estimatedCompletion` - Estimated completion time (if available)
- `errors` - Array of error messages (if any)
- `message` - Additional status information

### Status Values

| Status | Description | Terminal State |
|--------|-------------|----------------|
| `Pending` | Workflow queued, not yet started | No |
| `Running` | Workflow currently executing | No |
| `Completed` | Workflow finished successfully | Yes |
| `Failed` | Workflow encountered errors | Yes |
| `Cancelled` | Workflow was cancelled by user | Yes |

### Examples

#### Get Status Once

```bash
# Using positional argument
cdc-cli workflow status a1b2c3d4-e5f6-7890-abcd-ef1234567890

# Using option
cdc-cli workflow status --id a1b2c3d4-e5f6-7890-abcd-ef1234567890

# JSON output
cdc-cli workflow status a1b2c3d4-e5f6-7890-abcd-ef1234567890 --output json-pretty
```

**Output (JSON):**
```json
{
  "workflowId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "name": "OrderProcessing_Performance_Test",
  "status": "Running",
  "currentPhase": "Replay Captured Statements",
  "progress": 65.5,
  "startTime": "2024-01-15T14:30:00Z",
  "estimatedCompletion": "2024-01-15T15:00:00Z"
}
```

**Output (Text):**
```
Workflow ID: a1b2c3d4-e5f6-7890-abcd-ef1234567890
Name: OrderProcessing_Performance_Test
Status: ⟳ Running
Current Phase: Replay Captured Statements
Progress: 65.5%
Start Time: 2024-01-15 14:30:00 UTC
Estimated Completion: 25m 30s
```

#### Watch Mode (Continuous Monitoring)

```bash
# Watch with default 5-second interval
cdc-cli workflow status a1b2c3d4-e5f6-7890-abcd-ef1234567890 --watch

# Watch with custom interval
cdc-cli workflow status a1b2c3d4-e5f6-7890-abcd-ef1234567890 --watch --interval 10
```

**Behavior:**
- Polls workflow status at specified interval
- Updates display in place (text mode) or outputs each status (JSON mode)
- Shows progress indicators: ○ Pending, ⟳ Running, ✓ Completed, ✗ Failed, ⊗ Cancelled
- Exits automatically when workflow reaches terminal state
- Press Ctrl+C to stop watching (workflow continues running)
- Exit code 0 if workflow completed successfully, 1 if failed

#### Polling Script Example

```bash
#!/bin/bash
WORKFLOW_ID="a1b2c3d4-e5f6-7890-abcd-ef1234567890"

while true; do
  STATUS=$(cdc-cli workflow status $WORKFLOW_ID --output json | jq -r '.status')
  PROGRESS=$(cdc-cli workflow status $WORKFLOW_ID --output json | jq -r '.progress // 0')
  
  echo "[$(date)] Status: $STATUS, Progress: $PROGRESS%"
  
  if [ "$STATUS" == "Completed" ] || [ "$STATUS" == "Failed" ]; then
    break
  fi
  
  sleep 5
done

# Get final results
cdc-cli workflow status $WORKFLOW_ID --output json-pretty
```

### Exit Codes

- `0` - Success (status retrieved, or workflow completed in watch mode)
- `1` - API error or workflow failed
- `3` - Validation error (invalid workflow ID format)

---

## Workflow List

List recent workflow executions with optional filtering.

### Synopsis

```bash
cdc-cli workflow list [--status <status>] [--limit <number>] [--since <date>]
```

### Options

| Option | Alias | Description | Required | Default |
|--------|-------|-------------|----------|---------|
| `--status` | `-s` | Filter by status | No | all |
| `--limit` | `-l` | Maximum number of results | No | 50 |
| `--since` | - | Show workflows since date | No | all |

### Status Filter Values

- `pending` - Queued workflows
- `running` - Currently executing
- `completed` - Successfully finished
- `failed` - Failed execution
- `cancelled` - Cancelled by user

### Examples

#### List All Workflows

```bash
cdc-cli workflow list
```

**Output (Text):**
```
=============================================================================================================
ID                                   | Name                           | Status       | Start Time           | Duration    
-------------------------------------------------------------------------------------------------------------
a1b2c3d4-e5f6-7890-abcd-ef123456... | OrderProcessing_Test           | ✓ Completed  | 2024-01-15 14:30:00  | 12m 45s     
b2c3d4e5-f6a7-8901-bcde-f0123456... | Migration_Test                 | ⟳ Running    | 2024-01-15 15:00:00  | Running...  
c3d4e5f6-a7b8-9012-cdef-01234567... | Analytics_Optimization         | ✗ Failed     | 2024-01-15 13:00:00  | 2m 15s      
=============================================================================================================
Total: 3 workflow(s)
```

#### Filter by Status

```bash
# List only running workflows
cdc-cli workflow list --status running

# List only completed workflows
cdc-cli workflow list --status completed

# List only failed workflows for troubleshooting
cdc-cli workflow list --status failed --output json-pretty
```

#### Limit Results

```bash
# Get last 10 workflows
cdc-cli workflow list --limit 10

# Get last 100 workflows
cdc-cli workflow list --limit 100
```

#### Filter by Date

```bash
# Workflows since today
cdc-cli workflow list --since 2024-01-15

# Workflows since specific time
cdc-cli workflow list --since 2024-01-15T12:00:00Z

# Recent workflows from last hour
cdc-cli workflow list --since "$(date -u -d '1 hour ago' '+%Y-%m-%dT%H:%M:%SZ')"
```

#### Combined Filters

```bash
# Failed workflows from today
cdc-cli workflow list --status failed --since 2024-01-15

# Last 25 completed workflows
cdc-cli workflow list --status completed --limit 25

# Running workflows for monitoring
watch -n 5 'cdc-cli workflow list --status running --output text'
```

### JSON Output

```bash
cdc-cli workflow list --output json-pretty
```

```json
[
  {
    "workflowId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "workflowName": "OrderProcessing_Test",
    "status": "Completed",
    "startTime": "2024-01-15T14:30:00Z",
    "endTime": "2024-01-15T14:42:45Z",
    "success": true,
    "stepCount": 11,
    "progress": 100
  },
  {
    "workflowId": "b2c3d4e5-f6a7-8901-bcde-f01234567890",
    "workflowName": "Migration_Test",
    "status": "Running",
    "startTime": "2024-01-15T15:00:00Z",
    "endTime": null,
    "success": false,
    "stepCount": 11,
    "progress": 45.5
  }
]
```

### Exit Codes

- `0` - Success (workflows listed)
- `1` - API error
- `3` - Validation error (invalid status or limit)

---

## Best Practices

### Workflow Configuration

1. **Use Descriptive Names**: Include date, purpose, and environment
   ```json
   "workflowName": "OrderProc_Optimization_2024-01-15_Dev"
   ```

2. **Externalize Connection Strings**: Use environment variables in CI/CD
   ```json
   "connectionString": "${DB_CONNECTION_STRING}"
   ```

3. **Specify CDC Tables**: Only monitor tables that change
   ```json
   "cdcTables": ["dbo.Orders", "dbo.OrderItems"]
   ```

4. **Configure Trace Limits**: Prevent excessive disk usage
   ```json
   "traceConfig": {
     "maxFileSize": 100,
     "maxFiles": 5
   }
   ```

### Execution Patterns

1. **Long-Running Workflows**: Use async mode
   ```bash
   cdc-cli workflow execute --file long-test.json --async
   ```

2. **Quick Tests**: Use synchronous mode with text output
   ```bash
   cdc-cli workflow execute --file quick-test.json --output text
   ```

3. **CI/CD Integration**: Capture workflow ID and poll for completion
   ```bash
   WORKFLOW_ID=$(cdc-cli workflow execute --file ci-test.json --async --output json | jq -r '.workflowId')
   cdc-cli workflow status $WORKFLOW_ID --watch
   ```

### Monitoring and Troubleshooting

1. **Watch Active Workflows**
   ```bash
   cdc-cli workflow list --status running --output text
   ```

2. **Monitor Workflow Progress**
   ```bash
   cdc-cli workflow status <workflow-id> --watch --interval 10
   ```

3. **Investigate Failures**
   ```bash
   cdc-cli workflow list --status failed --output json-pretty
   cdc-cli workflow status <failed-workflow-id> --output json-pretty
   ```

4. **Track Performance Over Time**
   ```bash
   cdc-cli workflow list --since 2024-01-01 --output json > workflows.json
   ```

### Error Recovery

1. **Workflow Fails Mid-Execution**: Check status for specific error
   ```bash
   cdc-cli workflow status <workflow-id> --output json-pretty
   ```

2. **Database State After Failure**: Manually restore baseline snapshot
   ```bash
   cdc-cli snapshot restore --database TestDB --snapshot baseline
   ```

3. **Orphaned Traces**: Clean up trace sessions
   ```bash
   cdc-cli trace list
   cdc-cli trace delete --session-id <session-id>
   ```

## API Endpoint Mapping

| Command | HTTP Method | Endpoint | Description |
|---------|-------------|----------|-------------|
| `workflow execute` | POST | `/api/testworkflow/execute` | Start workflow execution |
| `workflow status` | GET | `/api/testworkflow/status/{id}` | Get workflow status |
| `workflow list` | GET | `/api/testworkflow/executions` | List workflows |

## Related Commands

- [`cdc`](CDC-COMMANDS.md) - CDC lifecycle management
- [`snapshot`](../README.md) - Snapshot operations
- [`trace`](TRACE-COMMANDS.md) - Trace session management

## Support

For issues or questions:
- Check workflow status with detailed output: `cdc-cli workflow status <id> --output json-pretty`
- Review API logs on the server
- Ensure database permissions for CDC, snapshots, and traces
- Verify connection strings are correct and accessible
