# CDC API Usage Guide

## Overview

The CDC API provides REST endpoints for managing Change Data Capture (CDC) operations on SQL Server databases. This guide shows you how to use the API to capture database changes during testing scenarios.

## Prerequisites

1. **Database Migration**: Run the database migration script from [`docs/cdc-database-migration.md`](./cdc-database-migration.md) to update your CdcMe PostgreSQL database schema.

2. **Database Configuration**: Ensure your `appsettings.json` or environment variables are configured with:

   - `TEST_DB_CONNECTION`: SQL Server connection string for the database under test
   - `CDCME_DB_CONNECTION`: PostgreSQL connection string for the CdcMe database

3. **Permissions**: The SQL Server user must have permissions to enable/disable CDC and create CDC tables.

## API Endpoints

### 1. Start CDC Operations

**Endpoint**: `POST /cdc/start`

**Purpose**: Enable CDC on the database and specified tables.

**Request Body**:

```json
{
  "sessionName": "order-processing-test",
  "tablesToInclude": ["dbo.Orders", "dbo.OrderItems"],
  "tablesToExclude": ["dbo.AuditLog", "dbo.TempData"]
}
```

**Parameters**:

- `sessionName` (required): Unique name for this CDC session
- `tablesToInclude` (optional): Specific tables to monitor. If not provided, all user tables are included.
- `tablesToExclude` (optional): Tables to exclude from CDC monitoring

**Response**:

```json
{
  "success": true,
  "sessionName": "order-processing-test",
  "message": "CDC enabled successfully on 2 tables",
  "tablesEnabled": ["dbo.Orders", "dbo.OrderItems"],
  "tablesSkipped": ["dbo.AuditLog"],
  "errors": []
}
```

### 2. Stop CDC and Capture Data

**Endpoint**: `POST /cdc/stop`

**Purpose**: Capture all CDC data, save it to the CdcMe database with a name, and disable CDC.

**Request Body**:

```json
{
  "sessionName": "order-processing-test",
  "captureName": "baseline-capture",
  "captureType": "Baseline"
}
```

**Parameters**:

- `sessionName` (required): The session name used when starting CDC
- `captureName` (required): Name for this specific capture (for later retrieval)
- `captureType` (optional): Type of capture - "Baseline", "Replay", "Optimized", etc.

**Response**:

```json
{
  "success": true,
  "sessionName": "order-processing-test",
  "captureName": "baseline-capture",
  "message": "CDC data captured and CDC disabled successfully",
  "tablesWithChanges": ["dbo_Orders", "dbo_OrderItems"],
  "totalRecords": 150,
  "captureId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "errors": []
}
```

### 3. Capture Data Without Stopping CDC

**Endpoint**: `POST /cdc/capture`

**Purpose**: Capture CDC data at a point in time without stopping CDC (for intermediate captures).

**Request Body**:

```json
{
  "sessionName": "order-processing-test",
  "captureName": "checkpoint-1",
  "captureType": "Intermediate"
}
```

**Response**: Similar to the stop endpoint, but CDC remains active.

## Usage Examples

### Basic Workflow

```bash
# 1. Start CDC monitoring
curl -X POST http://localhost:5000/cdc/start \
  -H "Content-Type: application/json" \
  -d '{
    "sessionName": "my-test-session",
    "tablesToInclude": ["dbo.Orders", "dbo.Customers"]
  }'

# 2. Run your business operations here
# ... your application logic that modifies data ...

# 3. Stop CDC and capture the changes
curl -X POST http://localhost:5000/cdc/stop \
  -H "Content-Type: application/json" \
  -d '{
    "sessionName": "my-test-session",
    "captureName": "baseline-capture",
    "captureType": "Baseline"
  }'
```

### Advanced Workflow with Intermediate Captures

```bash
# 1. Start CDC
curl -X POST http://localhost:5000/cdc/start \
  -H "Content-Type: application/json" \
  -d '{
    "sessionName": "performance-test",
    "tablesToExclude": ["dbo.AuditLog", "dbo.SystemLog"]
  }'

# 2. Run first batch of operations
# ... business operations ...

# 3. Take intermediate capture
curl -X POST http://localhost:5000/cdc/capture \
  -H "Content-Type: application/json" \
  -d '{
    "sessionName": "performance-test",
    "captureName": "after-batch-1",
    "captureType": "Intermediate"
  }'

# 4. Run second batch of operations
# ... more business operations ...

# 5. Final capture and stop
curl -X POST http://localhost:5000/cdc/stop \
  -H "Content-Type: application/json" \
  -d '{
    "sessionName": "performance-test",
    "captureName": "final-capture",
    "captureType": "Baseline"
  }'
```

## Table Filtering

### Include Specific Tables

```json
{
  "sessionName": "focused-test",
  "tablesToInclude": ["dbo.Orders", "dbo.OrderItems", "dbo.Customers"]
}
```

Only the specified tables will have CDC enabled.

### Exclude Specific Tables

```json
{
  "sessionName": "broad-test",
  "tablesToExclude": ["dbo.AuditLog", "dbo.TempData", "dbo.SystemLog"]
}
```

