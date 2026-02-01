# API Tester

YAML-driven API test harness for cdc-me.

## Quick Start

```bash
# Install dependencies
npm install

# Build
npm run build

# Run tests
npx api-tester tests/*.yaml
```

## Test File Format

```yaml
name: Create Snapshot
description: Optional description

request:
  method: POST
  url: /api/snapshots
  headers:
    Content-Type: application/json
  body:
    databaseName: testdb
    snapshotName: snap_001

expect:
  status: [200, 201]           # Single or array of valid statuses
  headers:
    content-type: application/json
  body:
    equals: { success: true }  # Exact match
    contains:                   # String contains
      - snapshotId
    notContains:                # String does not contain
      - error
    jsonPath:                   # JSONPath assertions
      $.success: true
      $.data.id: 123
    schema:                     # JSON Schema validation
      type: object
      required: [id, name]

# Capture values from response into environment
capture:
  SNAPSHOT_ID: "$.snapshotId"
```

## CLI Options

```bash
api-tester [options] <test-pattern>

Options:
  -b, --base-url <url>    Base URL for API requests (default: http://localhost:8080)
  -e, --env <key=value>   Set environment variable (repeatable)
  -v, --verbose           Show response bodies on failure
  -h, --help              Show help
```

## Variable Interpolation

Use `${VAR}` or `${VAR:-default}` syntax in YAML:

```yaml
request:
  url: /api/snapshots/${SNAPSHOT_ID}
  body:
    name: "${DB_NAME:-testdb}"
```

Variables are resolved from:
1. Captured values from previous tests
2. `-e` CLI arguments
3. Process environment variables

## Features

- ✅ Status code assertions (single or multiple valid codes)
- ✅ Header assertions
- ✅ Body exact match
- ✅ Body string contains/notContains
- ✅ JSONPath assertions
- ✅ JSON Schema validation
- ✅ Variable capture from responses
- ✅ Environment variable interpolation
- ✅ Skip tests with `skip: true`
