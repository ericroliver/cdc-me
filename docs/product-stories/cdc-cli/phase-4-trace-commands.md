# Phase 4: Trace Commands - User Stories

## Overview

Phase 4 implements the Trace command group for SQL Server Extended Events trace management.

**Prerequisites**: Phase 1 (Foundation) must be complete

---

## Story 4.1: Implement `trace start` Command

**As a** user  
**I want** to start trace sessions via CLI  
**So that** I can capture SQL statements for replay

### API Endpoint

`POST /api/trace/start`

### Acceptance Criteria

- [ ] `TraceStartCommand` class created in `Commands/Trace/TraceStartCommand.cs`
- [ ] Command registered with name `"start"` under `"trace"` group
- [ ] Command description: "Start a trace session to capture SQL statements"
- [ ] Command options:
  ```
  --session, -s <name>       Session name (required)
  --database, -d <name>      Database name (required)
  --max-file-size <mb>       Max file size in MB (optional)
  --max-files <count>        Max number of files (optional)
  --events <list>            Comma-separated events to capture (optional)
  --data <json>              JSON payload as string
  --file, -f <path>          Path to JSON file
  ```
- [ ] Request building from CLI parameters
- [ ] API call to `POST /api/trace/start`
- [ ] Response includes session ID and status
- [ ] Complex configurations via JSON file recommended in help

### Usage Examples

```bash
# Basic trace start
cdc-cli trace start --session "trace-1" --database "TestDB"

# With options
cdc-cli trace start \
  --session "trace-1" \
  --database "TestDB" \
  --max-file-size 100 \
  --max-files 5 \
  --events "sql_statement_completed,rpc_completed"

# Complex configuration via file
cdc-cli trace start --file trace-config.json
```

### Definition of Done

- Command implemented
- All options working
- Tests passing
- Help text complete

---

## Story 4.2: Implement `trace stop` Command

**As a** user  
**I want** to stop trace sessions  
**So that** I can finalize trace data capture

### API Endpoint

`POST /api/trace/stop`

### Acceptance Criteria

- [ ] `TraceStopCommand` class created in `Commands/Trace/TraceStopCommand.cs`
- [ ] Command registered with name `"stop"` under `"trace"` group
- [ ] Command description: "Stop a trace session"
- [ ] Command options:
  ```
  --session, -s <name>       Session name (required)
  --data <json>              JSON payload as string
  --file, -f <path>          Path to JSON file
  ```
- [ ] API call to `POST /api/trace/stop`
- [ ] Response shows session stopped successfully

### Usage Examples

```bash
# Stop trace
cdc-cli trace stop --session "trace-1"

# Using JSON
cdc-cli trace stop --data '{"sessionName":"trace-1"}'
```

### Definition of Done

- Command implemented
- Tests passing
- Help text complete

---

## Story 4.3: Implement `trace status` Command

**As a** user  
**I want** to check trace session status  
**So that** I can monitor active traces

### API Endpoint

`GET /api/trace/status/{sessionName}`

### Acceptance Criteria

- [ ] `TraceStatusCommand` class created in `Commands/Trace/TraceStatusCommand.cs`
- [ ] Command registered with name `"status"` under `"trace"` group
- [ ] Command description: "Get trace session status"
- [ ] Command accepts session name as argument or option:
  ```
  cdc-cli trace status <session-name>
  OR
  cdc-cli trace status --session <name>
  ```
- [ ] API call to `GET /api/trace/status/{sessionName}`
- [ ] Response shows:
  - Session ID
  - Status (Running/Stopped)
  - Start time
  - Event count
  - Configuration

### Usage Examples

```bash
# Get status (positional arg)
cdc-cli trace status trace-1

# Get status (option)
cdc-cli trace status --session trace-1

# JSON output for scripting
cdc-cli trace status trace-1 --output json
```

### Definition of Done

- Command implemented
- Both argument styles supported
- Tests passing
- Help text complete

---

## Story 4.4: Implement `trace list` Command

**As a** user  
**I want** to list all trace sessions  
**So that** I can see what traces exist

### API Endpoint

`GET /api/trace/sessions`

### Acceptance Criteria

