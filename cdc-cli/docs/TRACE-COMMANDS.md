# Trace Commands

## Overview

The Trace command group provides operations for managing SQL Server Extended Events trace sessions. These commands enable capturing SQL statements for replay and analysis, which is essential for performance testing and validation of database changes.

Extended Events tracing captures SQL execution data including:
- SQL statement text
- Execution parameters
- Timing information
- Connection context
- Resource usage

## Workflow

A typical trace workflow consists of:

1. **Start Trace** - Begin capturing SQL events
2. **Monitor** (optional) - Check trace status and event counts
3. **Export** - Save trace data to trace database
4. **Stop Trace** - End the trace session
5. **Analyze Events** - Review captured statements
6. **Cleanup** - Delete old trace sessions

```bash
# Complete workflow example
cdc-cli trace start --session "perf-test-1" --database "TestDB"
# ... run your test scenario ...
cdc-cli trace export --session "perf-test-1"
cdc-cli trace stop --session "perf-test-1"
# ... analyze results ...
SESSION_ID=$(cdc-cli trace list --output json | jq -r '.[0].sessionId')
cdc-cli trace delete $SESSION_ID --force
```

## Commands

### trace start

Starts a new Extended Events trace session to capture SQL statements.

#### Synopsis

```bash
cdc-cli trace start [options]
```

#### Options

| Option | Alias | Description | Required |
|--------|-------|-------------|----------|
| `--session <name>` | `-s` | Trace session name | Yes* |
| `--database <name>` | `-d` | Database name | Yes* |
| `--max-file-size <mb>` | | Max file size in MB (default: 100) | No |
| `--max-files <count>` | | Max number of files (default: 5) | No |
| `--events <list>` | | Comma-separated events to capture | No |
| `--data <json>` | | Inline JSON request data | No |
| `--file <path>` | `-f` | Path to JSON file with request data | No |

*Required unless using `--data`, `--file`, or stdin

#### Common Event Types

Default events captured: `sql_statement_completed`

Additional events you can specify:
- `rpc_completed` - Stored procedure calls
- `sql_batch_completed` - Batch completions
- `sp_statement_completed` - Statements within stored procedures
- `module_end` - Module (proc/function) completions

#### Input Methods

**CLI Parameters** (recommended for simple cases):
```bash
cdc-cli trace start --session "trace-1" --database "TestDB"
```

**With custom events**:
```bash
cdc-cli trace start \
  --session "trace-1" \
  --database "TestDB" \
  --events "sql_statement_completed,rpc_completed"
```

**JSON File** (recommended for complex configurations):
```bash
cdc-cli trace start --file trace-config.json
```

Where `trace-config.json` contains:
```json
{
  "sessionName": "trace-1",
  "databaseName": "TestDB",
  "maxFileSize": 100,
  "maxFiles": 5,
  "eventsToCapture": [
    "sql_statement_completed",
    "rpc_completed"
  ],
  "filterCriteria": {
    "databaseName": "TestDB"
  }
}
```

#### Response

```json
{
  "success": true,
  "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "sessionName": "trace-1",
  "status": {
    "state": "Active"
  },
  "startedAt": "2024-01-15T10:30:00Z",
  "message": "Trace session started successfully"
}
```

#### Examples

**Start basic trace**:
```bash
cdc-cli trace start --session "perf-test-1" --database "ERP"
```

**Start with custom file limits**:
```bash
cdc-cli trace start \
  --session "long-running-trace" \
  --database "ERP" \
  --max-file-size 200 \
  --max-files 10
```

**Start with multiple event types**:
```bash
cdc-cli trace start \
  --session "comprehensive-trace" \
  --database "ERP" \
  --events "sql_statement_completed,rpc_completed,sp_statement_completed"
```

### trace stop

Stops an active trace session.

#### Synopsis

```bash
cdc-cli trace stop [options]
```

#### Options

| Option | Alias | Description | Required |
|--------|-------|-------------|----------|
| `--session <name>` | `-s` | Session name | Yes* |
| `--data <json>` | | Inline JSON request data | No |
| `--file <path>` | `-f` | Path to JSON file | No |

*Required unless using `--data`, `--file`, or stdin

#### Response

```json
{
  "success": true,
  "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "sessionName": "trace-1",
  "status": {
    "state": "Stopped"
  },
  "stoppedAt": "2024-01-15T11:00:00Z",
  "message": "Trace session stopped successfully"
}
```

#### Examples

