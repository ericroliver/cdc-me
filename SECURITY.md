# Security Policy

## Supported Versions

The CDC Testing Framework is currently in active development. Security updates will be provided for the following versions:

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a Vulnerability

We take the security of the CDC Testing Framework seriously. If you discover a security vulnerability, please report it responsibly.

### How to Report

**DO NOT** create a public GitHub issue for security vulnerabilities.

Instead, please report security vulnerabilities by:

1. **Email**: Send details to eric.oliver@widowmakersoftware.com
2. **Subject Line**: "SECURITY: CDC Testing Framework Vulnerability Report"
3. **Include**:
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
   - Suggested fix (if available)

### What to Expect

- **Acknowledgment**: Within 48 hours of submission
- **Initial Assessment**: Within 5 business days
- **Status Updates**: Every 7 days until resolved
- **Fix Timeline**: Critical issues within 30 days, others within 90 days
- **Credit**: We will acknowledge your contribution (unless you prefer to remain anonymous)

## Security Best Practices

### For Deployment

#### 1. Environment Variables & Secrets

**CRITICAL**: Never commit credentials to version control.

- Use environment variables for all sensitive configuration
- Store credentials in secure secret management systems:
  - Azure Key Vault
  - AWS Secrets Manager
  - HashiCorp Vault
  - Docker Secrets (for containerized deployments)

**Setup**:
```bash
# Copy the example file
cp .env.example cdc-api/.env

# Edit with your actual credentials
# NEVER commit the .env file
```

#### 2. Database Connections

**Development**:
```bash
# Use TrustServerCertificate=true ONLY in development
TEST_DB_CONNECTION="Server=localhost;Database=test;User Id=testuser;Password=<SECURE_PASSWORD>;TrustServerCertificate=true;"
```

**Production**:
```bash
# Always use proper SSL/TLS certificate validation
TEST_DB_CONNECTION="Server=prod.example.com;Database=test;User Id=testuser;Password=<SECURE_PASSWORD>;Encrypt=true;TrustServerCertificate=false;"
```

**⚠️ WARNING**: Using `TrustServerCertificate=true` in production exposes you to man-in-the-middle attacks.

#### 3. API Authentication

The API currently **does not** include built-in authentication. Before deploying to production:

**Option 1: API Gateway** (Recommended for production)
- Deploy behind an API gateway (e.g., Azure API Management, AWS API Gateway)
- Configure authentication at the gateway level
- Use JWT tokens or OAuth 2.0

**Option 2: Network-Level Security**
- Deploy on a private network
- Use VPN or bastion host for access
- Implement firewall rules

**Option 3: API Key Authentication** (Minimum requirement)
```csharp
// Future implementation - not yet available
// Configure in appsettings.json:
{
  "Authentication": {
    "ApiKey": {
      "Enabled": true,
      "ValidateKeyHeader": "X-API-Key"
    }
  }
}
```

#### 4. CORS Configuration

**Development**:
```bash
ASPNETCORE_ENVIRONMENT=Development
# Uses permissive CORS policy - suitable only for local development
```

**Production**:
```bash
ASPNETCORE_ENVIRONMENT=Production
CORS_ALLOWED_ORIGINS="https://your-app.example.com,https://admin.example.com"
```

**⚠️ WARNING**: Never use `AllowAnyOrigin()` in production environments.

#### 5. Docker Security

When deploying with Docker:

```bash
# Use Docker secrets for credentials
docker secret create db_connection_test your_test_db_connection.txt
docker secret create db_connection_cdcme your_cdcme_db_connection.txt

# Reference in docker-compose.yml:
services:
  cdc-api:
    secrets:
      - db_connection_test
      - db_connection_cdcme
    environment:
      TEST_DB_CONNECTION_FILE: /run/secrets/db_connection_test
      CDCME_DB_CONNECTION_FILE: /run/secrets/db_connection_cdcme
```

### For Development

#### 1. Credential Management

- **Never** hardcode credentials in source code
- **Never** commit `.env` files (already in `.gitignore`)
- Use strong, unique passwords for each environment
- Rotate credentials regularly

#### 2. SQL Injection Prevention

The framework includes robust SQL injection protection via [`SqlIdentifierValidator`](cdc-lib/Utilities/SqlIdentifierValidator.cs):

```csharp
// Always use the validator for dynamic SQL identifiers
var validatedName = SqlIdentifierValidator.ValidateIdentifier(userInput, "table name");
var escapedName = SqlIdentifierValidator.EscapeIdentifier(validatedName);
```

