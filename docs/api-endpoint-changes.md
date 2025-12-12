# API Endpoint Changes

This document describes the breaking changes made to API endpoints as part of the reviewer feedback implementation.

## DELETE Snapshot Endpoint

### Previous Implementation

```
DELETE /api/snapshot
Content-Type: application/json

{
  "SnapshotName": "my-snapshot"
}
```

### New Implementation

```
DELETE /api/snapshot/{snapshotName}
```

### Migration Guide

**Before:**

```javascript
// Old approach - JSON body
const response = await fetch("/api/snapshot", {
  method: "DELETE",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ SnapshotName: "my-snapshot" }),
});
```

**After:**

```javascript
// New approach - Route parameter
const response = await fetch("/api/snapshot/my-snapshot", {
  method: "DELETE",
});
```

### Benefits of the Change

1. **REST Compliance**: DELETE endpoints should not require request bodies
2. **Client Compatibility**: Many HTTP clients have issues with DELETE requests containing bodies
3. **Simplicity**: Route parameters are more straightforward than JSON payloads for simple operations
4. **Caching**: URL-based parameters work better with HTTP caching mechanisms

### Response Format

The response format remains unchanged:

```json
{
  "success": true,
  "message": "Successfully dropped snapshot my-snapshot",
  "snapshotName": "my-snapshot",
  "deletedAt": "2024-01-15T10:30:00Z"
}
```

### Error Handling

- **400 Bad Request**: If snapshot name is empty or whitespace
- **404 Not Found**: If snapshot doesn't exist
- **500 Internal Server Error**: If deletion fails due to database issues

### Testing

Update your integration tests to use the new endpoint format:

```csharp
// Old test approach
var request = new DeleteSnapshotRequest { SnapshotName = "TestSnapshot" };
var response = await client.PostAsync("/api/snapshot", JsonContent.Create(request));

// New test approach
var response = await client.DeleteAsync("/api/snapshot/TestSnapshot");
```
