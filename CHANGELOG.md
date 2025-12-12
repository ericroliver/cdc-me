# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Environment variable configuration support for database connections
- Transaction wrapping for CDC capture operations
- ModelState validation for all API endpoints
- Comprehensive implementation plan for reviewer feedback

### Changed

- **BREAKING**: DELETE `/api/snapshot` endpoint now uses route parameter instead of JSON body
  - Old: `DELETE /api/snapshot` with `{"SnapshotName": "name"}` in body
  - New: `DELETE /api/snapshot/{snapshotName}`
- **BREAKING**: SnapshotManager restore behavior now fails if target database doesn't exist (instead of creating it)
- Removed hard-coded credentials from appsettings.json - now uses environment variables
- Centralized Extended Events session name generation logic
- Improved error messages in SnapshotManager operations

### Fixed

- Column index mismatch in SqlServerTraceProvider after TestConnectionString removal
- Duplicate session_id in Extended Events ACTION lists
- Incorrect error message in SnapshotManager.DropSnapshotAsync
- Hard-coded "TestDatabase" value replaced with actual database name
- Event data extraction logic in TraceManager (statement vs sql_text)

### Security

- Removed hard-coded database credentials from configuration files
- Added environment variable-based configuration for sensitive data

### Deprecated

- None (clean breaking changes were acceptable for this development phase)

## Migration Guide

### API Endpoint Changes

If you're using the DELETE snapshot endpoint, update your client code:

```javascript
// Old approach
fetch("/api/snapshot", {
  method: "DELETE",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ SnapshotName: "my-snapshot" }),
});

// New approach
fetch("/api/snapshot/my-snapshot", {
  method: "DELETE",
});
```

### Configuration Changes

Update your deployment configuration to use environment variables:

1. Copy `.env.example` to `.env` and fill in your values
2. Set environment variables in your deployment environment:
   - `TEST_DB_CONNECTION`: SQL Server connection string
   - `CDCME_DB_CONNECTION`: PostgreSQL connection string

### Database Restore Behavior

The SnapshotManager.RestoreSnapshotAsync method now requires the target database to exist before restoration. If you relied on automatic database creation, ensure target databases exist before calling restore operations.