**Stop trace**:
```bash
cdc-cli trace stop --session "perf-test-1"
```

**Stop and save response**:
```bash
cdc-cli trace stop --session "perf-test-1" --output json > stop-result.json
```

### trace status

Gets the current status of a trace session.

#### Synopsis

```bash
cdc-cli trace status <session-name>
# OR
cdc-cli trace status --session <session-name>
```

#### Arguments

| Argument | Description | Required |
|----------|-------------|----------|
| `session-name` | Session name (positional) | Yes |

#### Options

| Option | Alias | Description | Required |
|--------|-------|-------------|----------|
| `--session <name>` | `-s` | Session name (alternative) | No |

#### Response

```json
{
  "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "sessionName": "trace-1",
  "databaseName": "TestDB",
  "status": {
    "state": "Active"
  },
  "startTime": "2024-01-15T10:30:00Z",
  "endTime": null,
  "eventCount": 1543,
  "configuration": {
    "maxFileSize": 100,
    "maxFiles": 5,
    "eventsToCapture": ["sql_statement_completed"]
  }
}
```

#### Examples

**Check trace status (positional)**:
```bash
cdc-cli trace status perf-test-1
```

**Check trace status (option)**:
```bash
cdc-cli trace status --session perf-test-1
```

**Monitor trace in a loop**:
```bash
while true; do
  cdc-cli trace status perf-test-1 --output json | jq '.eventCount'
  sleep 5
done
```

### trace list

Lists all trace sessions.

#### Synopsis

```bash
cdc-cli trace list
```

#### Options

None (global output format applies).

#### Response

```json
[
  {
    "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "sessionName": "trace-1",
    "databaseName": "TestDB",
    "status": {
      "state": "Active"
    },
    "startTime": "2024-01-15T10:30:00Z",
    "endTime": null,
    "eventCount": 1543
  },
  {
    "sessionId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
    "sessionName": "trace-2",
    "databaseName": "TestDB",
    "status": {
      "state": "Stopped"
    },
    "startTime": "2024-01-14T09:00:00Z",
    "endTime": "2024-01-14T10:00:00Z",
    "eventCount": 892
  }
]
```

#### Examples

**List all traces**:
```bash
cdc-cli trace list
```

**List with pretty JSON**:
```bash
cdc-cli trace list --output json-pretty
```

**Filter active traces**:
```bash
cdc-cli trace list --output json | jq '.[] | select(.status.state=="Active")'
```

**Get active trace count**:
```bash
cdc-cli trace list --output json | jq '[.[] | select(.status.state=="Active")] | length'
```

### trace export

Exports trace data to the trace database for analysis and replay.

#### Synopsis

```bash
cdc-cli trace export [options]
```

#### Options

| Option | Alias | Description | Required |
|--------|-------|-------------|----------|
| `--session <name>` | `-s` | Session name | Yes |

#### Response

```json
{
  "success": true,
  "sessionName": "trace-1",
  "message": "Trace data exported to /path/to/export",
  "exportedAt": "2024-01-15T11:30:00Z"
}
```

#### Examples

**Export trace data**:
```bash
cdc-cli trace export --session "perf-test-1"
```

**Export and capture path**:
```bash
EXPORT_PATH=$(cdc-cli trace export --session "perf-test-1" --output json | jq -r '.message')
echo "Data exported to: $EXPORT_PATH"
```

### trace events

Retrieves trace events for analysis. Supports pagination for large result sets.

#### Synopsis

```bash
cdc-cli trace events <session-id> [options]
```

#### Arguments

| Argument | Description | Required |
|----------|-------------|----------|
| `session-id` | Session ID (GUID) | Yes |

#### Options

| Option | Description | Required | Default |
|--------|-------------|----------|---------|
| `--limit <count>` | Maximum events to return (1-1000) | No | 100 |
| `--offset <count>` | Number of events to skip | No | 0 |

#### Pagination

The events command supports pagination to handle large trace datasets:

- **limit**: Maximum number of events to return per request (max: 1000)
- **offset**: Number of events to skip (for getting next page)

**Pagination pattern**:
```
Page 1: offset=0,   limit=100  (events 0-99)
Page 2: offset=100, limit=100  (events 100-199)
Page 3: offset=200, limit=100  (events 200-299)
```

#### Response

