# Reviewer Feedback Implementation Plan

This document provides detailed implementation guidance for addressing all reviewer feedback issues identified in `docs/reviewer-feedback.md`.

## Overview

The feedback has been categorized by priority and impact:

- **Critical Priority**: Security and data integrity issues that could cause system failures
- **High Priority**: Breaking changes and API design issues
- **Medium Priority**: Code quality and maintainability improvements
- **Low Priority**: Test fixes and documentation updates

## Critical Priority Fixes

### 1. Remove Hard-coded Credentials from Configuration

**Issue**: Hard-coded production-like credentials in `cdc-api/appsettings.json`

**Files to modify**:

- `cdc-api/appsettings.json`
- `cdc-api/Program.cs` (if needed for configuration binding)

**Implementation**:

```json
// cdc-api/appsettings.json - Replace lines 10-12
"ConnectionStrings": {
  "TEST_DB_CONNECTION": "",
  "CDCME_DB_CONNECTION": ""
}
```

**Configuration approach**:

- Use environment variables: `TEST_DB_CONNECTION` and `CDCME_DB_CONNECTION`
- The existing `.env.example` file already documents the expected format
- ASP.NET Core will automatically bind environment variables to `ConnectionStrings:*` configuration

### 2. Fix Column Index Mismatch in SqlServerTraceProvider

**Issue**: Column indexes are off by one after removing TestConnectionString from SELECT

**File**: `cdc-lib/Trace/SqlServerTraceProvider.cs`

**Problem**: The `MapTraceSession` method (lines 508-518) uses incorrect column indexes:

- SnapshotName uses index 4, should be 3
- StartTime uses index 5, should be 4
- EndTime uses index 6, should be 5
- Status uses index 7, should be 6
- CreatedBy uses index 8, should be 7
- Description uses index 9, should be 8
- Configuration uses index 10, should be 9

**Implementation**:

```csharp
// Fix MapTraceSession method around line 506
return new TraceSession
{
    SessionId = reader.GetGuid(0),
    SessionName = reader.GetString(1),
    TestDatabase = reader.GetString(2),
    SnapshotName = reader.IsDBNull(3) ? null : reader.GetString(3),  // Changed from 4 to 3
    StartTime = reader.GetDateTime(4),                               // Changed from 5 to 4
    EndTime = reader.IsDBNull(5) ? null : reader.GetDateTime(5),     // Changed from 6 to 5
    Status = reader.GetString(6),                                    // Changed from 7 to 6
    CreatedBy = reader.GetString(7),                                 // Changed from 8 to 7
    Description = reader.IsDBNull(8) ? null : reader.GetString(8),   // Changed from 9 to 8
    Configuration = config
};

// Also fix the configuration JSON reading (line 492)
var configJson = reader.IsDBNull(9) ? null : reader.GetString(9);   // Changed from 10 to 9
```

### 3. Add Transaction Wrapping to CDC Capture Operations

**Issue**: CDC capture operations execute individually without transactions, causing partial captures on failure

**File**: `cdc-api/Controllers/CdcController.cs`

**Method**: `SaveCdcCaptureAsync` (lines 350-420)

**Implementation**:

```csharp
private async Task<string> SaveCdcCaptureAsync(
    SimpleDac cdcMeDac,
    string sessionName,
    string captureName,
    string captureType,
    IDictionary<string, IEnumerable<IDictionary<string, object>>> cdcData,
    List<string> tablesEnabled,
    List<string> tablesSkipped)
{
    // Wrap entire operation in a transaction
    using var transaction = await cdcMeDac.BeginTransactionAsync();
    try
    {
        // Step 1: Get session ID (existing code)
        const string getSessionSql = "SELECT session_id FROM trace_sessions WHERE session_name = @sessionName";
        var sessionId = await cdcMeDac.ExecuteScalarAsync<Guid>(getSessionSql,
            new Dictionary<string, object> { ["sessionName"] = sessionName });

        if (sessionId == Guid.Empty)
        {
            throw new InvalidOperationException($"Session '{sessionName}' not found. Please start CDC first.");
        }

        // Step 2: Create capture header (existing code)
        // ... existing header creation code ...

        // Step 3: Create capture details for each table (existing code)
        // ... existing detail creation code ...

        // Commit transaction
        await transaction.CommitAsync();
        return captureHeaderId.ToString();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

**Note**: This assumes `SimpleDac` supports transactions. If not, we'll need to modify the `SimpleDac` class or use the underlying connection directly.

### 4. Add ModelState Validation to API Controllers

**Issue**: Model validation attributes are present but ModelState is never checked

**Files**:

- `cdc-api/Controllers/CdcController.cs`
- `cdc-api/Controllers/SnapshotController.cs`
- `cdc-api/Controllers/TraceController.cs`

**Implementation**: Add validation to all action methods:

```csharp
[HttpPost("start")]
public async Task<ActionResult<StartCdcResponse>> StartCdc([FromBody] StartCdcRequest request)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(ModelState);
    }

    // ... existing implementation
}
```

Apply this pattern to all POST/PUT endpoints that accept request models.

## High Priority Fixes

### 5. Redesign DELETE Snapshot Endpoint

**Issue**: DELETE endpoint requires JSON body, which is unconventional and causes client compatibility issues

**File**: `cdc-api/Controllers/SnapshotController.cs`

**Current**: `DELETE /api/snapshot` with JSON body `{ "SnapshotName": "name" }`
**New**: `DELETE /api/snapshot/{snapshotName}`

**Implementation**:

```csharp
[HttpDelete("{snapshotName}")]
public async Task<ActionResult<SnapshotResult>> DeleteSnapshot(string snapshotName)
{
    if (string.IsNullOrWhiteSpace(snapshotName))
    {
        return BadRequest("Snapshot name is required");
    }

    _logger.LogInformation("Deleting snapshot {SnapshotName}", snapshotName);

    var result = await _snapshotManager.DropSnapshotAsync(snapshotName);

    if (result.Success)
    {
        return Ok(result);
    }

    return BadRequest(result);
}
```

### 6. Create CHANGELOG.md

**File**: `CHANGELOG.md` (new file)

**Content**: Document all breaking changes, especially:

- ISnapshotManager interface changes
- SnapshotManager restore behavior changes
- API endpoint changes (DELETE snapshot)

## Medium Priority Fixes

### 7. Fix Duplicate session_id in Extended Events

**Issue**: Duplicate `sqlserver.session_id` in ACTION list

**File**: `cdc-lib/Trace/TraceManager.cs`

**Lines**: 419-424 and 427-430

**Implementation**: Remove duplicate `sqlserver.session_id` from ACTION lists:

```csharp
// Fix both sql_batch_completed and rpc_completed events
ACTION(sqlserver.client_app_name, sqlserver.client_hostname,
       sqlserver.database_name, sqlserver.session_id, sqlserver.username,
       sqlserver.sql_text, sqlserver.tsql_stack, sqlserver.plan_handle,
       sqlserver.request_id, sqlserver.client_connection_id, sqlserver.transaction_id)
