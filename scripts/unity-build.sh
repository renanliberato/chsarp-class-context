#!/bin/bash

set -e

# Unity WebGL Fixture Build Script
# Builds a Unity project for WebGL without manual editor interaction

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="${1:?Usage: $0 <project_directory>}"
BUILD_DIR="${2:-$PROJECT_DIR/Build}"

echo "=== Unity WebGL Fixture: Build ==="
echo "Project directory: $PROJECT_DIR"
echo "Build output: $BUILD_DIR"
echo ""

if [ ! -d "$PROJECT_DIR" ]; then
    echo "Error: Project directory not found: $PROJECT_DIR"
    exit 1
fi

# Run build via dotnet
dotnet run --project "$SCRIPT_DIR/../UnityFixture/UnityFixture.csproj" -- build "$PROJECT_DIR" "$BUILD_DIR"

echo ""
echo "Build complete. WebGL build at: $BUILD_DIR"
echo ""
echo "Next step:"
echo "  Serve: ./scripts/unity-serve.sh $BUILD_DIR"