**✅ DO**:
- Use parameterized queries
- Validate and escape all SQL identifiers
- Use the built-in validation utilities

**❌ DON'T**:
- Concatenate user input into SQL queries
- Trust input from external sources
- Bypass the identifier validation

#### 3. Error Handling

The API sanitizes error messages to prevent information disclosure:

```csharp
// ✅ GOOD - Generic client message, detailed server logs
catch (Exception ex)
{
    _logger.LogError(ex, "Detailed error for server logs");
    return BadRequest(new { error = "Operation failed. Check server logs." });
}

// ❌ BAD - Exposes internal details
catch (Exception ex)
{
    return BadRequest(new { error = ex.Message });
}
```

#### 4. File Path Validation

When handling file paths from user input:

```csharp
// File path validation is built into JsonHandler
// Prevents path traversal attacks like ../../etc/passwd
```

## Known Security Limitations

### Current Limitations (v1.0)

1. **No Built-in Authentication**: API endpoints are not authenticated
   - **Mitigation**: Deploy on private networks or behind authenticated API gateway
   
2. **No Rate Limiting**: API can be overwhelmed with requests
   - **Mitigation**: Implement rate limiting at reverse proxy or API gateway level
   
3. **No Audit Logging**: User actions are not tracked
   - **Mitigation**: Enable detailed application logging and ship to SIEM

4. **Database Credentials Required**: Service account needs elevated permissions
   - **Mitigation**: Use dedicated service accounts with minimum required permissions
   
5. **No Encryption at Rest**: CDC data stored unencrypted
   - **Mitigation**: Use database-level encryption features (TDE for SQL Server)

### Planned Security Enhancements (Future Versions)

- [ ] Built-in API key authentication
- [ ] Role-based access control (RBAC)
- [ ] Rate limiting middleware
- [ ] Audit logging
- [ ] Secrets encryption in configuration
- [ ] Support for managed identities (Azure, AWS)
- [ ] Security scanning in CI/CD pipeline

## Security Checklist for Deployment

Before deploying to production:

### Pre-Deployment
- [ ] All credentials stored in secret management system
- [ ] No `.env` files in version control
- [ ] `TrustServerCertificate=false` in all production connection strings
- [ ] CORS configured with specific allowed origins
- [ ] API deployed behind authentication layer
- [ ] Network security groups/firewall rules configured
- [ ] SSL/TLS certificates valid and properly configured

### Configuration
- [ ] `ASPNETCORE_ENVIRONMENT=Production` set
- [ ] `CORS_ALLOWED_ORIGINS` configured with actual domains
- [ ] Database connection strings use encrypted connections
- [ ] Service accounts use minimum required permissions
- [ ] Logging configured to exclude sensitive data

### Monitoring
- [ ] Error logging enabled and monitored
- [ ] Security events logged
- [ ] Failed authentication attempts logged (when auth implemented)
- [ ] Anomalous activity alerts configured

### Post-Deployment
- [ ] Security scan performed
- [ ] Penetration testing completed (for sensitive environments)
- [ ] Incident response plan documented
- [ ] Regular security updates scheduled

## Compliance Considerations

### Data Protection

- **Data Classification**: Determine sensitivity of captured CDC data
- **Retention Policies**: Configure appropriate data retention
- **Access Controls**: Implement need-to-know access principles
- **Encryption**: Enable encryption in transit and at rest

### Regulatory Compliance

Depending on your use case, consider:

- **GDPR**: Personal data handling requirements
- **HIPAA**: Healthcare data protection (if applicable)
- **SOC 2**: Security controls for service organizations
- **PCI DSS**: Payment card data security (if applicable)

## Security Update Policy

- **Critical Vulnerabilities**: Patched within 48-72 hours
- **High Severity**: Patched within 7 days
- **Medium Severity**: Patched within 30 days
- **Low Severity**: Included in next regular release

## Additional Resources

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [Microsoft Security Best Practices](https://docs.microsoft.com/en-us/security/)
- [Docker Security Best Practices](https://docs.docker.com/develop/security-best-practices/)

## Contact

For security-related questions or concerns:
- **Security Issues**: [security contact - UPDATE THIS]
- **General Questions**: Create a GitHub discussion

---

**Last Updated**: 2026-01-20  
**Document Version**: 1.0