```json
[
  {
    "eventId": 1,
    "eventName": "sql_statement_completed",
    "timestamp": "2024-01-15T10:31:00Z",
    "statement": "SELECT * FROM Orders WHERE OrderId = @p0",
    "duration": 15,
    "cpuTime": 10,
    "logicalReads": 45,
    "physicalReads": 0
  },
  {
    "eventId": 2,
    "eventName": "rpc_completed",
    "timestamp": "2024-01-15T10:31:05Z",
    "statement": "EXEC sp_GetCustomer @CustomerId = 123",
    "duration": 8,
    "cpuTime": 5,
    "logicalReads": 12,
    "physicalReads": 0
  }
]
```

#### Examples

**Get first 100 events**:
```bash
SESSION_ID="a1b2c3d4-e5f6-7890-abcd-ef1234567890"
cdc-cli trace events $SESSION_ID
```

**Get specific page**:
```bash
cdc-cli trace events $SESSION_ID --offset 100 --limit 50
```

**Get more events per page**:
```bash
cdc-cli trace events $SESSION_ID --limit 500
```

**Iterate through all events** (scripting):
```bash
SESSION_ID="a1b2c3d4-e5f6-7890-abcd-ef1234567890"
OFFSET=0
LIMIT=100

while true; do
  EVENTS=$(cdc-cli trace events $SESSION_ID --limit $LIMIT --offset $OFFSET --output json)
  COUNT=$(echo "$EVENTS" | jq 'length')
  
  if [ "$COUNT" -eq 0 ]; then
    break
  fi
  
  echo "$EVENTS" >> all-events.json
  OFFSET=$((OFFSET + LIMIT))
  echo "Retrieved $COUNT events, offset now $OFFSET"
done
```

**Filter events by type**:
```bash
cdc-cli trace events $SESSION_ID --output json | \
  jq '.[] | select(.eventName=="sql_statement_completed")'
```

**Get slow queries** (duration > 100ms):
```bash
cdc-cli trace events $SESSION_ID --output json | \
  jq '.[] | select(.duration > 100) | {statement, duration}'
```

### trace delete

Deletes a trace session and all its data. This operation is permanent.

#### Synopsis

```bash
cdc-cli trace delete <session-id> [options]
```

#### Arguments

| Argument | Description | Required |
|----------|-------------|----------|
| `session-id` | Session ID (GUID) | Yes |

#### Options

| Option | Alias | Description | Default |
|--------|-------|-------------|---------|
| `--force` | `-f` | Skip confirmation prompt | false |

#### Confirmation

Without `--force`, you'll be prompted to confirm:
```
WARNING: You are about to permanently delete trace session 'a1b2c3d4-...' and all its data.
Are you sure you want to continue? (y/N):
```

#### Response

```json
{
  "success": true,
  "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "sessionName": "trace-1",
  "message": "Trace session deleted successfully",
  "deletedAt": "2024-01-15T12:00:00Z"
}
```

#### Examples

**Delete with confirmation**:
```bash
cdc-cli trace delete a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

**Delete without confirmation** (scripting):
```bash
cdc-cli trace delete a1b2c3d4-e5f6-7890-abcd-ef1234567890 --force
```

**Batch delete stopped traces**:
```bash
for id in $(cdc-cli trace list --output json | \
            jq -r '.[] | select(.status.state=="Stopped") | .sessionId')
do
  echo "Deleting trace $id"
  cdc-cli trace delete "$id" --force
done
```

## Common Workflows

### Performance Testing Workflow

```bash
#!/bin/bash
# Performance test with baseline and optimized comparison

# Baseline test
echo "Running baseline test..."
cdc-cli trace start --session "baseline" --database "TestDB"
./run-baseline-procedure.sh
cdc-cli trace export --session "baseline"
cdc-cli trace stop --session "baseline"

BASELINE_ID=$(cdc-cli trace list --output json | \
              jq -r '.[] | select(.sessionName=="baseline") | .sessionId')

# Optimized test
echo "Running optimized test..."
cdc-cli trace start --session "optimized" --database "TestDB"
./run-optimized-procedure.sh
cdc-cli trace export --session "optimized"
cdc-cli trace stop --session "optimized"

OPTIMIZED_ID=$(cdc-cli trace list --output json | \
               jq -r '.[] | select(.sessionName=="optimized") | .sessionId')

# Analyze and compare
echo "Baseline events:"
cdc-cli trace events $BASELINE_ID --output json | jq 'length'

echo "Optimized events:"
cdc-cli trace events $OPTIMIZED_ID --output json | jq 'length'

