# CDC Commands

## Overview

The CDC (Change Data Capture) command group provides operations for starting, stopping, and capturing database changes. These commands interface with the CDC API to enable, disable, and collect change data from SQL Server databases.

## Workflow

A typical CDC testing workflow consists of three stages:

1. **Start CDC** - Enable CDC monitoring on specified tables
2. **Capture** (optional, repeatable) - Take intermediate snapshots of changes
3. **Stop CDC** - Disable CDC and capture final changes

```bash
# Complete workflow example
cdc-cli cdc start --session "performance-test" --include "dbo.Orders" --include "dbo.Customers"
# ... run your test scenario ...
cdc-cli cdc capture --session "performance-test" --capture "checkpoint-1"
# ... continue testing ...
cdc-cli cdc stop --session "performance-test" --capture "final" --type "Baseline"
```

## Commands

### cdc start

Starts CDC operations on database tables.

#### Synopsis

```bash
cdc-cli cdc start [options]
```

#### Options

| Option | Alias | Description | Required |
|--------|-------|-------------|----------|
| `--session <name>` | `-s` | Name for the CDC session | Yes* |
| `--include <table>` | `-i` | Tables to include (repeatable) | No |
| `--exclude <table>` | `-e` | Tables to exclude (repeatable) | No |
| `--data <json>` | `-d` | Inline JSON request data | No |
| `--file <path>` | `-f` | Path to JSON file with request data | No |

*Required unless using `--data`, `--file`, or stdin

#### Input Methods

**CLI Parameters** (recommended for simple cases):
```bash
cdc-cli cdc start --session "test-1" --include "dbo.Orders" --include "dbo.Customers"
```

**JSON File**:
```bash
cdc-cli cdc start --file start-request.json
```

Where `start-request.json` contains:
```json
{
  "sessionName": "test-1",
  "tablesToInclude": ["dbo.Orders", "dbo.Customers"]
}
```

**Inline JSON**:
```bash
cdc-cli cdc start --data '{"sessionName":"test-1","tablesToInclude":["dbo.Orders"]}'
```

**Stdin**:
```bash
echo '{"sessionName":"test-1","tablesToInclude":["dbo.Orders"]}' | cdc-cli cdc start
```

#### Response

```json
{
  "success": true,
  "sessionName": "test-1",
  "message": "CDC started successfully",
  "tablesEnabled": ["dbo.Orders", "dbo.Customers"],
  "tablesSkipped": [],
  "errors": []
}
```

#### Examples

**Start CDC on specific tables**:
```bash
cdc-cli cdc start --session "perf-test" --include "dbo.Orders" --include "dbo.LineItems"
```

**Start CDC with table exclusions**:
```bash
cdc-cli cdc start --session "test" --exclude "dbo.AuditLog" --exclude "dbo.TempData"
```

**Start with JSON output**:
```bash
cdc-cli cdc start --session "test" --include "dbo.Orders" --output json-pretty
```

### cdc capture

Captures CDC data without stopping CDC operations. Useful for taking intermediate snapshots during long-running tests.

#### Synopsis

```bash
cdc-cli cdc capture [options]
```

#### Options

| Option | Alias | Description | Required |
|--------|-------|-------------|----------|
| `--session <name>` | `-s` | Name of the CDC session | Yes* |
| `--capture <name>` | `-c` | Name for this capture | Yes* |
| `--type <type>` | `-t` | Capture type (default: "Intermediate") | No |
| `--data <json>` | `-d` | Inline JSON request data | No |
| `--file <path>` | `-f` | Path to JSON file with request data | No |

*Required unless using `--data`, `--file`, or stdin

#### Input Methods

Same as `cdc start` command.

#### Response

```json
{
  "success": true,
  "sessionName": "test-1",
  "captureName": "checkpoint-1",
  "captureType": "Intermediate",
  "message": "CDC data captured successfully",
  "tablesWithChanges": ["dbo.Orders"],
  "totalRecords": 150,
  "captureId": "capture-abc123",
  "errors": []
}
```

#### Examples

**Take an intermediate capture**:
```bash
cdc-cli cdc capture --session "long-test" --capture "phase-1"
```

**Multiple captures during a test**:
```bash
# Start CDC
cdc-cli cdc start --session "multi-phase" --include "dbo.Orders"

# Phase 1
echo "Running phase 1..."
cdc-cli cdc capture --session "multi-phase" --capture "phase-1" --type "Intermediate"

# Phase 2
echo "Running phase 2..."
cdc-cli cdc capture --session "multi-phase" --capture "phase-2" --type "Intermediate"

# Final capture and stop
cdc-cli cdc stop --session "multi-phase" --capture "final"
```

