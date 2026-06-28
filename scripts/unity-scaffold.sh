#!/bin/bash

set -e

# Unity WebGL Fixture Scaffold Script
# Creates a minimal Unity project for extractor validation

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT_DIR="${1:-./UnityFixture}"

echo "=== Unity WebGL Fixture: Scaffold ==="
echo "Output directory: $OUTPUT_DIR"
echo ""

# Run scaffolding via dotnet
dotnet run --project "$SCRIPT_DIR/UnityFixture/UnityFixture.csproj" -- scaffold "$OUTPUT_DIR"

echo ""
echo "Scaffold complete. Unity project created at: $OUTPUT_DIR"
echo ""
echo "Next steps:"
echo "  1. (Optional) Review generated C# files in $OUTPUT_DIR/Assets/"
echo "  2. Build: ./scripts/unity-build.sh $OUTPUT_DIR"
echo "  3. Serve: ./scripts/unity-serve.sh $OUTPUT_DIR/Build"