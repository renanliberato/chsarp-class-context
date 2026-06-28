#!/bin/bash

set -e

# Unity WebGL Fixture Serve Script
# Serves a WebGL build locally for testing

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="${1:?Usage: $0 <build_directory>}"
PORT="${2:-8080}"

echo "=== Unity WebGL Fixture: Serve ==="
echo "Build directory: $BUILD_DIR"
echo "Port: $PORT"
echo ""

if [ ! -d "$BUILD_DIR" ]; then
    echo "Error: Build directory not found: $BUILD_DIR"
    exit 1
fi

# Run serve via dotnet
dotnet run --project "$SCRIPT_DIR/../UnityFixture/UnityFixture.csproj" -- serve "$BUILD_DIR" $PORT

echo ""
echo "Serving stopped."