**Capture with JSON file**:
```bash
cdc-cli cdc capture --file capture-request.json --output json > capture-output.json
```

### cdc stop

Stops CDC operations and captures final changes.

#### Synopsis

```bash
cdc-cli cdc stop [options]
```

#### Options

| Option | Alias | Description | Required |
|--------|-------|-------------|----------|
| `--session <name>` | `-s` | Name of the CDC session | Yes* |
| `--capture <name>` | `-c` | Name for this capture | Yes* |
| `--type <type>` | `-t` | Capture type (default: "Baseline") | No |
| `--data <json>` | `-d` | Inline JSON request data | No |
| `--file <path>` | `-f` | Path to JSON file with request data | No |

*Required unless using `--data`, `--file`, or stdin

#### Input Methods

Same as `cdc start` command.

#### Response

```json
{
  "success": true,
  "sessionName": "test-1",
  "captureName": "final",
  "message": "CDC stopped successfully",
  "tablesWithChanges": ["dbo.Orders", "dbo.Customers"],
  "totalRecords": 500,
  "captureId": "capture-xyz789",
  "errors": []
}
```

#### Examples

**Stop CDC with default capture type**:
```bash
cdc-cli cdc stop --session "test-1" --capture "baseline"
```

**Stop CDC with custom capture type**:
```bash
cdc-cli cdc stop --session "test-1" --capture "optimized" --type "Optimized"
```

**Stop and save results for comparison**:
```bash
cdc-cli cdc stop --session "test-1" --capture "run-1" --output json > baseline.json
```

## Common Workflows

### Basic Test Workflow

```bash
# 1. Start CDC monitoring
cdc-cli cdc start --session "basic-test" --include "dbo.Orders"

# 2. Run your test scenario
./run-test-scenario.sh

# 3. Stop CDC and capture results
cdc-cli cdc stop --session "basic-test" --capture "results"
```

### Multi-Phase Test with Checkpoints

```bash
# Start CDC
cdc-cli cdc start --session "phases" --include "dbo.Orders" --include "dbo.Customers"

# Phase 1: Initial load
echo "Phase 1: Initial data load..."
./load-initial-data.sh
cdc-cli cdc capture --session "phases" --capture "after-load" --type "Checkpoint"

# Phase 2: Updates
echo "Phase 2: Update operations..."
./update-data.sh
cdc-cli cdc capture --session "phases" --capture "after-updates" --type "Checkpoint"

# Phase 3: Deletes
echo "Phase 3: Delete operations..."
./delete-data.sh

# Final capture and stop
cdc-cli cdc stop --session "phases" --capture "final" --type "Baseline"
```

### Baseline vs Optimized Comparison

```bash
# Capture baseline
cdc-cli cdc start --session "baseline" --include "dbo.Orders"
./run-original-procedure.sh
cdc-cli cdc stop --session "baseline" --capture "original" --type "Baseline" \
  --output json > baseline.json

# Capture optimized
cdc-cli cdc start --session "optimized" --include "dbo.Orders"
./run-optimized-procedure.sh
cdc-cli cdc stop --session "optimized" --capture "improved" --type "Optimized" \
  --output json > optimized.json

# Compare (using separate comparison tool)
cdc-cli cdc compare --baseline baseline.json --test optimized.json
```

### CI/CD Integration

```yaml
# .github/workflows/test.yml
- name: Run CDC Test
  run: |
    # Start CDC
    cdc-cli cdc start --session "${{ github.run_id }}" \
      --include "dbo.Orders" \
      --include "dbo.Customers"
    
    # Run tests
    npm test
    
    # Capture and stop
    cdc-cli cdc stop --session "${{ github.run_id }}" \
      --capture "ci-run" \
      --output json > results.json
    
    # Upload artifact
    - uses: actions/upload-artifact@v3
      with:
        name: cdc-results
        path: results.json
```

## Output Formats

All commands support three output formats via the global `--output` option:

| Format | Description | Use Case |
|--------|-------------|----------|
| `json` | Compact JSON (default) | Machine processing, piping to other tools |
| `json-pretty` | Formatted JSON with indentation | Human reading, debugging |
| `text` | Human-readable summary | Console output, logs |

### Examples

```bash
# Compact JSON for piping
cdc-cli cdc stop --session "test" --capture "result" --output json | jq '.captureId'

# Pretty JSON for viewing
cdc-cli cdc stop --session "test" --capture "result" --output json-pretty

# Text summary for logs
cdc-cli cdc stop --session "test" --capture "result" --output text
```

