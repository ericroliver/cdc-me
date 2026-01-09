# Phase 2: CDC Commands - User Stories

## Overview

Phase 2 implements the CDC command group (`cdc start`, `cdc stop`, `cdc capture`) which are the core commands for Change Data Capture operations.

**Prerequisites**: Phase 1 (Foundation) must be complete

---

## Story 2.1: Implement `cdc start` Command

**As a** user  
**I want** to start CDC monitoring on database tables via CLI  
**So that** I can capture database changes from the command line

### API Endpoint

`POST /api/cdc/start`

### Acceptance Criteria

- [ ] `CdcStartCommand` class created in `Commands/Cdc/CdcStartCommand.cs`
- [ ] Command inherits from [`ApiCommandBase`](ApiCommandBase.cs)
- [ ] Command registered with name `"start"` under `"cdc"` group
- [ ] Command description: "Start CDC operations on database tables"
- [ ] Command options implemented:
  ```
  --session, -s <name>       Session name (required if not using --data/--file)
  --include, -i <table>      Tables to include (repeatable)
  --exclude, -e <table>      Tables to exclude (repeatable)
  --data, -d <json>          JSON payload as string
  --file, -f <path>          Path to JSON file
  ```
- [ ] Input handling priority:
  1. `--data` (inline JSON)
  2. `--file` (JSON from file)
  3. stdin (if no --data/--file)
  4. CLI parameters (--session, --include, --exclude)
- [ ] Request building from CLI parameters:
  ```csharp
  new StartCdcRequest
  {
      SessionName = session,
      TablesToInclude = include?.ToList(),
      TablesToExclude = exclude?.ToList()
  }
  ```
- [ ] API call to `POST /api/cdc/start`
- [ ] Response output to stdout (respects `--output` format)
- [ ] Error handling for:
  - Missing session name
  - Invalid JSON input
  - API errors (4xx, 5xx)
  - Network errors
- [ ] Help text comprehensive with examples

### Usage Examples

```bash
# Using CLI parameters
cdc-cli cdc start --session "test-1" --include "dbo.Orders" --include "dbo.Customers"

# Using JSON file
cdc-cli cdc start --file start-request.json

# Using inline JSON
cdc-cli cdc start --data '{"sessionName":"test-1","tablesToInclude":["dbo.Orders"]}'

# Using stdin
echo '{"sessionName":"test-1","tablesToInclude":["dbo.Orders"]}' | cdc-cli cdc start

# With exclude filter
cdc-cli cdc start --session "test-1" --exclude "dbo.AuditLog" --exclude "dbo.TempData"
```

### Test Cases

- [ ] Test with valid session and include tables
- [ ] Test with exclude tables
- [ ] Test with both include and exclude
- [ ] Test with JSON file input
- [ ] Test with stdin input
- [ ] Test with inline JSON
- [ ] Test missing required fields
- [ ] Test invalid JSON format
- [ ] Test API error responses
- [ ] Test output formats (json, json-pretty, text)

### Definition of Done

- Command implemented and working
- All test cases pass
- Help text complete
- Code reviewed
- Documentation updated

---

## Story 2.2: Implement `cdc stop` Command

**As a** user  
**I want** to stop CDC monitoring and capture data  
**So that** I can save the captured changes and disable CDC

### API Endpoint

`POST /api/cdc/stop`

### Acceptance Criteria

- [ ] `CdcStopCommand` class created in `Commands/Cdc/CdcStopCommand.cs`
- [ ] Command inherits from [`ApiCommandBase`](ApiCommandBase.cs)
- [ ] Command registered with name `"stop"` under `"cdc"` group
- [ ] Command description: "Stop CDC operations and capture data"
- [ ] Command options implemented:
  ```
  --session, -s <name>       Session name (required)
  --capture, -c <name>       Capture name (required)
  --type, -t <type>          Capture type (optional, default: "Baseline")
  --data, -d <json>          JSON payload as string
  --file, -f <path>          Path to JSON file
  ```
- [ ] Input handling priority same as start command
- [ ] Request building from CLI parameters:
  ```csharp
  new StopCdcRequest
  {
      SessionName = session,
      CaptureName = captureName,
      CaptureType = captureType ?? "Baseline"
  }
  ```
- [ ] API call to `POST /api/cdc/stop`
- [ ] Response includes:
  - Success status
  - Capture ID
  - Tables with changes
  - Total records captured
