# CDC Capture Comparison

## Overview

The CDC Capture Comparison feature allows you to compare two CDC captures to validate that refactored stored procedures or optimized code produce identical data changes. This is essential for ensuring that performance improvements or code refactoring don't introduce data inconsistencies.

## Architecture

### Components

1. **CdcCaptureComparer** (`cdc-lib/Cdc/CdcCaptureComparer.cs`)
   - Core comparison engine
   - Retrieves capture data from the trace database
   - Performs detailed field-by-field comparison
   - Generates comprehensive failure reports

2. **Comparison Models** (`cdc-lib/Cdc/CdcComparisonModels.cs`)
   - `CompareCapturesRequest`: Request parameters
   - `CompareCapturesResponse`: Comparison results
   - `CaptureComparisonFailure`: Detailed failure information
   - `ComparisonSummary`: Statistics about the comparison

3. **API Endpoint** (`cdc-api/Controllers/CdcController.cs`)
   - `POST /api/cdc/compare`: HTTP endpoint for comparing captures

## Usage

### API Request

```http
POST /api/cdc/compare
Content-Type: application/json

{
  "baselineCaptureName": "original-procedure",
  "testCaptureName": "optimized-procedure",
  "fieldsToIgnore": ["created_date", "modified_date"],
  "ignoreLsnDifferences": true
}
```

### Request Parameters

- **baselineCaptureName** (required): Name of the baseline/expected capture (must be unique)
- **testCaptureName** (required): Name of the test capture to compare (must be unique)
- **fieldsToIgnore** (optional): List of field names to exclude from comparison (e.g., timestamps)
- **ignoreLsnDifferences** (optional): Whether to ignore LSN differences (default: true)

**Note**: Capture names must be unique across the system. The comparison matches captures by name only, not by session.

### Response

```json
{
  "isMatch": false,
  "failures": [
    {
      "tableName": "Orders",
      "failureType": "FieldMismatch",
      "primaryKey": "123",
      "fieldName": "total_amount",
      "baselineValue": 100.50,
      "testValue": 100.75,
      "description": "Field 'total_amount' mismatch for record '123' in table 'Orders'"
    }
  ],
  "summary": {
    "tablesCompared": 5,
    "recordsCompared": 1250,
    "fieldsCompared": 12500,
    "totalFailures": 1,
    "tablesWithFailures": 1,
    "comparisonDuration": "00:00:01.5"
  },
  "errors": []
}
```

## Comparison Process

### 1. Table-Level Comparison
- Identifies missing tables in test capture
- Identifies extra tables in test capture

### 2. Record-Level Comparison
- Matches records by primary key (`__$primary_key`)
- Detects missing records
- Detects extra records
- Compares record counts

### 3. Field-Level Comparison
- Compares CDC operation types (INSERT, UPDATE, DELETE)
- Compares all data fields (old_ and new_ prefixed)
- Respects field exclusion list
- Handles null and DBNull values correctly

## Failure Types

- **MissingTable**: Table exists in baseline but not in test
- **ExtraTable**: Table exists in test but not in baseline
- **MissingRecord**: Record exists in baseline but not in test
- **ExtraRecord**: Record exists in test but not in baseline
- **FieldMismatch**: Field value differs between baseline and test
- **OperationMismatch**: CDC operation type differs
- **RecordCountMismatch**: Different number of records in table

## Best Practices

### 1. Ignore Volatile Fields
Always exclude fields that naturally differ between runs:
```json
{
  "fieldsToIgnore": [
    "created_date",
    "modified_date",
    "last_updated",
    "timestamp"
  ]
}
```

### 2. Ignore LSN Differences
LSN (Log Sequence Number) values will always differ between captures. Keep `ignoreLsnDifferences: true` unless you specifically need to compare LSNs.

### 3. Use Descriptive Capture Names
Use clear, descriptive names that indicate what each capture represents:
- `baseline-original-sp`
- `test-optimized-sp-v1`
- `test-refactored-logic`

### 4. Review Failures Carefully
Not all failures indicate bugs:
- Some differences may be expected (e.g., auto-generated IDs)
- Timing differences may cause legitimate variations
- Review the failure descriptions to understand the context

## Example Workflow

### 1. Capture Baseline
```http
POST /api/cdc/start
{
  "sessionName": "optimization-test",
  "tablesToInclude": ["dbo.Orders", "dbo.OrderDetails"]
}

# Run original stored procedure
EXEC dbo.ProcessOrder @OrderId = 123

POST /api/cdc/stop
{
  "sessionName": "optimization-test",
  "captureName": "baseline-original",
  "captureType": "Baseline"
}
```

### 2. Capture Test
```http
POST /api/cdc/start
{
  "sessionName": "optimization-test",
  "tablesToInclude": ["dbo.Orders", "dbo.OrderDetails"]
}

# Run optimized stored procedure
EXEC dbo.ProcessOrder_Optimized @OrderId = 123

POST /api/cdc/stop
{
  "sessionName": "optimization-test",
  "captureName": "test-optimized",
  "captureType": "Test"
}
```

### 3. Compare Captures
```http
POST /api/cdc/compare
{
  "baselineCaptureName": "baseline-original",
  "testCaptureName": "test-optimized",
  "fieldsToIgnore": ["modified_date"]
}
```

## Testing

Comprehensive tests are available in:
- `cdc-api.Tests/Cdc/CdcCaptureComparerTests.cs`: Unit tests for the comparer
- `cdc-api.Tests/Controllers/CdcControllerTests.cs`: API endpoint tests

Run tests with:
```bash
dotnet test cdc-me.sln
```

## Performance Considerations

- Large captures with many records may take longer to compare
- The comparison is performed in-memory, so very large datasets may require significant memory
- Consider comparing subsets of tables for initial validation
- Use the `fieldsToIgnore` parameter to reduce comparison overhead

## Troubleshooting

### "Capture not found" Error
- Verify the capture names match exactly (case-sensitive)
- Check that both captures exist in the trace database
- Ensure capture names are unique (no duplicates with the same name)

### High Memory Usage
- Reduce the number of tables being compared
- Use table filtering in the start/stop CDC requests
- Consider comparing captures in batches

### Unexpected Failures
- Review the failure descriptions carefully
- Check if fields should be added to `fieldsToIgnore`
- Verify that test data is truly identical between runs
- Consider timing-related differences (e.g., auto-generated timestamps)

## See Also

- [CDC API Usage Guide](cdc-api-usage-guide.md)
- [Architecture Overview](architecture.md)
- [Getting Started](getting-started.md)