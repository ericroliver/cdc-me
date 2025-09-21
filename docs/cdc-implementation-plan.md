# CDC API Implementation Plan

## Summary

This document outlines the complete implementation plan for exposing CDC (Change Data Capture) capabilities through REST API endpoints. The design has been reviewed and approved, and all necessary documentation and database schema changes have been prepared.

## What's Been Completed ✅

### 1. API Design

- **Three main endpoints** designed: `/api/cdc/start`, `/api/cdc/stop`, `/api/cdc/capture`
- **Request/Response models** created in [`cdc-api/Models/CdcModels.cs`](../cdc-api/Models/CdcModels.cs)
- **Comprehensive API specification** documented in [`docs/cdc-api-design.md`](./cdc-api-design.md)

### 2. Database Schema Design

- **Header-detail pattern** designed for better data organization
- **New `cdc_capture_headers` table** to store capture metadata (name, filters, etc.)
- **Modified `cdc_captures` table** to store per-table CDC data
- **Complete migration script** provided in [`docs/cdc-database-migration.md`](./cdc-database-migration.md)

### 3. Documentation

- **Design document**: Complete API specification with examples
- **Schema proposal**: Detailed database design rationale
- **Migration guide**: Step-by-step database update instructions
- **Implementation plan**: This document

## Key Design Decisions

### API Workflow

1. **Start CDC**: Enable CDC on database and filtered tables, create session
2. **Business Operations**: User runs their test scenarios
3. **Stop CDC**: Capture all CDC data, save to CdcMe database, disable CDC

### Data Organization

- **Sessions**: Group related CDC operations under named sessions
- **Captures**: Each stop operation creates a named capture with full metadata
- **Table Filtering**: Flexible include/exclude patterns for table selection

### Database Integration

- **TestDatabase** (SQL Server): Where CDC operations are performed
- **CdcMeDatabase** (PostgreSQL): Where captured data is stored permanently

## Ready for Implementation 🚀

The following components are ready to be implemented:

### 1. Database Migration

- Run the migration script from [`docs/cdc-database-migration.md`](./cdc-database-migration.md)
- This creates the new `cdc_capture_headers` table and updates `cdc_captures`

### 2. Controller Implementation

- Update [`cdc-api/Controllers/CdcController.cs`](../cdc-api/Controllers/CdcController.cs)
- Inject `IDatabaseConnectionFactory` for database access
- Implement the three main endpoints using existing [`CdcDataUtilities`](../cdc-lib/Cdc/CdcDataUtilities.cs) methods

### 3. Core Implementation Tasks

#### Table Filtering Logic

```csharp
private static IEnumerable<SqlTable> FilterTables(
    IEnumerable<SqlTable> allTables,
    List<string>? tablesToInclude,
    List<string>? tablesToExclude)
{
    // Implementation logic for include/exclude filtering
}
```

#### CDC Data Persistence

```csharp
private async Task<string> SaveCdcCaptureAsync(
    string sessionName,
    string captureName,
    string captureType,
    IDictionary<string, IEnumerable<IDictionary<string, object>>> cdcData,
    List<string> tablesEnabled,
    List<string> tablesSkipped)
{
    // Create header record in cdc_capture_headers
    // Create detail records in cdc_captures
    // Return capture header ID
}
```

## Implementation Dependencies

### Required Services

- `IDatabaseConnectionFactory` - Already available
- `ILogger<CdcController>` - Standard ASP.NET Core logging

### Required Libraries

- `System.Security.Cryptography` - For SHA256 hashing
- `System.Text.Json` - For JSON serialization
- Existing CDC utilities from `cdc-lib`

## Testing Strategy

### Unit Tests

- Test table filtering logic with various include/exclude combinations
- Test CDC data transformation and hashing
- Mock database operations for isolated testing

### Integration Tests

- Test full CDC workflow with real databases
- Verify data persistence in CdcMe database
- Test error handling scenarios

## API Usage Examples

### Basic Workflow

```bash
# 1. Start CDC with table filtering
POST /api/cdc/start
{
  "sessionName": "order-processing-test",
  "tablesToInclude": ["dbo.Orders", "dbo.OrderItems"],
  "tablesToExclude": ["dbo.AuditLog"]
}

# 2. Run business operations (external to API)
# ... business logic executes, CDC captures changes ...

# 3. Stop CDC and capture data
POST /api/cdc/stop
{
  "sessionName": "order-processing-test",
  "captureName": "baseline-capture",
  "captureType": "Baseline"
}
```

### Advanced Usage

```bash
# Optional: Capture intermediate data without stopping CDC
POST /api/cdc/capture
{
  "sessionName": "order-processing-test",
  "captureName": "checkpoint-1",
  "captureType": "Intermediate"
}
```

## Error Handling Strategy

- **Per-table errors**: Continue processing other tables, report errors in response
- **Database-level errors**: Fail fast with detailed error messages
- **Session management errors**: Provide clear guidance on session state
- **Validation errors**: Return 400 Bad Request with specific field errors

## Performance Considerations

- **Large datasets**: Consider streaming for very large CDC captures
- **Table filtering**: Reduces overhead by only monitoring relevant tables
- **Hash calculation**: Enables quick data comparison without full content comparison
- **Indexing**: Proper database indexes for efficient querying

## Security Considerations

- **Input validation**: Sanitize session names and table names
- **SQL injection**: Use parameterized queries for all database operations
- **Database permissions**: Ensure proper CDC permissions on SQL Server
- **Access control**: Consider adding authentication/authorization if needed

## Next Steps

1. **Run Database Migration**: Execute the migration script on CdcMe database
2. **Switch to Code Mode**: Begin implementation of the controller
3. **Implement Core Logic**: Start with table filtering and basic CDC operations
4. **Add Data Persistence**: Implement the header-detail data saving logic
5. **Add Error Handling**: Comprehensive error handling and logging
6. **Write Tests**: Unit and integration tests for all functionality
7. **Update Documentation**: API documentation and usage examples

## Questions for Implementation

1. **Session Storage**: Should we store the original table filters in memory during the session, or retrieve them from the database?
2. **Concurrent Sessions**: How should we handle multiple CDC sessions running simultaneously?
3. **Data Retention**: Should there be automatic cleanup of old CDC captures?
4. **Monitoring**: Do we need real-time status endpoints for long-running CDC operations?

The design is complete and ready for implementation. All major architectural decisions have been made, and the database schema is prepared for the new functionality.