## Error Handling

### Exit Codes

| Code | Meaning | Examples |
|------|---------|----------|
| 0 | Success | Command executed successfully |
| 1 | API/HTTP error | Network failure, API returned error |
| 2 | File I/O error | File not found, permission denied |
| 3 | Validation error | Missing required parameters, invalid JSON |

### Common Errors

**Missing session name**:
```bash
$ cdc-cli cdc start --include "dbo.Orders"
Error: Session name is required. Use --session or provide JSON input with sessionName.
Exit code: 3
```

**API connection failure**:
```bash
$ cdc-cli cdc start --session "test" --include "dbo.Orders"
Error: API request failed: Connection refused
Exit code: 1
```

**Invalid JSON**:
```bash
$ cdc-cli cdc start --data '{"sessionName":"test"'
Error: Invalid JSON: Unexpected end of JSON input
Exit code: 3
```

### Verbose Mode

Use the `--verbose` flag for detailed error information:

```bash
cdc-cli cdc start --session "test" --include "dbo.Orders" --verbose
```

## Environment Variables

### CDC_API_URL

Set the base URL for the CDC API (default: `http://localhost:5000`):

```bash
export CDC_API_URL="https://cdc-api.example.com"
cdc-cli cdc start --session "test" --include "dbo.Orders"
```

Override via command line:

```bash
cdc-cli cdc start --base-url "https://cdc-api.example.com" \
  --session "test" --include "dbo.Orders"
```

## API Endpoint Mappings

| Command | HTTP Method | Endpoint | Request Model | Response Model |
|---------|-------------|----------|---------------|----------------|
| `cdc start` | POST | `/api/cdc/start` | `StartCdcRequest` | `StartCdcResponse` |
| `cdc capture` | POST | `/api/cdc/capture` | `CaptureCdcRequest` | `CaptureCdcResponse` |
| `cdc stop` | POST | `/api/cdc/stop` | `StopCdcRequest` | `StopCdcResponse` |

## Troubleshooting

### CDC won't start

**Problem**: CDC start command fails immediately

**Solutions**:
1. Verify the database has CDC enabled: `EXEC sp_changedbowner 'sa'`
2. Ensure SQL Server Agent is running
3. Check that tables exist and you have permissions
4. Verify API is accessible: `curl http://localhost:5000/health`

### Capture returns no data

**Problem**: Capture shows 0 records even though data changed

**Solutions**:
1. Check that CDC is actually running: `SELECT * FROM sys.dm_cdc_log_scan_sessions`
2. Verify changes were made after CDC was enabled
3. Ensure SQL Server Agent jobs are running
4. Wait a few seconds for CDC to process changes

### Session not found

**Problem**: Capture or stop fails with "session not found"

**Solutions**:
1. Verify the session name exactly matches what you used in `start`
2. Check if session was already stopped
3. Ensure you're connecting to the same API instance

### Performance issues

**Problem**: CDC operations are slow

**Solutions**:
1. Limit tables with `--include` rather than monitoring all tables
2. Use `--exclude` to skip large audit/log tables
3. Consider taking fewer intermediate captures
4. Increase CDC scan interval in SQL Server

## Best Practices

1. **Use descriptive session names**: Include test name, timestamp, or run ID
   ```bash
   cdc-cli cdc start --session "test-orders-$(date +%Y%m%d-%H%M%S)"
   ```

2. **Always specify tables**: Don't rely on defaults, explicitly include tables
   ```bash
   # Good
   cdc-cli cdc start --session "test" --include "dbo.Orders"
   
   # Avoid
   cdc-cli cdc start --session "test"  # Monitors ALL tables
   ```

3. **Clean up sessions**: Always stop CDC when done to avoid orphaned sessions

4. **Use typed captures**: Specify capture type for clarity
   ```bash
   cdc-cli cdc capture --session "test" --capture "checkpoint-1" --type "Checkpoint"
   cdc-cli cdc stop --session "test" --capture "final" --type "Baseline"
   ```

5. **Capture output for analysis**: Save results to files for later comparison
   ```bash
   cdc-cli cdc stop --session "test" --capture "run" --output json > result.json
   ```

6. **Handle errors in scripts**: Check exit codes
   ```bash
   if ! cdc-cli cdc start --session "test" --include "dbo.Orders"; then
       echo "Failed to start CDC" >&2
       exit 1
   fi
   ```

## See Also

- [CDC API Documentation](../../docs/web-api.md)
- [CDC Testing Workflow Guide](../../docs/cdc-implementation-plan.md)
- [CLI Configuration](../README.md)
