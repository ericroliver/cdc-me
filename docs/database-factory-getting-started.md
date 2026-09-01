# Database Factory — Getting Started (Field Install)

This guide walks through the **first real-world use** of the DTAI Database Factory at a field install. You have a `database.bak` file and want to turn it into a provisioned database.

---

## What You Need

| Item | Description |
|------|-------------|
| DTAI API running | The `cdc-api` service must be up and reachable (e.g. `http://localhost:8080`) |
| SQL Server instance | A reachable SQL Server (2016+) with SA credentials or a login with `dbcreator` server role |
| Your `database.bak` file | The backup file you want to use as a template |

---

## The Flow

```
Register Connection → Register Template → Place Order → Database is Built
```

Three API calls. That's it. The factory handles restore + hydration + registry automatically.

---

## Step 1: Register a Connection

A **Connection** is a named, registered SQL Server instance. Everything in the factory references connections by ID — no raw connection strings scattered around.

```bash
curl -X POST http://localhost:8080/api/factory/connections \
  -H "Content-Type: application/json" \
  -d '{
    "name": "field-sqlserver",
    "platform": "SqlServer",
    "host": "sqlserver",
    "port": 1433,
    "connectionString": "Server=sqlserver;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;",
    "description": "SQL Server at the field site",
    "isDefault": true
  }'
```

**What happens:**
- DTAI creates a connection record in its PostgreSQL metadata store
- `isDefault` is optional — you can set it to `true` if you want orders to auto-resolve this connection when no `targetConnectionId` is specified, but in most cases you'll pass the connection ID explicitly

**Response:**
```json
{
  "id": "a1b2c3d4-...",
  "name": "field-sqlserver",
  "platform": "SqlServer",
  "host": "sqlserver",
  "port": 1433,
  "isDefault": true,
  ...
}
```

> **Save the `id`** — you'll pass it as `targetConnectionId` when placing orders.

### Test the Connection (optional but recommended)

```bash
curl -X POST http://localhost:8080/api/factory/connections/{id}/test
```

Returns `{ "success": true, "message": "Connection successful" }` if the SQL Server is reachable.

---

## Step 2: Register Your Template

A **Template** is a database backup file registered in DTAI as a starting point. There are two ways to get your `database.bak` in:

### Option A: Upload via API

Use this if you have the `.bak` file on your local machine and want to push it to the DTAI templates volume:

```bash
curl -X POST http://localhost:8080/api/factory/templates/upload \
  -F "file=@/path/to/database.bak" \
  -F "name=my-first-template" \
  -F "version=1.0.0" \
  -F "platform=SqlServer" \
  -F "description=First field install template"
```

DTAI saves the file to its templates volume and creates the metadata record.

### Option B: Register by Path (recommended for field installs)

Use this if the `.bak` file is already on the server (e.g. copied to the templates volume by automation, or already on the SQL Server host):

1. **Copy the file to the templates volume** (inside the DTAI container):

```bash
# If DTAI is running in Docker, copy the .bak into the container's templates path:
docker cp /path/to/database.bak cdc-api:/app/templates/database.bak
```

2. **Register it by path:**

```bash
curl -X POST http://localhost:8080/api/factory/templates \
  -H "Content-Type: application/json" \
  -d '{
    "name": "my-first-template",
    "version": "1.0.0",
    "platform": "SqlServer",
    "filePath": "/app/templates/database.bak",
    "description": "First field install template"
  }'
```

**Response:**
```json
{
  "id": "e5f6g7h8-...",
  "name": "my-first-template",
  "version": "1.0.0",
  "platform": "SqlServer",
  "filePath": "/app/templates/database.bak",
  ...
}
```

> **Save the `id`** — you need it for the order.

### Verify the Template (optional)

```bash
curl -X POST http://localhost:8080/api/factory/templates/{id}/verify
```

Returns `{ "success": true, "message": "File verified successfully" }` if the file exists and the checksum matches.

---

## Step 3: Place an Order

An **Order** tells the factory: "restore this template to that connection, as this database name, and run these script groups." For your first database, you can skip the script groups — just restore the backup.

Pass the connection name from Step 1 so the factory knows exactly which SQL Server to target. You can use the connection `id` or the `name` — whichever is more convenient:

**By name (recommended for field work):**

```bash
curl -X POST http://localhost:8080/api/factory/orders \
  -H "Content-Type: application/json" \
  -d '{
    "templateId": "e5f6g7h8-...",
    "targetConnectionName": "field-sqlserver",
    "targetDatabaseName": "my_first_database",
    "scriptGroupIds": [],
    "parameters": {}
  }'
```

