# Phase 3: Snapshot Commands - User Stories

## Overview

Phase 3 implements the Snapshot command group for managing SQL Server database snapshots.

**Prerequisites**: Phase 1 (Foundation) must be complete

---

## Story 3.1: Implement `snapshot create` Command

**As a** user  
**I want** to create database snapshots via CLI  
**So that** I can establish baseline states for testing

### API Endpoint

`POST /api/snapshot`

### Acceptance Criteria

- [ ] `SnapshotCreateCommand` class created in `Commands/Snapshot/SnapshotCreateCommand.cs`
- [ ] Command inherits from [`ApiCommandBase`](ApiCommandBase.cs)
- [ ] Command registered with name `"create"` under `"snapshot"` group
- [ ] Command description: "Create a database snapshot"
- [ ] Command options:
  ```
  --database, -d <name>      Database name (required)
  --snapshot, -s <name>      Snapshot name (required)
  --data <json>              JSON payload as string
  --file, -f <path>          Path to JSON file
  ```
- [ ] Request building from CLI parameters
- [ ] API call to `POST /api/snapshot`
- [ ] Response output with snapshot creation result
- [ ] Validation:
  - Database name required
  - Snapshot name required
- [ ] Error handling for duplicate snapshots, permissions

### Usage Examples

```bash
# Create snapshot with CLI parameters
cdc-cli snapshot create --database "TestDB" --snapshot "baseline-snapshot"

# Using JSON file
cdc-cli snapshot create --file create-snapshot.json

# Using stdin
echo '{"databaseName":"TestDB","snapshotName":"baseline"}' | cdc-cli snapshot create
```

### Definition of Done

- Command implemented
- All input methods working
- Tests passing
- Help text complete

---

## Story 3.2: Implement `snapshot restore` Command

**As a** user  
**I want** to restore databases from snapshots  
**So that** I can return to baseline states for repeat testing

### API Endpoint

`POST /api/snapshot/restore`

### Acceptance Criteria

- [ ] `SnapshotRestoreCommand` class created in `Commands/Snapshot/SnapshotRestoreCommand.cs`
- [ ] Command registered with name `"restore"` under `"snapshot"` group
- [ ] Command description: "Restore a database from snapshot"
- [ ] Command options:
  ```
  --database, -d <name>      Database name (required)
  --snapshot, -s <name>      Snapshot name (required)
  --data <json>              JSON payload as string
  --file, -f <path>          Path to JSON file
  ```
- [ ] API call to `POST /api/snapshot/restore`
- [ ] Response output with restore result
- [ ] Warning in help text: "This will overwrite the current database"
- [ ] Validation for required fields

### Usage Examples

```bash
# Restore from snapshot
cdc-cli snapshot restore --database "TestDB" --snapshot "baseline-snapshot"

# Using JSON
cdc-cli snapshot restore --file restore-snapshot.json
```

### Definition of Done

- Command implemented
- Warning displayed appropriately
- Tests passing
- Help text complete

---

## Story 3.3: Implement `snapshot list` Command

**As a** user  
**I want** to list available snapshots  
**So that** I can see what snapshots exist for a database

### API Endpoint

`GET /api/snapshot/{databaseName}/snapshots`

### Acceptance Criteria

- [ ] `SnapshotListCommand` class created in `Commands/Snapshot/SnapshotListCommand.cs`
- [ ] Command registered with name `"list"` under `"snapshot"` group
- [ ] Command description: "List all snapshots for a database"
- [ ] Command options:
  ```
  --database, -d <name>      Database name (required)
  ```
- [ ] API call to `GET /api/snapshot/{databaseName}/snapshots`
- [ ] Response output as JSON array or formatted table
- [ ] Text output format shows:
  - Snapshot name
  - Creation time
  - Size (if available)
- [ ] Handle empty list gracefully

### Usage Examples