# Cleanup
cdc-cli trace delete $BASELINE_ID --force
cdc-cli trace delete $OPTIMIZED_ID --force
```

### Long-Running Trace with Monitoring

```bash
#!/bin/bash
# Start trace and monitor periodically

SESSION="long-running-test"

# Start trace
cdc-cli trace start --session "$SESSION" --database "TestDB" \
  --max-file-size 500 --max-files 20

# Monitor in background
{
  while true; do
    STATUS=$(cdc-cli trace status "$SESSION" --output json)
    COUNT=$(echo "$STATUS" | jq '.eventCount')
    echo "[$(date)] Events captured: $COUNT"
    sleep 60
  done
} &

MONITOR_PID=$!

# Run test (this might take hours)
./run-long-test.sh

# Stop monitoring
kill $MONITOR_PID

# Stop and export
cdc-cli trace export --session "$SESSION"
cdc-cli trace stop --session "$SESSION"
```

### Event Analysis Workflow

```bash
#!/bin/bash
# Analyze trace events for performance issues

SESSION_ID="a1b2c3d4-e5f6-7890-abcd-ef1234567890"

# Get all events
cdc-cli trace events $SESSION_ID --limit 1000 --output json > events.json

# Find slowest queries
echo "Top 10 slowest queries:"
jq -r '.[] | "\(.duration)ms: \(.statement)"' events.json | \
  sort -rn | head -10

# Find most frequent queries
echo -e "\nMost frequent queries:"
jq -r '.[] | .statement' events.json | \
  sort | uniq -c | sort -rn | head -10

# Find queries with high CPU
echo -e "\nQueries with high CPU usage:"
jq '.[] | select(.cpuTime > 50) | {statement, cpuTime, duration}' events.json

# Find queries with physical reads
echo -e "\nQueries causing physical I/O:"
jq '.[] | select(.physicalReads > 0) | {statement, physicalReads}' events.json
```

### CI/CD Integration

```yaml
# .github/workflows/performance-test.yml
name: Performance Test

on: [push, pull_request]

jobs:
  trace-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Start trace
        run: |
          SESSION="ci-run-${{ github.run_id }}"
          cdc-cli trace start --session "$SESSION" --database "TestDB"
          echo "SESSION_NAME=$SESSION" >> $GITHUB_ENV
      
      - name: Run tests
        run: npm test
      
      - name: Export and stop trace
        if: always()
        run: |
          cdc-cli trace export --session "$SESSION_NAME"
          cdc-cli trace stop --session "$SESSION_NAME"
          
          # Get session ID for artifact
          SESSION_ID=$(cdc-cli trace list --output json | \
                       jq -r ".[] | select(.sessionName==\"$SESSION_NAME\") | .sessionId")
          echo "SESSION_ID=$SESSION_ID" >> $GITHUB_ENV
      
      - name: Download trace events
        if: always()
        run: |
          cdc-cli trace events "$SESSION_ID" --limit 1000 --output json > trace-events.json
      
      - name: Upload trace artifacts
        if: always()
        uses: actions/upload-artifact@v3
        with:
          name: trace-data
          path: trace-events.json
      
      - name: Cleanup
        if: always()
        run: cdc-cli trace delete "$SESSION_ID" --force
```

## Output Formats

All commands support three output formats via the global `--output` option:

| Format | Description | Use Case |
|--------|-------------|----------|
| `json` | Compact JSON (default) | Machine processing, piping |
| `json-pretty` | Formatted JSON | Human reading, debugging |
| `text` | Human-readable summary | Console output, logs |

### Examples

```bash
# Compact JSON for scripting
cdc-cli trace list --output json | jq '.[] | .sessionId'

# Pretty JSON for viewing
cdc-cli trace status perf-test-1 --output json-pretty

