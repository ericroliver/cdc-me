# Phase 5: Workflow Commands - User Stories

## Overview

Phase 5 implements the Workflow command group for executing complete test workflows that orchestrate multiple operations.

**Prerequisites**: Phases 1-4 must be complete (Foundation, CDC, Snapshot, Trace)

---

## Story 5.1: Implement `workflow execute` Command

**As a** user  
**I want** to execute complete test workflows  
**So that** I can run complex multi-step testing scenarios

### API Endpoint

`POST /api/testworkflow/execute`

### Acceptance Criteria

- [ ] `WorkflowExecuteCommand` class created in `Commands/Workflow/WorkflowExecuteCommand.cs`
- [ ] Command registered with name `"execute"` under `"workflow"` group
- [ ] Command description: "Execute a complete test workflow"
- [ ] Command options:
  ```
  --file, -f <path>          Path to workflow JSON file (required)
  --data <json>              JSON payload as string
  ```
- [ ] Given complexity, file/stdin input strongly recommended
- [ ] Help text includes example workflow JSON structure
- [ ] API call to `POST /api/testworkflow/execute`
- [ ] Response shows:
  - Workflow ID
  - Execution status
  - Steps completed
  - Results
- [ ] Long-running operation handling (may take minutes)
- [ ] Progress indication if API supports it

### Workflow JSON Example

```json
{
  "workflowName": "OrderProcessingTest",
  "databaseName": "TestDB",
  "connectionString": "Server=...",
  "traceConnectionString": "Server=...",
  "baselineSnapshotName": "baseline",
  "testSnapshotName": "after-test",
  "traceSessionName": "order-test",
  "enableCdc": true,
  "baselineWorkloadPath": "/path/to/workload.sql",
  "cdcTables": ["dbo.Orders", "dbo.OrderItems"],
  "traceConfig": {
    "maxFileSize": 100,
    "maxFiles": 5
  }
}
```

### Usage Examples

```bash
# Execute workflow from file
cdc-cli workflow execute --file test-workflow.json

# Using stdin
cat workflow-config.json | cdc-cli workflow execute

# Capture workflow ID for status checks
WORKFLOW_ID=$(cdc-cli workflow execute --file test.json --output json | jq -r '.workflowId')
echo "Workflow ID: $WORKFLOW_ID"
```

### Technical Notes

- Workflow execution can be long-running (minutes to hours)
- Consider timeout configuration
- May want to add `--async` flag for background execution (future)

### Definition of Done

- Command implemented
- Complex JSON input handled
- Long-running execution supported
- Tests passing
- Help text includes example JSON

---

## Story 5.2: Implement `workflow status` Command

**As a** user  
**I want** to check workflow execution status  
**So that** I can monitor long-running workflows

### API Endpoint

`GET /api/testworkflow/status/{workflowId}`

### Acceptance Criteria

- [ ] `WorkflowStatusCommand` class created in `Commands/Workflow/WorkflowStatusCommand.cs`
- [ ] Command registered with name `"status"` under `"workflow"` group
- [ ] Command description: "Get workflow execution status"
- [ ] Command accepts workflow ID:
  ```
  cdc-cli workflow status <workflow-id>
  OR
  cdc-cli workflow status --workflow <id>
  ```
- [ ] API call to `GET /api/testworkflow/status/{workflowId}`
- [ ] Response shows:
  - Workflow ID
  - Current status
  - Completed steps
  - Current step
  - Progress percentage (if available)
  - Errors

### Usage Examples

```bash
# Get workflow status (positional)
cdc-cli workflow status a1b2c3d4-e5f6-7890-abcd-ef1234567890

# Get workflow status (option)
cdc-cli workflow status --workflow a1b2c3d4-e5f6-7890-abcd-ef1234567890

# Poll for completion (script)
WORKFLOW_ID="a1b2c3d4-e5f6-7890-abcd-ef1234567890"
while true; do
  STATUS=$(cdc-cli workflow status $WORKFLOW_ID --output json | jq -r '.status')
  echo "Status: $STATUS"
  if [ "$STATUS" == "Completed" ] || [ "$STATUS" == "Failed" ]; then
    break
  fi
  sleep 5
done
```

### Definition of Done

- Command implemented
- Both argument styles supported
- Tests passing
- Help text with polling example

---

## Story 5.3: Implement `workflow list` Command

**As a** user  
**I want** to list workflow executions  
**So that** I can see recent workflow runs

### API Endpoint

`GET /api/testworkflow/executions`

### Acceptance Criteria

- [ ] `WorkflowListCommand` class created in `Commands/Workflow/WorkflowListCommand.cs`
- [ ] Command registered with name `"list"` under `"workflow"` group
- [ ] Command description: "List workflow executions"
- [ ] No required parameters
- [ ] Optional filters (if API supports):
  - `--status <status>`: Filter by status
  - `--limit <count>`: Limit results
- [ ] API call to `GET /api/testworkflow/executions`
- [ ] Response shows array of workflow summaries
- [ ] Text output formatted as table
- [ ] Handle empty list gracefully

### Usage Examples

```bash
# List all workflows
cdc-cli workflow list

# JSON output for scripting
cdc-cli workflow list --output json

# Filter and format with jq
cdc-cli workflow list --output json | \
  jq '.[] | select(.success==true) | .workflowId'
```