```bash
# List all snapshots for database
cdc-cli snapshot list --database "TestDB"

# JSON output for scripting
cdc-cli snapshot list --database "TestDB" --output json

# Pretty formatted
cdc-cli snapshot list --database "TestDB" --output json-pretty

# Text table format
cdc-cli snapshot list --database "TestDB" --output text
```

### Definition of Done

- Command implemented
- Multiple output formats working
- Empty list handled
- Tests passing

---

## Story 3.4: Implement `snapshot info` Command

**As a** user  
**I want** to get detailed information about a snapshot  
**So that** I can verify snapshot properties

### API Endpoint

`GET /api/snapshot/{databaseName}/snapshots/{snapshotName}`

### Acceptance Criteria

- [ ] `SnapshotInfoCommand` class created in `Commands/Snapshot/SnapshotInfoCommand.cs`
- [ ] Command registered with name `"info"` under `"snapshot"` group
- [ ] Command description: "Get detailed snapshot information"
- [ ] Command options:
  ```
  --database, -d <name>      Database name (required)
  --snapshot, -s <name>      Snapshot name (required)
  ```
- [ ] API call to `GET /api/snapshot/{databaseName}/snapshots/{snapshotName}`
- [ ] Response shows:
  - Snapshot name
  - Source database
  - Creation time
  - Size
  - Status
- [ ] Handle not found (404) gracefully

### Usage Examples

```bash
# Get snapshot info
cdc-cli snapshot info --database "TestDB" --snapshot "baseline-snapshot"

# JSON output
cdc-cli snapshot info --database "TestDB" --snapshot "baseline" --output json
```

### Definition of Done

- Command implemented
- Not found handled
- Tests passing
- Help text complete

---

## Story 3.5: Implement `snapshot delete` Command

**As a** user  
**I want** to delete snapshots  
**So that** I can clean up unused snapshots

### API Endpoint

`DELETE /api/snapshot/{snapshotName}`

### Acceptance Criteria

- [ ] `SnapshotDeleteCommand` class created in `Commands/Snapshot/SnapshotDeleteCommand.cs`
- [ ] Command registered with name `"delete"` under `"snapshot"` group
- [ ] Command description: "Delete a database snapshot"
- [ ] Command options:
  ```
  --snapshot, -s <name>      Snapshot name (required)
  --force, -f                Skip confirmation (optional)
  ```
- [ ] API call to `DELETE /api/snapshot/{snapshotName}`
- [ ] Optional confirmation prompt (unless --force)
- [ ] Response output with deletion result
- [ ] Handle not found gracefully

### Usage Examples

```bash
# Delete with confirmation
cdc-cli snapshot delete --snapshot "old-snapshot"

# Delete without confirmation
cdc-cli snapshot delete --snapshot "old-snapshot" --force

# Batch delete with script
for snap in $(cdc-cli snapshot list --database "TestDB" --output json | jq -r '.[].snapshotName')
do
  cdc-cli snapshot delete --snapshot "$snap" --force
done
```

### Definition of Done

- Command implemented
- Confirmation working (skippable with --force)
- Tests passing
- Help text complete

---

## Story 3.6: Create Snapshot Command Group

**As a** developer  
**I want** all snapshot commands organized under a group  
**So that** the CLI has a logical structure

### Acceptance Criteria

- [ ] Snapshot command group created in `Program.cs`
- [ ] Group command: `"snapshot"`
- [ ] Group description: "Database snapshot management"
- [ ] Subcommands registered:
  - `create` - [`SnapshotCreateCommand`](SnapshotCreateCommand.cs)
  - `restore` - [`SnapshotRestoreCommand`](SnapshotRestoreCommand.cs)
  - `list` - [`SnapshotListCommand`](SnapshotListCommand.cs)
  - `info` - [`SnapshotInfoCommand`](SnapshotInfoCommand.cs)
  - `delete` - [`SnapshotDeleteCommand`](SnapshotDeleteCommand.cs)
- [ ] Help text comprehensive
- [ ] Command structure: `cdc-cli snapshot <subcommand> [options]`

### Test Cases