- [ ] Response output to stdout
- [ ] Error handling comprehensive
- [ ] Validation:
  - Session name required
  - Capture name required

### Usage Examples

```bash
# Using CLI parameters
cdc-cli cdc stop --session "test-1" --capture "baseline" --type "Baseline"

# Minimal (type defaults to "Baseline")
cdc-cli cdc stop --session "test-1" --capture "baseline"

# Using JSON file
cdc-cli cdc stop --file stop-request.json

# Using stdin
echo '{"sessionName":"test-1","captureName":"baseline"}' | cdc-cli cdc stop

# Capture result to variable (for scripting)
CAPTURE_ID=$(cdc-cli cdc stop --session "test-1" --capture "run-1" --output json | jq -r '.captureId')
```

### Test Cases

- [ ] Test with all parameters
- [ ] Test with minimal parameters (defaults)
- [ ] Test with JSON file input
- [ ] Test with stdin input
- [ ] Test missing session name
- [ ] Test missing capture name
- [ ] Test API error responses
- [ ] Test successful capture with data
- [ ] Test capture with no changes
- [ ] Test output formats

### Definition of Done

- Command implemented and working
- All test cases pass
- Help text complete
- Code reviewed
- Works with scripting (exit codes correct)

---

## Story 2.3: Implement `cdc capture` Command

**As a** user  
**I want** to capture CDC data without stopping  
**So that** I can take intermediate snapshots during a test

### API Endpoint

`POST /api/cdc/capture`

### Acceptance Criteria

- [ ] `CdcCaptureCommand` class created in `Commands/Cdc/CdcCaptureCommand.cs`
- [ ] Command inherits from [`ApiCommandBase`](ApiCommandBase.cs)
- [ ] Command registered with name `"capture"` under `"cdc"` group
- [ ] Command description: "Capture CDC data without stopping CDC"
- [ ] Command options (same as stop):
  ```
  --session, -s <name>       Session name (required)
  --capture, -c <name>       Capture name (required)
  --type, -t <type>          Capture type (optional, default: "Intermediate")
  ```
- [ ] Request building similar to stop command:
  ```csharp
  new CaptureCdcRequest
  {
      SessionName = session,
      CaptureName = captureName,
      CaptureType = captureType ?? "Intermediate"
  }
  ```
- [ ] API call to `POST /api/cdc/capture`
- [ ] Response handling same as stop command
- [ ] Note in help: "CDC remains active after capture"
- [ ] Error handling comprehensive

### Usage Examples

```bash
# Take intermediate capture
cdc-cli cdc capture --session "test-1" --capture "checkpoint-1"

# Multiple captures during test
cdc-cli cdc start --session "long-test"
# Run phase 1...
cdc-cli cdc capture --session "long-test" --capture "phase-1" --type "Intermediate"
# Run phase 2...
cdc-cli cdc capture --session "long-test" --capture "phase-2" --type "Intermediate"
# Run phase 3...
cdc-cli cdc stop --session "long-test" --capture "final" --type "Baseline"

# Using JSON
cdc-cli cdc capture --file capture-request.json
```

### Test Cases

- [ ] Test basic capture
- [ ] Test with custom capture type
- [ ] Test multiple sequential captures
- [ ] Test with JSON input
- [ ] Test error scenarios
- [ ] Test that CDC remains active (verify with status call if available)

### Definition of Done

- Command implemented and working
- All test cases pass
- Help text clearly indicates CDC stays active
- Code reviewed
- Integration test with multiple captures

---

## Story 2.4: Create CDC Command Group

**As a** developer  
**I want** all CDC commands organized under a group  
**So that** the CLI has a logical structure

### Acceptance Criteria

- [ ] CDC command group created in `Program.cs`
- [ ] Group command: `"cdc"`
- [ ] Group description: "Change Data Capture operations"
- [ ] Subcommands registered:
  - `start` - [`CdcStartCommand`](CdcStartCommand.cs)
  - `stop` - [`CdcStopCommand`](CdcStopCommand.cs)
  - `capture` - [`CdcCaptureCommand`](CdcCaptureCommand.cs)
- [ ] Help text for group:
  ```bash
  cdc-cli cdc --help
  ```
  Shows all subcommands with descriptions
- [ ] Global options inherited by all subcommands
- [ ] Command structure: `cdc-cli cdc <subcommand> [options]`

### Test Cases