- [ ] `TraceListCommand` class created in `Commands/Trace/TraceListCommand.cs`
- [ ] Command registered with name `"list"` under `"trace"` group
- [ ] Command description: "List all trace sessions"
- [ ] No required parameters
- [ ] API call to `GET /api/trace/sessions`
- [ ] Response shows array of sessions
- [ ] Text output shows formatted table
- [ ] Handle empty list gracefully

### Usage Examples

```bash
# List all traces
cdc-cli trace list

# JSON output
cdc-cli trace list --output json

# Pretty formatted
cdc-cli trace list --output json-pretty

# Filter active traces with jq
cdc-cli trace list --output json | jq '.[] | select(.status.state=="Active")'
```

### Definition of Done

- Command implemented
- Multiple output formats
- Empty list handled
- Tests passing

---

## Story 4.5: Implement `trace export` Command

**As a** user  
**I want** to export trace data  
**So that** I can save trace events for analysis

### API Endpoint

`POST /api/trace/export`

### Acceptance Criteria

- [ ] `TraceExportCommand` class created in `Commands/Trace/TraceExportCommand.cs`
- [ ] Command registered with name `"export"` under `"trace"` group
- [ ] Command description: "Export trace data to trace database"
- [ ] Command options:
  ```
  --session, -s <name>       Session name (required)
  ```
- [ ] API call to `POST /api/trace/export`
- [ ] Response shows export path or confirmation

### Usage Examples

```bash
# Export trace data
cdc-cli trace export --session "trace-1"

# Capture export path
EXPORT_PATH=$(cdc-cli trace export --session "trace-1" --output json | jq -r '.exportPath')
```

### Definition of Done

- Command implemented
- Tests passing
- Help text complete

---

## Story 4.6: Implement `trace events` Command

**As a** user  
**I want** to retrieve trace events  
**So that** I can analyze captured SQL statements

### API Endpoint

`GET /api/trace/sessions/{sessionId}/events`

### Acceptance Criteria

- [ ] `TraceEventsCommand` class created in `Commands/Trace/TraceEventsCommand.cs`
- [ ] Command registered with name `"events"` under `"trace"` group
- [ ] Command description: "Get trace events for a session"
- [ ] Command options:
  ```
  <session-id>               Session ID (required, positional)
  --limit <count>            Maximum events to return (default: 100)
  --offset <count>           Events to skip (default: 0)
  ```
- [ ] API call to `GET /api/trace/sessions/{sessionId}/events?limit={limit}&offset={offset}`
- [ ] Response shows array of trace events
- [ ] Support pagination with limit/offset

### Usage Examples

```bash
# Get first 100 events
cdc-cli trace events a1b2c3d4-e5f6-7890-abcd-ef1234567890

# Get specific page
cdc-cli trace events a1b2c3d4-e5f6-7890-abcd-ef1234567890 --limit 50 --offset 100

# Get all events (scripting)
OFFSET=0
LIMIT=100
while true; do
  EVENTS=$(cdc-cli trace events $SESSION_ID --limit $LIMIT --offset $OFFSET --output json)
  COUNT=$(echo $EVENTS | jq 'length')
  if [ $COUNT -eq 0 ]; then break; fi
  echo $EVENTS >> all-events.json
  OFFSET=$((OFFSET + LIMIT))
done
```

### Definition of Done

- Command implemented
- Pagination working
- Tests passing
- Help text with pagination examples

---

## Story 4.7: Implement `trace delete` Command

**As a** user  
**I want** to delete trace sessions  
**So that** I can clean up old traces

### API Endpoint

`DELETE /api/trace/sessions/{sessionId}`

### Acceptance Criteria

- [ ] `TraceDeleteCommand` class created in `Commands/Trace/TraceDeleteCommand.cs`
- [ ] Command registered with name `"delete"` under `"trace"` group
- [ ] Command description: "Delete a trace session and its data"
- [ ] Command options:
  ```
  <session-id>               Session ID (required, positional)
  --force, -f                Skip confirmation
  ```
- [ ] API call to `DELETE /api/trace/sessions/{sessionId}`
- [ ] Optional confirmation prompt
- [ ] Auto-stop if trace is running

### Usage Examples

```bash
# Delete with confirmation
cdc-cli trace delete a1b2c3d4-e5f6-7890-abcd-ef1234567890

# Delete without confirmation
cdc-cli trace delete a1b2c3d4-e5f6-7890-abcd-ef1234567890 --force

# Batch delete old traces
for id in $(cdc-cli trace list --output json | jq -r '.[] | select(.status.state=="Stopped") | .sessionId')
do
  cdc-cli trace delete "$id" --force
done
```

