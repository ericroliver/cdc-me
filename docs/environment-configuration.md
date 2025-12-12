# Environment Variable Configuration

This document describes how to configure the CDC Testing Framework using environment variables for secure credential management.

## Overview

As part of security improvements, hard-coded database credentials have been removed from configuration files. The application now uses environment variables for sensitive configuration data.

## Required Environment Variables

### Database Connections

| Variable              | Description                                              | Example                                                                                             |
| --------------------- | -------------------------------------------------------- | --------------------------------------------------------------------------------------------------- |
| `TEST_DB_CONNECTION`  | SQL Server connection string for the database under test | `Server=myserver;Database=testdb;User Id=testuser;Password=mypassword;TrustServerCertificate=true;` |
| `CDCME_DB_CONNECTION` | PostgreSQL connection string for trace data storage      | `Host=myhost;Database=cdcme;Username=cdcuser;Password=mypassword`                                   |

### Optional Provider Overrides

| Variable            | Description                         | Default      | Valid Values |
| ------------------- | ----------------------------------- | ------------ | ------------ |
| `TEST_DB_PROVIDER`  | Database provider for test database | `SqlServer`  | `SqlServer`  |
| `CDCME_DB_PROVIDER` | Database provider for CDC storage   | `PostgreSQL` | `PostgreSQL` |

## Configuration Methods

### 1. Environment Variables (Production)

Set environment variables in your deployment environment:

**Linux/macOS:**

```bash
export TEST_DB_CONNECTION="Server=prod-sql;Database=testdb;User Id=cdcuser;Password=secure_password;TrustServerCertificate=true;"
export CDCME_DB_CONNECTION="Host=prod-postgres;Database=cdcme;Username=cdcuser;Password=secure_password"
```

**Windows:**

```cmd
set TEST_DB_CONNECTION=Server=prod-sql;Database=testdb;User Id=cdcuser;Password=secure_password;TrustServerCertificate=true;
set CDCME_DB_CONNECTION=Host=prod-postgres;Database=cdcme;Username=cdcuser;Password=secure_password
```

**PowerShell:**

```powershell
$env:TEST_DB_CONNECTION="Server=prod-sql;Database=testdb;User Id=cdcuser;Password=secure_password;TrustServerCertificate=true;"
$env:CDCME_DB_CONNECTION="Host=prod-postgres;Database=cdcme;Username=cdcuser;Password=secure_password"
```

### 2. .env File (Development)

For local development, create a `.env` file in the project root:

```bash
# Copy .env.example to .env and fill in your values
cp .env.example .env
```

Edit `.env`:

```bash
# TEST_DB - SQL Server database under test (CDC/traces enabled)
TEST_DB_CONNECTION=Server=localhost;Database=cdctest;User Id=sa;Password=YourPassword123!;TrustServerCertificate=true;

# CDCME_DB - PostgreSQL database for storing trace data and CDC snapshots
CDCME_DB_CONNECTION=Host=localhost;Database=cdcme;Username=postgres;Password=YourPassword123!

# Optional: Override database types (defaults shown)
TEST_DB_PROVIDER=SqlServer
CDCME_DB_PROVIDER=PostgreSQL
```

**Important:** Never commit `.env` files to source control. The `.env` file is already in `.gitignore`.

### 3. Docker Environment

**docker-compose.yml:**

```yaml
version: "3.8"
services:
  cdc-api:
    build: .
    environment:
      - TEST_DB_CONNECTION=Server=sqlserver;Database=testdb;User Id=sa;Password=YourPassword123!;TrustServerCertificate=true;
      - CDCME_DB_CONNECTION=Host=postgres;Database=cdcme;Username=postgres;Password=YourPassword123!
    depends_on:
      - sqlserver
      - postgres
```

**Docker run:**

```bash
docker run -e TEST_DB_CONNECTION="Server=host.docker.internal;Database=testdb;User Id=sa;Password=YourPassword123!;TrustServerCertificate=true;" \
           -e CDCME_DB_CONNECTION="Host=host.docker.internal;Database=cdcme;Username=postgres;Password=YourPassword123!" \
           cdc-api
```

### 4. Kubernetes Secrets

**secret.yaml:**

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: cdc-db-secrets
type: Opaque
stringData:
  TEST_DB_CONNECTION: "Server=sql-server-service;Database=testdb;User Id=cdcuser;Password=secure_password;TrustServerCertificate=true;"
  CDCME_DB_CONNECTION: "Host=postgres-service;Database=cdcme;Username=cdcuser;Password=secure_password"
```

**deployment.yaml:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: cdc-api
spec:
  template:
    spec:
      containers:
        - name: cdc-api
          image: cdc-api:latest
          envFrom:
            - secretRef:
                name: cdc-db-secrets
```

## Connection String Formats

### SQL Server (TEST_DB_CONNECTION)

```
Server=hostname;Database=dbname;User Id=username;Password=password;TrustServerCertificate=true;
```

**Parameters:**

- `Server`: SQL Server hostname or IP
- `Database`: Database name
- `User Id`: SQL Server username
- `Password`: SQL Server password
- `TrustServerCertificate`: Set to `true` for development/self-signed certificates

### PostgreSQL (CDCME_DB_CONNECTION)

```
Host=hostname;Database=dbname;Username=username;Password=password
```

**Parameters:**

- `Host`: PostgreSQL hostname or IP
- `Database`: Database name
- `Username`: PostgreSQL username
- `Password`: PostgreSQL password

## Security Best Practices

1. **Never commit credentials to source control**
2. **Use different credentials for each environment**
3. **Rotate passwords regularly**
4. **Use least-privilege database accounts**
5. **Enable SSL/TLS for database connections in production**
6. **Use managed identity or service accounts when available**

## Troubleshooting

### Connection Issues

1. **Check environment variables are set:**

   ```bash
   echo $TEST_DB_CONNECTION
   echo $CDCME_DB_CONNECTION
   ```

2. **Verify connection strings format**
3. **Test database connectivity manually**
4. **Check firewall and network access**

### Common Errors

**"Connection string is empty"**

- Environment variable not set or empty
- Check variable name spelling

**"Login failed"**

- Incorrect username/password
- Database user doesn't exist
- Insufficient permissions

**"Server not found"**

- Incorrect hostname/IP
- Network connectivity issues
- Database server not running

## Migration from Hard-coded Configuration

If upgrading from a version with hard-coded credentials:

1. **Backup your current configuration**
2. **Set environment variables with your database credentials**
3. **Remove any hard-coded credentials from config files**
4. **Test the application with new configuration**
5. **Update deployment scripts/documentation**

## Development Setup

For new developers:

1. **Copy the example environment file:**

   ```bash
   cp .env.example .env
   ```

2. **Fill in your local database credentials in `.env`**

3. **Ensure databases are running and accessible**

4. **Run the application:**
   ```bash
   dotnet run --project cdc-api
   ```

The application will automatically load environment variables from the `.env` file during development.