```

### 8. Fix Error Message in SnapshotManager

**Issue**: Error message says "restore" instead of "drop"

**File**: `cdc-lib/Trace/SnapshotManager.cs`

**Line**: 221

**Implementation**:

```csharp
Message = $"Failed to drop snapshot: {ex.Message}",
```

### 9. Replace Hard-coded "TestDatabase" Value

**Issue**: Hard-coded testDatabase value stores incorrect database name

**File**: `cdc-api/Controllers/CdcController.cs`

**Line**: 330

**Implementation**: Get actual database name from connection factory:

```csharp
// Replace hard-coded value with actual database name
["testDatabase"] = testDac.DatabaseName, // or extract from connection string
```

### 10. Centralize Session Name Generation

**Issue**: Hard-coded session name format is duplicated

**File**: `cdc-lib/Trace/TraceManager.cs`

**Lines**: 69, 73, 76 (and others)

**Implementation**: Replace all instances of `$"CDC_Trace_{sessionId:N}"` with `GetExtendedEventsSessionName(sessionId)`

### 11. Fix Event Data Extraction Logic

**Issue**: Mixing retrieval of sql_text as action and statement as action

**File**: `cdc-lib/Trace/TraceManager.cs`

**Implementation**: Use correct XPath for event data vs actions:

```sql
-- For statement data (event data, not action)
event_data.value('(data[@name=''statement'']/value)[1]', 'nvarchar(max)') AS statement,

-- For sql_text (action data)
event_data.value('(action[@name=''sql_text'']/value)[1]', 'nvarchar(max)') AS sql_text,
```

## Low Priority Fixes

### 12. Extract Shared Filtering Logic

**Issue**: Filtering logic is duplicated between controller and tests

**File**: `cdc-api/Controllers/CdcController.cs`

**Implementation**: Make `FilterTables` method internal and use `InternalsVisibleTo` attribute, or create a shared utility class.

### 13. Fix Test Naming and Expectations

**Issue**: Test expects failure but is named as success test

**File**: `cdc-api.Tests/Controllers/SnapshotControllerTests.cs`

**Implementation**: Either fix the test to expect success or rename it to reflect the expected failure behavior.

## Implementation Order

1. **Critical Priority** (must be done first):

   - Remove hard-coded credentials
   - Fix column index mismatch
   - Add transaction wrapping
   - Add ModelState validation

2. **High Priority** (breaking changes):

   - Redesign DELETE endpoint
   - Create CHANGELOG.md

3. **Medium Priority** (code quality):

   - Fix Extended Events duplicates
   - Fix error messages
   - Replace hard-coded values
   - Centralize session name logic
   - Fix event data extraction

4. **Low Priority** (cleanup):
   - Extract shared logic
   - Fix tests
   - Update documentation

## Testing Strategy

After implementing fixes:

1. Run `dotnet build cdc-me.sln` to ensure no compilation errors
2. Run `dotnet test cdc-me.sln` to ensure all tests pass
3. Test API endpoints manually or with integration tests
4. Verify environment variable configuration works correctly

## Risk Assessment

- **Low Risk**: Code quality improvements, documentation updates
- **Medium Risk**: API endpoint changes (may affect clients)
- **High Risk**: Database transaction changes, column index fixes (could cause runtime errors if incorrect)

Test thoroughly in development environment before deploying.
