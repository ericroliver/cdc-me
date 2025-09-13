# Code Coverage

This document describes how to run code coverage analysis for the CDC Testing Framework project.

## Overview

The project uses .NET's built-in code coverage tools with the XPlat Code Coverage collector and ReportGenerator for HTML report generation.

## Prerequisites

1. Install the ReportGenerator global tool (optional, but recommended for HTML reports):
   ```bash
   dotnet tool install -g dotnet-reportgenerator-globaltool
   ```

## Running Coverage Analysis

### Using the Coverage Script

The easiest way to run coverage analysis is using the provided script:

```bash
./scripts/coverage.sh
```

This script will:

1. Clean build artifacts
2. Run all tests with coverage collection
3. Generate HTML and text summary reports
4. Display coverage statistics

### Manual Coverage Run

You can also run coverage manually:

```bash
# Clean the solution
dotnet clean cdc-me.sln

# Run tests with coverage
dotnet test cdc-api.Tests/ \
    --collect:"XPlat Code Coverage" \
    --results-directory:./TestResults \
    --configuration Release

# Generate HTML report (if reportgenerator is installed)
reportgenerator \
    -reports:"./TestResults/*/coverage.cobertura.xml" \
    -targetdir:"./TestResults/CoverageReport" \
    -reporttypes:"Html;TextSummary"
```

## Coverage Reports

### HTML Report

After running coverage analysis, an HTML report is generated at:

```
./TestResults/CoverageReport/index.html
```

This report provides:

- Line-by-line coverage visualization
- Branch coverage details
- Method coverage statistics
- Interactive filtering and navigation

### Text Summary

A text summary is also generated at:

```
./TestResults/CoverageReport/Summary.txt
```

### Cobertura XML

The raw coverage data is stored in Cobertura XML format:

```
./TestResults/{guid}/coverage.cobertura.xml
```

## Coverage Metrics

The coverage analysis tracks:

- **Line Coverage**: Percentage of executable lines that were executed
- **Branch Coverage**: Percentage of decision branches that were taken
- **Method Coverage**: Percentage of methods that were called

## Current Coverage Status

As of the last run:

- **Overall Line Coverage**: 24.5% (822/3349 lines)
- **Branch Coverage**: 12.2% (124/1016 branches)
- **Method Coverage**: 42.4% (232/547 methods)

### Project Breakdown

- **cdc-api**: 76.4% line coverage
- **cdc-lib**: 7.4% line coverage

## Improving Coverage

To improve code coverage:

1. **Add Unit Tests**: Focus on the `cdc-lib` project which has low coverage
2. **Integration Tests**: Add more comprehensive integration tests
3. **Edge Cases**: Test error conditions and edge cases
4. **Mock Dependencies**: Use mocking to isolate units under test

## CI/CD Integration

The coverage script can be integrated into CI/CD pipelines:

```yaml
# Example GitHub Actions step
- name: Run Code Coverage
  run: ./scripts/coverage.sh

- name: Upload Coverage Reports
  uses: actions/upload-artifact@v3
  with:
    name: coverage-report
    path: TestResults/CoverageReport/
```

## Troubleshooting

### No Coverage File Found

If you see "❌ No coverage file found!", ensure:

1. Tests are running successfully
2. The `coverlet.collector` package is installed in test projects
3. The `--collect:"XPlat Code Coverage"` parameter is used

### ReportGenerator Not Found

If ReportGenerator is not installed:

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

### Permission Issues

Make sure the coverage script is executable:

```bash
chmod +x scripts/coverage.sh
```