### Definition of Done

- Command implemented
- Multiple output formats
- Empty list handled
- Tests passing

---

## Story 5.4: Create Workflow Command Group

**As a** developer  
**I want** all workflow commands organized under a group  
**So that** the CLI has a logical structure

### Acceptance Criteria

- [ ] Workflow command group created in `Program.cs`
- [ ] Group command: `"workflow"`
- [ ] Group description: "Test workflow orchestration"
- [ ] Subcommands registered:
  - `execute` - [`WorkflowExecuteCommand`](WorkflowExecuteCommand.cs)
  - `status` - [`WorkflowStatusCommand`](WorkflowStatusCommand.cs)
  - `list` - [`WorkflowListCommand`](WorkflowListCommand.cs)
- [ ] Help text comprehensive
- [ ] Command structure: `cdc-cli workflow <subcommand> [options]`

### Test Cases

```bash
# Test group help
cdc-cli workflow --help

# Test subcommand help
cdc-cli workflow execute --help
cdc-cli workflow status --help
cdc-cli workflow list --help
```

### Definition of Done

- Workflow command group properly structured
- All subcommands accessible
- Help text clear

---

## Story 5.5: Integration Tests for Workflow Commands

**As a** developer  
**I want** integration tests for workflow commands  
**So that** I can verify end-to-end functionality

### Acceptance Criteria

- [ ] Integration test class: `WorkflowCommandsIntegrationTests.cs`
- [ ] Test scenarios:
  - [ ] Execute workflow and monitor status
  - [ ] List workflows
  - [ ] Error scenarios (invalid workflow config)
  - [ ] Timeout handling for long workflows
- [ ] Mock/test workflows that complete quickly
- [ ] Verify all output formats
- [ ] Verify exit codes

### Test Example

```csharp
[Fact]
public async Task WorkflowExecution_WithStatusCheck_Success()
{
    // Create test workflow config
    var workflowFile = CreateTestWorkflowConfig();
    
    // Execute workflow
    var executeResult = await RunCommand(
        $"workflow execute --file {workflowFile}");
    var workflowId = ExtractWorkflowId(executeResult.Output);
    Assert.NotNull(workflowId);
    
    // Check status
    var statusResult = await RunCommand(
        $"workflow status {workflowId}");
    Assert.Equal(0, statusResult.ExitCode);
    
    // Verify in list
    var listResult = await RunCommand("workflow list --output json");
    Assert.Contains(workflowId.ToString(), listResult.Output);
}
```

### Definition of Done

- Integration tests implemented
- All scenarios covered
- Tests pass consistently (or skipped for long-running)

---

## Story 5.6: Documentation for Workflow Commands

**As a** user  
**I want** comprehensive documentation for workflow commands  
**So that** I can orchestrate complex test scenarios

### Acceptance Criteria

- [ ] Workflow commands section added to user guide
- [ ] Each command documented
- [ ] Complete workflow JSON schema documented
- [ ] Example workflow configurations:
  - Basic test workflow
  - CDC comparison workflow
  - Trace and replay workflow
- [ ] Best practices:
  - Workflow naming conventions
  - Configuration management
  - Error handling in workflows
  - Monitoring long-running workflows

### Documentation Structure

```markdown
## Workflow Commands

### Overview
Workflows orchestrate multiple CDC operations into repeatable test scenarios...

### workflow execute

#### Workflow Configuration Schema
```json
{
  "workflowName": "string (required)",
  "databaseName": "string (required)",
  ...
}
```

#### Complete Example Workflows

##### Basic CDC Test Workflow
...

##### Performance Comparison Workflow
...

### workflow status
Monitoring long-running workflows...

### workflow list
...

### Best Practices
...
```

### Definition of Done

- Documentation complete
- Full workflow examples provided
- Schema documented
- Reviewed

---

## Phase 5 Completion Criteria

**Phase 5 is complete when:**

✅ All three workflow commands implemented  
✅ Commands properly grouped under `workflow`  
✅ Complex JSON configuration handled  
✅ Long-running execution supported  
✅ Status monitoring working  
✅ Error handling comprehensive  
✅ Unit tests passing (>80% coverage)  
✅ Integration tests passing  
✅ Documentation complete with full examples  
✅ Code reviewed  

**Example Complete Workflow:**
```bash
# Create workflow config file
cat > test-workflow.json <<EOF
{
  "workflowName": "OrderProcessing",
  "databaseName": "TestDB",
  "connectionString": "Server=localhost;Database=TestDB;...",
  "traceConnectionString": "Server=localhost;Database=CdcMe;...",
  "baselineSnapshotName": "baseline",
  "testSnapshotName": "after-test",
  "traceSessionName": "order-test",
  "enableCdc": true,
  "cdcTables": ["dbo.Orders"]
}
EOF

# Execute workflow
WORKFLOW_ID=$(cdc-cli workflow execute --file test-workflow.json --output json | jq -r '.workflowId')

# Monitor progress
while true; do
  STATUS=$(cdc-cli workflow status $WORKFLOW_ID --output json | jq -r '.status')
  if [ "$STATUS" == "Completed" ]; then
    echo "Workflow completed successfully"
    break
  fi
  sleep 5
done

# View all workflows
cdc-cli workflow list
```

**Next Phase**: Phase 6 - Testing, Documentation, and Deployment
