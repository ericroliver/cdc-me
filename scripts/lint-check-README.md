# Local Lint Check Script

This document describes the local lint check script that replicates the CI/CD pipeline's linting and static analysis steps.

## Overview

The `lint-check.sh` script performs comprehensive code quality checks locally before pushing to the repository, helping you catch issues early and avoid CI/CD pipeline failures.

## Usage

```bash
# Make the script executable (first time only)
chmod +x scripts/lint-check.sh

# Run the lint check
./scripts/lint-check.sh
```

## What It Checks

The script performs the following checks in order:

### 1. **Environment Validation**

- ✅ Verifies .NET SDK is installed and accessible
- ✅ Checks for the solution file (`cdc-me.sln`)

### 2. **Build Process**

- 🧹 Cleans previous build artifacts
- 📦 Restores NuGet dependencies
- 🔧 Restores .NET tools (if configured)

### 3. **Code Quality Checks**

- 🏗️ **Build with warnings as errors** - Ensures code compiles cleanly
- 🎨 **Code formatting** - Runs `dotnet format --verify-no-changes`
- 🔍 **Static analysis** - Runs .NET analyzers for code quality issues
- ⚠️ **Nullable reference warnings** - Specifically checks for CS8602 and related warnings

### 4. **Security & Maintenance**

- 🛡️ **Vulnerable packages** - Scans for known security vulnerabilities (warning only)
- 📅 **Outdated packages** - Checks for package updates (if dotnet-outdated is installed)

## Exit Codes

- **0**: All checks passed successfully
- **1**: One or more critical checks failed

## Handling Warnings vs Errors

The script distinguishes between critical errors and warnings:

### Critical Errors (Will fail the script):

- Build compilation errors
- Code formatting issues
- CS8602 nullable reference warnings
- Critical static analysis issues

### Warnings (Won't fail the script):

- Package version compatibility warnings (e.g., "doesn't support net6.0")
- Vulnerable packages (reported but non-blocking)
- Missing optional tools

## Common Issues and Solutions

### Package Version Warnings

If you see warnings like "System.Text.Json 10.0.0 doesn't support net6.0":

- These are non-critical and won't block the build
- Consider upgrading to .NET 8.0+ for better compatibility
- Or add `<SuppressTfmSupportBuildWarnings>true</SuppressTfmSupportBuildWarnings>` to project files

### Code Formatting Issues

If formatting checks fail:

```bash
# Fix formatting automatically
dotnet format cdc-me.sln
```

### Nullable Reference Warnings (CS8602)

If you get CS8602 warnings:

1. Add null checks: `if (variable != null)`
2. Use null-conditional operators: `variable?.Property`
3. Use null-forgiving operator (carefully): `variable!`
4. Initialize variables properly

### Vulnerable Packages

If vulnerable packages are detected:

```bash
# Update specific packages
dotnet add package <PackageName> --version <LatestVersion>

# Or update all packages
dotnet list package --outdated
```

## Integration with CI/CD

This script replicates the same checks performed in the CI/CD pipeline:

- Same build configuration (Release)
- Same static analysis rules
- Same formatting requirements
- Same vulnerability scanning

Running this script locally ensures your code will pass the CI/CD pipeline checks.

## Optional Tools

### dotnet-outdated

Install for enhanced package update checking:

```bash
dotnet tool install -g dotnet-outdated-tool
```

### reportgenerator

For enhanced coverage reports (used by coverage.sh):

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

## Troubleshooting

### Script Permission Issues

```bash
chmod +x scripts/lint-check.sh
```

### .NET SDK Issues

Ensure .NET 6.0 SDK or later is installed:

```bash
dotnet --version
```

### Build Issues

If builds fail unexpectedly:

```bash
# Clean and restore manually
dotnet clean cdc-me.sln
dotnet restore cdc-me.sln
dotnet build cdc-me.sln --configuration Release
```

## Related Scripts

- `coverage.sh` - Runs tests with code coverage analysis
- `lint-check.sh` - This script (comprehensive code quality checks)

## Contributing

When modifying this script:

1. Test with both passing and failing scenarios
2. Ensure error messages are clear and actionable
3. Keep the script aligned with CI/CD pipeline requirements
4. Update this documentation if adding new checks