```bash
# Test group help
cdc-cli snapshot --help

# Test each subcommand help
cdc-cli snapshot create --help
cdc-cli snapshot restore --help
cdc-cli snapshot list --help
cdc-cli snapshot info --help
cdc-cli snapshot delete --help
```

### Definition of Done

- Snapshot command group properly structured
- All subcommands accessible
- Help text clear

---

## Story 3.7: Integration Tests for Snapshot Commands

**As a** developer  
**I want** integration tests for snapshot commands  
**So that** I can verify end-to-end functionality

### Acceptance Criteria

- [ ] Integration test class: `SnapshotCommandsIntegrationTests.cs`
- [ ] Test scenarios:
  - [ ] Create, list, info, delete workflow
  - [ ] Create, restore workflow
  - [ ] List empty snapshots
  - [ ] Delete non-existent snapshot
  - [ ] Error scenarios
- [ ] Test with all input methods
- [ ] Test all output formats
- [ ] Verify exit codes

### Test Example

```csharp
[Fact]
public async Task SnapshotWorkflow_CreateRestoreDelete_Success()
{
    var dbName = "TestDB";
    var snapName = $"test-snap-{Guid.NewGuid()}";
    
    // Create
    var createResult = await RunCommand(
        $"snapshot create --database {dbName} --snapshot {snapName}");
    Assert.Equal(0, createResult.ExitCode);
    
    // List - verify exists
    var listResult = await RunCommand($"snapshot list --database {dbName}");
    Assert.Contains(snapName, listResult.Output);
    
    // Restore
    var restoreResult = await RunCommand(
        $"snapshot restore --database {dbName} --snapshot {snapName}");
    Assert.Equal(0, restoreResult.ExitCode);
    
    // Delete
    var deleteResult = await RunCommand(
        $"snapshot delete --snapshot {snapName} --force");
    Assert.Equal(0, deleteResult.ExitCode);
}
```

### Definition of Done

- Integration tests implemented
- All scenarios covered
- Tests pass consistently

---

## Story 3.8: Documentation for Snapshot Commands

**As a** user  
**I want** comprehensive documentation for snapshot commands  
**So that** I can use them effectively

### Acceptance Criteria

- [ ] Snapshot commands section added to user guide
- [ ] Each command documented with:
  - Description
  - Parameters
  - Examples (basic and advanced)
  - Common issues
- [ ] Workflows documented:
  - Basic snapshot workflow
  - Test-restore-test cycle
  - Snapshot cleanup
- [ ] Best practices:
  - Naming conventions
  - Snapshot lifecycle management
  - Storage considerations

### Documentation Structure

```markdown
## Snapshot Commands

### Overview
Database snapshots provide point-in-time copies...

### snapshot create
...

### snapshot restore
...

### snapshot list
...

### snapshot info
...

### snapshot delete
...

### Common Workflows

#### Test-Restore-Test Pattern
1. Create baseline snapshot
2. Run test scenario
3. Restore to baseline
4. Run modified test
5. Compare results
```

### Definition of Done

- Documentation complete
- Examples tested
- Reviewed

---

## Phase 3 Completion Criteria

**Phase 3 is complete when:**

✅ All five snapshot commands implemented  
✅ Commands properly grouped under `snapshot`  
✅ All input methods working  
✅ Output formats working  
✅ Confirmation prompts working (delete)  
✅ Error handling comprehensive  
✅ Unit tests passing (>80% coverage)  
✅ Integration tests passing  
✅ Documentation complete  
✅ Code reviewed  

**Example Complete Workflow:**
```bash
# Create snapshot
cdc-cli snapshot create --database "TestDB" --snapshot "baseline"

# List snapshots
cdc-cli snapshot list --database "TestDB"

# Get snapshot details
cdc-cli snapshot info --database "TestDB" --snapshot "baseline"

# Restore database
cdc-cli snapshot restore --database "TestDB" --snapshot "baseline"

# Clean up
cdc-cli snapshot delete --snapshot "baseline" --force
```

**Next Phase**: Phase 4 - Trace Commands Implementation
