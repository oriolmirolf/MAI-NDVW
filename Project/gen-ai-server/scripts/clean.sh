#!/bin/bash

# Clean script for gen-ai-server
# Removes cache, test outputs, generated files, and Unity cache

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

echo "Cleaning gen-ai-server..."
echo "Project directory: $PROJECT_DIR"
echo ""

# Remove cache contents but keep directory structure
if [ -d "$PROJECT_DIR/cache" ]; then
    echo "Clearing server cache..."
    # Remove all files but keep directories
    find "$PROJECT_DIR/cache" -type f -delete 2>/dev/null || true
    echo "  ✓ Cleared cache/ contents"
else
    # Create cache directories if they don't exist
    mkdir -p "$PROJECT_DIR/cache/narrative"
    mkdir -p "$PROJECT_DIR/cache/dungeon"
    mkdir -p "$PROJECT_DIR/cache/music"
    mkdir -p "$PROJECT_DIR/cache/vision"
    echo "  ✓ Created cache/ directories"
fi

# Remove test outputs
if [ -d "$PROJECT_DIR/tests/output" ]; then
    echo "Removing test outputs..."
    rm -rf "$PROJECT_DIR/tests/output"
    echo "  ✓ Removed tests/output/"
fi

# Remove generated audio files but keep directory structure
if [ -d "$PROJECT_DIR/output" ]; then
    echo "Clearing generated audio files..."
    find "$PROJECT_DIR/output" -type f -name "*.wav" -delete 2>/dev/null || true
    echo "  ✓ Cleared output/*.wav files"
else
    mkdir -p "$PROJECT_DIR/output/voice"
    echo "  ✓ Created output/ directories"
fi

# Remove temp uploads
if [ -d "$PROJECT_DIR/temp_uploads" ]; then
    echo "Removing temp uploads..."
    rm -rf "$PROJECT_DIR/temp_uploads"
    echo "  ✓ Removed temp_uploads/"
fi

# Unity cache paths (cross-platform)
echo ""
echo "Cleaning Unity cache..."

# Detect Unity persistent data path
if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "cygwin" || -n "$WSL_DISTRO_NAME" ]]; then
    # Windows or WSL - find AppData/LocalLow
    if [ -n "$WSL_DISTRO_NAME" ]; then
        # WSL - access Windows path
        WIN_USER=$(cmd.exe /c "echo %USERNAME%" 2>/dev/null | tr -d '\r')
        UNITY_CACHE_BASE="/mnt/c/Users/$WIN_USER/AppData/LocalLow"
    else
        # Git Bash / MSYS
        UNITY_CACHE_BASE="$LOCALAPPDATA/../LocalLow"
    fi
elif [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS
    UNITY_CACHE_BASE="$HOME/Library/Application Support"
else
    # Linux
    UNITY_CACHE_BASE="$HOME/.config/unity3d"
fi

# Unity project cache files
UNITY_CACHE_PATTERNS=(
    "DefaultCompany/2D Top Down Pixel Combat"
    "DefaultCompany/MAI-NDVW"
    "DefaultCompany/MAI_NDVW"
)

for pattern in "${UNITY_CACHE_PATTERNS[@]}"; do
    UNITY_CACHE_DIR="$UNITY_CACHE_BASE/$pattern"
    if [ -d "$UNITY_CACHE_DIR" ]; then
        echo "Found Unity cache: $UNITY_CACHE_DIR"

        # Remove narrative cache
        if [ -f "$UNITY_CACHE_DIR/narrative_cache.json" ]; then
            rm -f "$UNITY_CACHE_DIR/narrative_cache.json"
            echo "  ✓ Removed narrative_cache.json"
        fi

        # Remove dungeon content cache
        if [ -f "$UNITY_CACHE_DIR/dungeon_content.json" ]; then
            rm -f "$UNITY_CACHE_DIR/dungeon_content.json"
            echo "  ✓ Removed dungeon_content.json"
        fi

        # Remove voice cache folder
        if [ -d "$UNITY_CACHE_DIR/voice_cache" ]; then
            rm -rf "$UNITY_CACHE_DIR/voice_cache"
            echo "  ✓ Removed voice_cache/"
        fi
    fi
done

echo ""
echo "✓ Cleanup complete!"