### Definition of Done

- Command implemented
- Confirmation working
- Auto-stop working
- Tests passing

---

## Story 4.8: Create Trace Command Group

**As a** developer  
**I want** all trace commands organized under a group  
**So that** the CLI has a logical structure

### Acceptance Criteria

- [ ] Trace command group created in `Program.cs`
- [ ] Group command: `"trace"`
- [ ] Group description: "SQL trace session management"
- [ ] Subcommands registered:
  - `start`
  - `stop`
  - `status`
  - `list`
  - `export`
  - `events`
  - `delete`
- [ ] Help text comprehensive

### Definition of Done

- Trace command group properly structured
- All subcommands accessible
- Help text clear

---

## Story 4.9: Integration Tests for Trace Commands

**As a** developer  
**I want** integration tests for trace commands  
**So that** I can verify end-to-end functionality

### Acceptance Criteria

- [ ] Integration test class: `TraceCommandsIntegrationTests.cs`
- [ ] Test scenarios:
  - [ ] Start, status, stop workflow
  - [ ] Start, export, delete workflow
  - [ ] List traces
  - [ ] Get events with pagination
  - [ ] Error scenarios
- [ ] Verify all output formats
- [ ] Verify exit codes

### Test Example

```csharp
[Fact]
public async Task TraceWorkflow_StartExportDelete_Success()
{
    var sessionName = $"test-trace-{Guid.NewGuid()}";
    
    // Start
    var startResult = await RunCommand(
        $"trace start --session {sessionName} --database TestDB");
    var sessionId = ExtractSessionId(startResult.Output);
    
    // Status
    var statusResult = await RunCommand($"trace status {sessionName}");
    Assert.Contains("Running", statusResult.Output);
    
    // Stop
    await RunCommand($"trace stop --session {sessionName}");
    
    // Export
    var exportResult = await RunCommand($"trace export --session {sessionName}");
    Assert.Equal(0, exportResult.ExitCode);
    
    // Delete
    var deleteResult = await RunCommand($"trace delete {sessionId} --force");
    Assert.Equal(0, deleteResult.ExitCode);
}
```

### Definition of Done

- Integration tests implemented
- All scenarios covered
- Tests pass consistently

---

## Story 4.10: Documentation for Trace Commands

**As a** user  
**I want** comprehensive documentation for trace commands  
**So that** I can use them effectively

### Acceptance Criteria

- [ ] Trace commands section added to user guide
- [ ] Each command documented
- [ ] Workflows documented:
  - Basic trace workflow
  - Trace and replay pattern
  - Event analysis
- [ ] Pagination examples for events command
- [ ] Best practices for trace management

### Documentation Structure

```markdown
## Trace Commands

### Overview
SQL Server Extended Events traces capture...

### trace start
...

### trace stop
...

### trace status
...

### trace list
...

### trace export
...

### trace events
Pagination examples...

### trace delete
...

### Common Workflows

#### Capture and Replay Pattern
...

#### Event Analysis
...
```

### Definition of Done

- Documentation complete
- Examples tested
- Reviewed

---

## Phase 4 Completion Criteria

**Phase 4 is complete when:**

✅ All seven trace commands implemented  
✅ Commands properly grouped under `trace`  
✅ Pagination working (events command)  
✅ All input methods working  
✅ Output formats working  
✅ Error handling comprehensive  
✅ Unit tests passing (>80% coverage)  
✅ Integration tests passing  
✅ Documentation complete  
✅ Code reviewed  

**Example Complete Workflow:**
```bash
# Start trace
cdc-cli trace start --session "trace-1" --database "TestDB"

# Check status
cdc-cli trace status trace-1

# Run workload...

# Stop trace
cdc-cli trace stop --session "trace-1"

# Export data
cdc-cli trace export --session "trace-1"

# Get session ID for events
SESSION_ID=$(cdc-cli trace list --output json | jq -r '.[] | select(.sessionName=="trace-1") | .sessionId')

# Get events
cdc-cli trace events $SESSION_ID --limit 50

# Clean up
cdc-cli trace delete $SESSION_ID --force
```

**Next Phase**: Phase 5 - Workflow Commands Implementation
