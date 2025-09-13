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

    # Preserve baseline coverage report for regression tracking
    echo "💾 Preserving baseline coverage report..."
    mkdir -p ./coverage-baseline
    cp "$COVERAGE_FILE" ./coverage-baseline/coverage.cobertura.xml
    cp "./TestResults/CoverageReport/Summary.txt" ./coverage-baseline/Summary.txt 2>/dev/null || true
    
    # Display text summary
    if [ -f "./TestResults/CoverageReport/Summary.txt" ]; then
        echo ""
        echo "📈 Coverage Summary:"
        cat "./TestResults/CoverageReport/Summary.txt"
    fi
    
    # Compare with baseline if it exists
    if [ -f "./coverage-baseline/Summary.txt" ] && [ -f "./TestResults/CoverageReport/Summary.txt" ]; then
        echo ""
        echo "🔍 Comparing with baseline coverage..."
        
        # Extract line coverage percentages
        CURRENT_COVERAGE=$(grep "Line coverage:" "./TestResults/CoverageReport/Summary.txt" | grep -o '[0-9.]*%' | head -1 | sed 's/%//')
        BASELINE_COVERAGE=$(grep "Line coverage:" "./coverage-baseline/Summary.txt" | grep -o '[0-9.]*%' | head -1 | sed 's/%//')
        
        if [ ! -z "$CURRENT_COVERAGE" ] && [ ! -z "$BASELINE_COVERAGE" ]; then
            DIFF=$(echo "$CURRENT_COVERAGE - $BASELINE_COVERAGE" | bc -l 2>/dev/null || echo "0")
            
            if (( $(echo "$DIFF > 0" | bc -l 2>/dev/null || echo "0") )); then
                echo "📈 Coverage improved by ${DIFF}% (${BASELINE_COVERAGE}% → ${CURRENT_COVERAGE}%)"
            elif (( $(echo "$DIFF < 0" | bc -l 2>/dev/null || echo "0") )); then
                echo "⚠️  Coverage decreased by ${DIFF#-}% (${BASELINE_COVERAGE}% → ${CURRENT_COVERAGE}%)"
            else
                echo "➡️  Coverage unchanged at ${CURRENT_COVERAGE}%"
            fi
        fi
    fi
else
    echo "ℹ️  Install reportgenerator for HTML reports: dotnet tool install -g dotnet-reportgenerator-globaltool"
fi

echo ""
echo "✅ Coverage analysis complete!"
echo "📊 Cobertura XML: $COVERAGE_FILE"
echo "💾 Baseline coverage: ./coverage-baseline/coverage.cobertura.xml"
echo ""
echo "💡 To view the HTML report, open: ./TestResults/CoverageReport/index.html"