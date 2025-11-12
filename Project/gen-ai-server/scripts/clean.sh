#!/bin/bash

# Clean script for gen-ai-server
# Removes cache, test outputs, and generated files

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

echo "Cleaning gen-ai-server..."
echo "Project directory: $PROJECT_DIR"
echo ""

# Remove cache
if [ -d "$PROJECT_DIR/cache" ]; then
    echo "Removing cache..."
    rm -rf "$PROJECT_DIR/cache"
    echo "  ✓ Removed cache/"
fi

# Remove test outputs
if [ -d "$PROJECT_DIR/tests/output" ]; then
    echo "Removing test outputs..."
    rm -rf "$PROJECT_DIR/tests/output"
    echo "  ✓ Removed tests/output/"
fi

# Remove main output (generated music)
if [ -d "$PROJECT_DIR/output" ]; then
    echo "Removing generated music files..."
    rm -rf "$PROJECT_DIR/output"/*.wav 2>/dev/null || true
    echo "  ✓ Removed output/*.wav"
fi

# Remove temp uploads
if [ -d "$PROJECT_DIR/temp_uploads" ]; then
    echo "Removing temp uploads..."
    rm -rf "$PROJECT_DIR/temp_uploads"
    echo "  ✓ Removed temp_uploads/"
fi

echo ""
echo "✓ Cleanup complete!"
