#!/bin/bash

# Coverage script for CDC Testing Framework
# This script runs tests with code coverage and generates reports
# It includes a clean step to avoid source-generated file warnings

set -e

echo "🧹 Cleaning build artifacts..."
dotnet clean cdc-me.sln --verbosity quiet

echo "🧪 Running tests with coverage..."
dotnet test cdc-api.Tests/ \
    --collect:"XPlat Code Coverage" \
    --results-directory:./TestResults \
    --verbosity quiet \
    --configuration Release

echo "📊 Generating coverage reports..."

# Find the latest coverage file
COVERAGE_FILE=$(find ./TestResults -name "coverage.cobertura.xml" -type f -exec ls -t {} + | head -n1)

if [ -z "$COVERAGE_FILE" ]; then
    echo "❌ No coverage file found!"
    exit 1
fi

echo "📄 Coverage file: $COVERAGE_FILE"

# Generate HTML report if reportgenerator is available
if command -v reportgenerator &> /dev/null; then
    echo "🌐 Generating HTML coverage report..."
    reportgenerator \
        -reports:"$COVERAGE_FILE" \
        -targetdir:"./TestResults/CoverageReport" \
        -reporttypes:"Html;TextSummary" \
        -filefilters:"-**/obj/**;-**/*.g.cs;-**/Microsoft.Extensions.Logging.Generators/**;-**/System.Text.Json.SourceGeneration/**;-**/bin/**" \
        -classfilters:"-System.*;-Microsoft.*" \
        -verbosity:Warning

    echo "✅ HTML coverage report generated at: ./TestResults/CoverageReport/index.html"

    # Display text summary
    if [ -f "./TestResults/CoverageReport/Summary.txt" ]; then
        echo ""
        echo "📈 Coverage Summary:"
        cat "./TestResults/CoverageReport/Summary.txt"
    fi
else
    echo "ℹ️  Install reportgenerator for HTML reports: dotnet tool install -g dotnet-reportgenerator-globaltool"
fi

echo ""
echo "✅ Coverage analysis complete!"
echo "📊 Cobertura XML: $COVERAGE_FILE"
echo ""
echo "💡 To view the HTML report, open: ./TestResults/CoverageReport/index.html"