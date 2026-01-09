# CDC CLI API Client - Project Overview

## Summary

Create a new command-line tool **`cdc-cli`** that communicates with the CDC REST API over HTTP, providing a scriptable interface for CI/CD pipelines and automation.

## Key Design Decisions

### 1. New Separate Project (Not Extending cdc-proto)

**Decision**: Create `cdc-cli` as a standalone project alongside `cdc-proto`

**Rationale**:
- **cdc-proto**: Direct database operations (for DBAs, local dev)
- **cdc-cli**: HTTP API operations (for developers, CI/CD, remote ops)
- Different dependencies: SQL drivers vs HTTP client
- Clearer separation of concerns

### 2. Code Sharing Strategy

**Shared via cdc-lib**:
- Business logic utilities
- Common interfaces
- Data models

**Option for API Models** (recommended):
- Create `cdc-models` library for DTOs shared between `cdc-api` and `cdc-cli`
- Alternative: Link model files from `cdc-api/Models`

### 3. Configuration Approach

**API Base URL** (in priority order):
1. `--base-url` command parameter
2. `CDC_API_URL` environment variable  
3. Default: `http://localhost:5000`

### 4. Input/Output Design

**Input Methods** (in priority order):
1. `--data <json>` - Inline JSON string
2. `--file <path>` - JSON from file
3. `stdin` - Piped JSON input
4. CLI parameters - Converted to JSON

**Output**:
- Success: JSON to stdout (for piping/scripting)
- Errors: JSON/text to stderr with appropriate exit codes

## Command Structure

```bash
cdc-cli <resource> <action> [options]
```

### Command Groups

**CDC Operations** (`cdc`):
- `cdc start` - Start CDC monitoring
- `cdc stop` - Stop CDC and capture data
- `cdc capture` - Capture without stopping

**Snapshot Management** (`snapshot`):
- `snapshot create` - Create database snapshot
- `snapshot restore` - Restore from snapshot
- `snapshot list` - List snapshots
- `snapshot info` - Get snapshot details
- `snapshot delete` - Delete snapshot

**Trace Operations** (`trace`):
- `trace start` - Start trace session
- `trace stop` - Stop trace
- `trace status` - Get status
- `trace list` - List sessions
- `trace export` - Export data
- `trace events` - Get trace events
- `trace delete` - Delete session

**Workflow Management** (`workflow`):
- `workflow execute` - Execute test workflow
- `workflow status` - Get workflow status
- `workflow list` - List executions

## Usage Examples

### Basic CDC Workflow
```bash
export CDC_API_URL="http://localhost:5000"

# Start monitoring
cdc-cli cdc start --session "test-1" --include "dbo.Orders"

# Run tests
./run-tests.sh

# Stop and capture
cdc-cli cdc stop --session "test-1" --capture "baseline"
```

### JSON File Input
```bash
# From file
cdc-cli cdc start --file start-request.json

# From stdin
cat request.json | cdc-cli cdc start
```

### Scripting with jq
```bash
# Extract values from response
CAPTURE_ID=$(cdc-cli cdc stop \
  --session "test" \
  --capture "run-1" | jq -r '.captureId')

echo "Captured: $CAPTURE_ID"
```

### CI/CD Integration
```yaml
- name: Start CDC
  run: |
    cdc-cli cdc start \
      --base-url "${{ secrets.CDC_API_URL }}" \
      --session "ci-${{ github.run_id }}" \
      --include "dbo.Orders"

- name: Run Tests
  run: ./test.sh

- name: Capture Results
  run: |
    cdc-cli cdc stop \
      --session "ci-${{ github.run_id }}" \
      --capture "ci-result"
```

## Implementation Phases

### Phase 1: Foundation
- Create `cdc-cli` project
- Setup DI and configuration
- Implement HTTP client service
- Implement JSON I/O handler

### Phase 2: CDC Commands
- Implement `cdc start`, `stop`, `capture`

### Phase 3: Snapshot Commands
- Implement all snapshot operations

### Phase 4: Trace Commands
- Implement all trace operations

### Phase 5: Workflow Commands
- Implement workflow operations

### Phase 6: Testing & Documentation
- Unit tests
- Integration tests
- User documentation
- Example scripts

## Technical Stack

- **.NET 8.0+**
- **System.CommandLine** - CLI framework
- **HttpClient** - HTTP API communication
- **System.Text.Json** - JSON serialization
- **Microsoft.Extensions.*** - DI, logging, configuration

## Key Features

✅ All API endpoints accessible via CLI  
✅ Multiple input methods (CLI params, file, stdin)  
✅ JSON output for scripting  
✅ Configurable base URL  
✅ Comprehensive error handling  
✅ Cross-platform (Windows, Linux, macOS)  
✅ Code sharing with existing cdc-proto

## Questions for Review

1. **Project naming**: Happy with `cdc-cli` or prefer different name?

2. **Shared models**: Create `cdc-models` library or link files from `cdc-api`?

3. **Command aliases**: Should we support short aliases (e.g., `cdc-cli c s` for `cdc-cli cdc start`)?

4. **Authentication**: Priority for adding API key/token support in initial release?

5. **Output formats**: Is JSON + JSON-pretty + text sufficient, or need CSV/table formats?

6. **Any missing endpoints** or commands that should be included?

## Next Steps

Once approved:
1. Create detailed user stories for each phase
2. Set up project structure  
3. Begin Phase 1 implementation

---

**Full Technical Design**: See [`docs/technical/cdc-cli-api-client-design.md`](../technical/cdc-cli-api-client-design.md)
