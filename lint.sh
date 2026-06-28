#!/bin/bash
# Run CovenantCheck linter on this repository, excluding generated files (bin/, obj/)

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COVENANTCHECK_PATH="../CovenantCheck/CovenantCheck"

# Check if CovenantCheck exists
if [ ! -d "$COVENANTCHECK_PATH" ]; then
    echo "Error: CovenantCheck not found at $COVENANTCHECK_PATH"
    echo "Please clone CovenantCheck to ../CovenantCheck"
    exit 1
fi

echo "Running CovenantCheck on $SCRIPT_DIR (excluding bin/, obj/)..."

# Create temporary directory for source files
TEMP_DIR=$(mktemp -d)
trap "rm -rf $TEMP_DIR" EXIT

# Copy all .cs files except from bin/ and obj/ directories
find "$SCRIPT_DIR" -name "*.cs" -not -path "*/bin/*" -not -path "*/obj/*" -not -path "*/.robot/*" | while read -r file; do
    # Create directory structure in temp
    rel_path="${file#$SCRIPT_DIR/}"
    temp_file="$TEMP_DIR/$rel_path"
    mkdir -p "$(dirname "$temp_file")"
    cp "$file" "$temp_file"
done

# Run CovenantCheck on the temp directory
dotnet run --project "$COVENANTCHECK_PATH" -- "$TEMP_DIR"