All user tables except the excluded ones will have CDC enabled.

### Combined Filtering

```json
{
  "sessionName": "precise-test",
  "tablesToInclude": [
    "dbo.Orders",
    "dbo.OrderItems",
    "dbo.Customers",
    "dbo.AuditLog"
  ],
  "tablesToExclude": ["dbo.AuditLog"]
}
```

Start with the include list, then remove the exclude list. Result: `["dbo.Orders", "dbo.OrderItems", "dbo.Customers"]`

## Data Storage

### Where Data is Stored

Captured CDC data is stored in the CdcMe PostgreSQL database using a header-detail pattern:

- **`cdc_capture_headers`**: One record per capture operation with metadata
- **`cdc_captures`**: One record per table within each capture with the actual CDC data

### Querying Captured Data

```sql
-- Get all captures for a session
SELECT
    h.capture_name,
    h.capture_type,
    h.capture_time,
    h.total_records,
    h.tables_enabled
FROM cdc_capture_headers h
JOIN trace_sessions s ON h.session_id = s.session_id
WHERE s.session_name = 'my-test-session'
ORDER BY h.capture_time;

-- Get detailed data for a specific capture
SELECT
    h.capture_name,
    c.table_name,
    c.record_count,
    c.capture_data
FROM cdc_capture_headers h
JOIN cdc_captures c ON h.capture_header_id = c.capture_header_id
WHERE h.capture_name = 'baseline-capture';
```

## Error Handling

### Common Error Scenarios

1. **Session Not Found**:

   ```json
   {
     "success": false,
     "message": "Session 'unknown-session' not found. Please start CDC first.",
     "errors": ["Session 'unknown-session' not found. Please start CDC first."]
   }
   ```

2. **Table Without Primary Key**:

   ```json
   {
     "success": true,
     "message": "CDC enabled successfully on 2 tables",
     "tablesEnabled": ["dbo.Orders"],
     "tablesSkipped": ["dbo.LogTable"],
     "errors": ["Table dbo.LogTable skipped - no primary key"]
   }
   ```

3. **Database Connection Issues**:
   ```json
   {
     "success": false,
     "message": "Error starting CDC: Unable to connect to database",
     "errors": ["Unable to connect to database"]
   }
   ```

### Best Practices

1. **Check Response Status**: Always check the `success` field in responses
2. **Handle Partial Success**: Some tables may be skipped due to missing primary keys
3. **Monitor Errors Array**: Review the `errors` array for detailed error information
4. **Unique Session Names**: Use unique session names to avoid conflicts
5. **Meaningful Capture Names**: Use descriptive capture names for easy identification

## Performance Considerations

1. **Table Filtering**: Use `tablesToInclude` or `tablesToExclude` to limit CDC to relevant tables
2. **Capture Frequency**: Avoid too frequent intermediate captures as they can impact performance
3. **Data Volume**: Large CDC captures may take time to process and store
4. **Cleanup**: Consider implementing cleanup procedures for old capture data

## Integration with Testing Frameworks

### Example with xUnit (C#)

```csharp
[Fact]
public async Task TestOrderProcessing()
{
    // Start CDC
    var startRequest = new { sessionName = "order-test", tablesToInclude = new[] { "dbo.Orders" } };
    await _httpClient.PostAsJsonAsync("/cdc/start", startRequest);

    // Run business logic
    await ProcessOrders();

    // Capture and stop CDC
    var stopRequest = new { sessionName = "order-test", captureName = "test-result", captureType = "Test" };
    var response = await _httpClient.PostAsJsonAsync("/cdc/stop", stopRequest);

    // Verify response
    var result = await response.Content.ReadFromJsonAsync<StopCdcResponse>();
    Assert.True(result.Success);
    Assert.True(result.TotalRecords > 0);
}
```

### Example with pytest (Python)

```python
import requests

def test_customer_updates():
    # Start CDC
    start_response = requests.post("http://localhost:5000/cdc/start", json={
        "sessionName": "customer-test",
        "tablesToInclude": ["dbo.Customers"]
    })
    assert start_response.json()["success"]

    # Run business operations
    update_customers()

    # Stop and capture
    stop_response = requests.post("http://localhost:5000/cdc/stop", json={
        "sessionName": "customer-test",
        "captureName": "customer-updates",
        "captureType": "Test"
    })

    result = stop_response.json()
    assert result["success"]
    assert result["totalRecords"] > 0
```

## Troubleshooting

### Common Issues

1. **CDC Not Starting**: Check SQL Server permissions and ensure CDC is supported
2. **No Data Captured**: Verify that changes were made to CDC-enabled tables
3. **Session Conflicts**: Use unique session names or ensure previous sessions are stopped
4. **Database Connection**: Verify connection strings in configuration

### Debugging Tips

1. Check application logs for detailed error messages
2. Verify database connectivity using the existing health check endpoints
3. Ensure the CdcMe database schema migration has been applied
4. Test with a simple table first before using complex filtering

This API provides a powerful way to capture and analyze database changes during testing, enabling you to verify data consistency and compare different implementations.
