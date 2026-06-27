#!/bin/bash

# Benchmark script for C# Class Context Analyzer
# This script runs the benchmark and captures the results

set -e

echo "=== C# Class Context Analyzer Benchmark Script ==="
echo ""

# Parse command line arguments
CLASS_COUNT=${1:-100}
ITERATIONS=${2:-2}
OUTPUT_DIR=${3:-./benchmark-results}

echo "Configuration:"
echo "  Class count: $CLASS_COUNT"
echo "  Iterations: $ITERATIONS"
echo "  Output directory: $OUTPUT_DIR"
echo ""

# Build the project
echo "Step 1: Building project..."
dotnet build ClassContextAnalyzer.csproj

# Run benchmark
echo ""
echo "Step 2: Running benchmark..."
dotnet run --project ClassContextAnalyzer.csproj -- --class-count $CLASS_COUNT --iterations $ITERATIONS --output $OUTPUT_DIR

# Display results
echo ""
echo "=== Benchmark Complete ==="

if [ -f "$OUTPUT_DIR/benchmark_report_"*.md ]; then
    LATEST_REPORT=$(ls -t "$OUTPUT_DIR/benchmark_report_"*.md | head -1)
    echo ""
    echo "Latest report:"
    cat "$LATEST_REPORT"
else
    echo "No benchmark report found"
fi