# Text summary for logs
cdc-cli trace list --output text
```

## Error Handling

### Exit Codes

| Code | Meaning | Examples |
|------|---------|----------|
| 0 | Success | Command executed successfully |
| 1 | API/HTTP error | Network failure, session not found |
| 2 | File I/O error | Cannot write export file |
| 3 | Validation error | Invalid GUID, missing parameters |

### Common Errors

**Invalid session ID**:
```bash
$ cdc-cli trace events invalid-guid
Error: Session ID must be a valid GUID
Exit code: 3
```

**Session not found**:
```bash
$ cdc-cli trace status "nonexistent"
Error: API request failed with status 404: Session not found
Exit code: 1
```

**Pagination limit exceeded**:
```bash
$ cdc-cli trace events $SESSION_ID --limit 2000
Error: Limit must be between 1 and 1000
Exit code: 3
```

## API Endpoint Mappings

| Command | HTTP Method | Endpoint | Request Model | Response Model |
|---------|-------------|----------|---------------|----------------|
| `trace start` | POST | `/api/trace/start` | `StartTraceRequest` | `TraceApiResult` |
| `trace stop` | POST | `/api/trace/stop` | `StopTraceRequest` | `TraceApiResult` |
| `trace status` | GET | `/api/trace/status/{session}` | - | `TraceSessionStatus` |
| `trace list` | GET | `/api/trace/sessions` | - | `TraceSessionSummary[]` |
| `trace export` | POST | `/api/trace/export` | `ExportTraceRequest` | `TraceApiResult` |
| `trace events` | GET | `/api/trace/sessions/{id}/events` | Query: limit, offset | `TraceEvent[]` |
| `trace delete` | DELETE | `/api/trace/sessions/{id}` | - | `TraceApiResult` |

## Troubleshooting

### Trace won't start

**Problem**: Trace start command fails

**Solutions**:
1. Verify SQL Server version supports Extended Events (2012+)
2. Check permissions: User needs ALTER ANY EVENT SESSION
3. Ensure database exists and is accessible
4. Verify API connection: `curl http://localhost:5000/health`

### No events captured

**Problem**: Trace shows 0 events even though queries ran

**Solutions**:
1. Verify trace is actually running: `cdc-cli trace status <session>`
2. Check event filters aren't too restrictive
3. Ensure queries ran after trace was started
4. Verify queries targeted the correct database

### Events command returns empty

**Problem**: `trace events` returns no results

**Solutions**:
1. Check if trace has been exported: `cdc-cli trace status <session>`
2. Verify session ID is correct (GUID format)
3. Try without offset: `--offset 0`
4. Check if trace captured any events: `cdc-cli trace status <session>`

### Export fails

**Problem**: Export command fails or times out

**Solutions**:
1. Check if trace is still running (should be stopped first)
2. Verify disk space on server
3. Check trace database connection
4. Review API logs for detailed error messages

## Best Practices

1. **Use descriptive session names**: Include test purpose and timestamp
   ```bash
   cdc-cli trace start --session "perf-test-orders-$(date +%Y%m%d-%H%M%S)" --database "ERP"
   ```

2. **Always stop traces**: Don't leave traces running indefinitely
   ```bash
   # Use trap to ensure cleanup
   trap 'cdc-cli trace stop --session "$SESSION"' EXIT
   ```

3. **Export before analyzing**: Always export trace data to the database
   ```bash
   cdc-cli trace export --session "my-trace"
   cdc-cli trace stop --session "my-trace"
   ```

4. **Use pagination for large traces**: Don't try to get all events at once
   ```bash
   # Good: paginate through results
   cdc-cli trace events $ID --limit 100 --offset 0
   cdc-cli trace events $ID --limit 100 --offset 100
   
   # Avoid: trying to get too many at once
   cdc-cli trace events $ID --limit 10000  # Will fail or timeout
   ```

5. **Clean up old traces**: Delete traces after analysis
   ```bash
   # Cleanup script
   cdc-cli trace list --output json | \
     jq -r '.[] | select(.status.state=="Stopped") | .sessionId' | \
     xargs -I {} cdc-cli trace delete {} --force
   ```

6. **Monitor long-running traces**: Check status periodically
   ```bash
   watch -n 30 'cdc-cli trace status my-trace --output json | jq ".eventCount"'
   ```

7. **Specify relevant events**: Don't capture everything
   ```bash
   # Good: specific events
   cdc-cli trace start --session "test" --database "DB" \
     --events "sql_statement_completed"
   
   # Avoid: all events (huge overhead)
   cdc-cli trace start --session "test" --database "DB" \
     --events "sql_statement_completed,rpc_completed,sql_batch_completed,..."
   ```

8. **Handle errors in scripts**: Always check exit codes
   ```bash
   if ! cdc-cli trace start --session "test" --database "DB"; then
     echo "Failed to start trace" >&2
     exit 1
   fi
   ```

## See Also

- [Trace API Documentation](../../docs/web-api.md#trace-endpoints)
- [Extended Events Overview](https://learn.microsoft.com/sql/relational-databases/extended-events/)
- [CDC Testing Workflow](../../docs/trace-testing-workflow-examples.md)
- [CLI Configuration](../README.md)