**By ID:**

```bash
curl -X POST http://localhost:8080/api/factory/orders \
  -H "Content-Type: application/json" \
  -d '{
    "templateId": "e5f6g7h8-...",
    "targetConnectionId": "a1b2c3d4-...",
    "targetDatabaseName": "my_first_database",
    "scriptGroupIds": [],
    "parameters": {}
  }'
```

If both `targetConnectionId` and `targetConnectionName` are provided, the ID takes precedence. If neither is provided, the factory falls back to the default connection (if one is set).

**What happens (automatically):**
1. **Resolve** — resolves the connection by ID (if provided), then by name, then falls back to default
2. **Validate** — checks template exists, platform matches connection
3. **Restore** — runs `RESTORE DATABASE [my_first_database] FROM DISK = '...'` on the SQL Server
4. **Hydrate** — no scripts to run (empty `scriptGroupIds`), skips this step
5. **Deliver** — records the provisioned database in the registry, marks order as `Delivered`

**Response:**
```json
{
  "id": "i9j0k1l2-...",
  "templateId": "e5f6g7h8-...",
  "targetDatabaseName": "my_first_database",
  "status": "Delivered",
  "createdAt": "2026-09-01T18:27:00Z",
  "completedAt": "2026-09-01T18:28:15Z"
}
```

> The order is processed **synchronously** — the response comes back with the final status. For large restores, this may take a few minutes.

---

## Step 4: Verify

### Check the Order Status

```bash
curl http://localhost:8080/api/factory/orders/{orderId}/status
```

### List All Provisioned Databases

```bash
curl http://localhost:8080/api/factory/databases
```

Returns:
```json
[
  {
    "id": "...",
    "databaseName": "my_first_database",
    "connectionId": "a1b2c3d4-...",
    "templateId": "e5f6g7h8-...",
    "status": "Active",
    "createdAt": "2026-09-01T18:28:15Z"
  }
]
```

### Connect to the Database Directly

Your database is now live on the SQL Server. Connect with any SQL client:

```sql
-- Using sqlcmd
sqlcmd -S sqlserver -U sa -P YourStrong@Passw0rd -C -Q "USE my_first_database; SELECT name FROM sys.tables"
```

---

## What's Next?

You now have a working database provisioned from a template. From here you can:

1. **Add hydration scripts** — create script groups and SQL scripts that configure the database after restore (e.g. set up branches, load customer data, enable features)
2. **Parameterize orders** — pass parameters like `BranchCount`, `Industry`, `CompanyName` that get substituted into your SQL scripts
3. **Layer your scripts** — organize hydration into layers (0 = base, 1 = company config, 2 = modules, etc.) with dependency tracking
4. **Template the database name** — use variables like `{companyName}_{industry}_v{templateVersion}_{date}` in your order's `targetDatabaseName`

See the [Database Factory Design Document](./database-factory-design.md) for the full architecture and capabilities.

---

## Quick Reference

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/factory/connections` | `POST` | Register a SQL Server connection |
| `/api/factory/connections/{id}/test` | `POST` | Ping a connection |
| `/api/factory/templates/upload` | `POST` | Upload a .bak file + register |
| `/api/factory/templates` | `POST` | Register a file already on the volume |
| `/api/factory/templates` | `GET` | List all templates |
| `/api/factory/templates/{id}/verify` | `POST` | Verify file integrity |
| `/api/factory/orders` | `POST` | Place an order (provision a database) |
| `/api/factory/orders/{id}/status` | `GET` | Check order status |
| `/api/factory/databases` | `GET` | List all provisioned databases |

---

## Troubleshooting

### "No default connection is set"

You omitted `targetConnectionId` from the order but no connection has `isDefault: true`. Either pass `targetConnectionId` explicitly in the order (the recommended approach), or register/re-register a connection with `isDefault: true`.

### "Platform mismatch: template platform 'SqlServer' does not match connection platform '...'"

Your template and connection must be on the same platform. Both should be `SqlServer` for `.bak` files.

### "File not found at path: ..."

The file path doesn't exist on the DTAI container's filesystem. Either upload the file via the `/upload` endpoint, or `docker cp` the file into the container's templates volume first.

### Restore fails

- The SQL Server must have permission to read the backup file from the path specified
- If the backup file is on the host but SQL Server is in a container, the path must be one that SQL Server can see (typically a mounted volume)
- Check the order's `errorMessage` field for details

### "RESTORE DATABASE" permission denied

The connection's SQL login needs `dbcreator` server role or `CREATE DATABASE` permission. The `sa` account always works.