```bash
# Test group help
cdc-cli cdc --help

# Test each subcommand
cdc-cli cdc start --help
cdc-cli cdc stop --help
cdc-cli cdc capture --help

# Test command execution
cdc-cli cdc start --session "test"
```

### Definition of Done

- CDC command group properly structured
- All subcommands accessible
- Help text clear and comprehensive
- Commands work independently

---

## Story 2.5: Integration Tests for CDC Commands

**As a** developer  
**I want** integration tests for CDC commands  
**So that** I can verify end-to-end functionality

### Acceptance Criteria

- [ ] Integration test class created: `CdcCommandsIntegrationTests.cs`
- [ ] Test setup:
  - Mock API server or use real API (configurable)
  - Test data fixtures
  - Cleanup after tests
- [ ] Test scenarios:
  - [ ] Complete workflow: start → capture → stop
  - [ ] Start with include tables
  - [ ] Start with exclude tables
  - [ ] Stop with different capture types
  - [ ] Multiple intermediate captures
  - [ ] Error scenarios (API errors, network errors)
- [ ] Test output verification:
  - JSON format correct
  - Exit codes correct
  - Stderr for errors, stdout for success
- [ ] Test with different input methods:
  - CLI parameters
  - JSON file
  - Stdin

### Test Example

```csharp
[Fact]
public async Task CompleteCdcWorkflow_Success()
{
    // Arrange
    var sessionName = $"test-{Guid.NewGuid()}";
    
    // Act & Assert - Start CDC
    var startResult = await RunCommand(
        $"cdc start --session {sessionName} --include dbo.Orders");
    Assert.Equal(0, startResult.ExitCode);
    
    // Act & Assert - Capture
    var captureResult = await RunCommand(
        $"cdc capture --session {sessionName} --capture checkpoint-1");
    Assert.Equal(0, captureResult.ExitCode);
    
    // Act & Assert - Stop
    var stopResult = await RunCommand(
        $"cdc stop --session {sessionName} --capture final");
    Assert.Equal(0, stopResult.ExitCode);
}
```

### Definition of Done

- Integration tests implemented
- All scenarios covered
- Tests pass consistently
- Test documentation complete

---

## Story 2.6: Documentation for CDC Commands

**As a** user  
**I want** comprehensive documentation for CDC commands  
**So that** I can use them effectively

### Acceptance Criteria

- [ ] User guide created: `docs/cdc-cli-user-guide.md`
- [ ] CDC commands section includes:
  - Overview of CDC operations
  - Command reference for each command
  - Parameter descriptions
  - Usage examples (basic and advanced)
  - Common workflows
  - Troubleshooting tips
- [ ] Examples cover:
  - Basic CDC workflow
  - Table filtering (include/exclude)
  - Multiple captures
  - JSON input methods
  - Scripting integration
  - CI/CD usage
- [ ] Error messages documented:
  - Common errors
  - Solutions
  - Exit codes
- [ ] README.md updated with CDC command examples

### Documentation Structure

```markdown
# CDC Commands

## Overview
...

## cdc start
### Description
### Parameters
### Examples
### Common Issues

## cdc stop
### Description
### Parameters
### Examples
### Common Issues

## cdc capture
### Description
### Parameters
### Examples
### Common Issues

## Workflows
### Basic Test Workflow
### Advanced Testing with Multiple Captures
### CI/CD Integration
```

### Definition of Done

- Documentation complete and accurate
- Examples tested and working
- Screenshots or output examples included
- Reviewed by team member

---

## Phase 2 Completion Criteria

**Phase 2 is complete when:**

✅ All three CDC commands implemented (start, stop, capture)  
✅ Commands properly grouped under `cdc`  
✅ All input methods working (CLI params, file, stdin)  
✅ Output formats working (json, json-pretty, text)  
✅ Error handling comprehensive  
✅ Unit tests passing (>80% coverage)  
✅ Integration tests passing  
✅ Documentation complete  
✅ Code reviewed  
✅ No build warnings  

**Example Complete Workflow:**
```bash
# This workflow should execute successfully
cdc-cli cdc start --session "test" --include "dbo.Orders"
# (run test scenario)
cdc-cli cdc capture --session "test" --capture "mid-test"
# (run more tests)
cdc-cli cdc stop --session "test" --capture "final"
```

**Next Phase**: Phase 3 - Snapshot Commands Implementation
