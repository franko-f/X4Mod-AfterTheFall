#!/usr/bin/env bash

# Exit immediately if a command exits with a non-zero status
set -e

# Start the global execution timer
GLOBAL_START=$SECONDS

# ==============================================================================
# PERFORMANCE OPTIMISATIONS
# ==============================================================================
export WINEFSYNC=1
export WINEDEBUG="-all"

# ==============================================================================
# CONFIGURATION & BLACKLIST
# ==============================================================================
DEFAULT_STEAM_COMMON="/home/franko/.local/share/Steam/steamapps/common"
STEAM_COMMON="${STEAM_COMMON_DIR:-$DEFAULT_STEAM_COMMON}"

X4_DIR="$STEAM_COMMON/X4 Foundations"
X_TOOLS_DIR="$STEAM_COMMON/X Tools"
CAT_TOOL="$X_TOOLS_DIR/XRCatTool.exe"

# Directories to delete during cleanup
DIR_BLACKLIST=(
    "assets/textures"
    "assets/cutscenes"
    "assets/environments"
#    "assets/fx"
    "assets/legacy"
    "assets/characters"
    "assets/cutscenecore"

    "textures"
    "particles"
    "sfx"
    "voice-l044"
    "voice-l049"
    "sound"
    "music"
    "movies"
    "cutscenes"
    "shadergl"
)

# ==============================================================================
# COMMAND LINE PARSING
# ==============================================================================
CLEANUP_ONLY=false
TARGET_DIR=""

usage() {
    echo "Usage: $0 [-c|--cleanup-only] <target_output_directory>"
    exit 1
}

# Parse options
while [[ "$#" -gt 0 ]]; do
    case "$1" in
        -c|--cleanup-only)
            CLEANUP_ONLY=true
            shift
            ;;
        -*)
            echo "Unknown option: $1"
            usage
            ;;
        *)
            if [ -z "$TARGET_DIR" ]; then
                TARGET_DIR="$1"
            else
                echo "Error: Multiple target directories specified."
                usage
            fi
            shift
            ;;
    esac
done

# Ensure a target output path was provided
if [ -z "$TARGET_DIR" ]; then
    echo "Error: Missing target output directory."
    usage
fi

# Resolve absolute path for the target output directory
TARGET_DIR=$(realpath -m "$TARGET_DIR")

# ==============================================================================
# HELPER FUNCTIONS
# ==============================================================================
format_time() {
    local total_seconds=$1
    local minutes=$((total_seconds / 60))
    local seconds=$((total_seconds % 60))
    if [ "$minutes" -gt 0 ]; then
        echo "${minutes}m ${seconds}s"
    else
        echo "${seconds}s"
    fi
}

cleanup_blacklisted_dirs() {
    local target_folder="$1"
    for trash_dir in "${DIR_BLACKLIST[@]}"; do
        if [ -d "$target_folder/$trash_dir" ]; then
            echo "  [Cleanup] Removing excluded directory: $trash_dir"
            rm -rf "$target_folder/$trash_dir"
        fi
    done
}

# ==============================================================================
# MODE: CLEANUP ONLY
# ==============================================================================
if [ "$CLEANUP_ONLY" = true ]; then
    echo "--- Mode: Cleanup Only ---"
    if [ ! -d "$TARGET_DIR" ]; then
        echo "Error: Target directory does not exist: $TARGET_DIR"
        exit 1
    fi

    echo "Cleaning base game data inside: $TARGET_DIR"
    cleanup_blacklisted_dirs "$TARGET_DIR"

    # Search for and clean extension subdirectories if they exist
    TARGET_EXT_DIR="$TARGET_DIR/extensions"
    if [ -d "$TARGET_EXT_DIR" ]; then
        echo "Scanning extensions for cleanup..."
        for ext_sub in "$TARGET_EXT_DIR"/*/; do
            if [ -d "$ext_sub" ]; then
                echo "Cleaning extension: extensions/$(basename "$ext_sub")"
                cleanup_blacklisted_dirs "$ext_sub"
            fi
        done
    fi

    GLOBAL_DURATION=$((SECONDS - GLOBAL_START))
    echo "--- Cleanup Complete! ---"
    echo "Total Execution Time: $(format_time $GLOBAL_DURATION)"
    exit 0
fi

# ==============================================================================
# VALIDATION (Only required for full extraction mode)
# ==============================================================================
if [ ! -d "$X4_DIR" ]; then
    echo "Error: X4 Foundations directory not found at: $X4_DIR"
    exit 1
fi

if [ ! -f "$CAT_TOOL" ]; then
    echo "Error: XRCatTool.exe not found at: $CAT_TOOL"
    exit 1
fi

WIN_CAT_TOOL=$(winepath -w "$CAT_TOOL")

# ==============================================================================
# PHASE 1: EXTRACT BASE GAME CATALOGS
# ==============================================================================
echo "Creating target directory: $TARGET_DIR"
mkdir -p "$TARGET_DIR"

if compgen -G "$X4_DIR"/*.cat > /dev/null; then
    echo "--- Extracting Base Game Catalogs ---"
    WIN_TARGET_DIR=$(winepath -w "$TARGET_DIR")

    for catfile in "$X4_DIR"/*.cat; do
        CAT_NAME=$(basename "$catfile")
        echo "Processing base file: $CAT_NAME"

        CAT_START=$SECONDS
        WIN_CAT_FILE=$(winepath -w "$catfile")

        wine "$WIN_CAT_TOOL" -in "$WIN_CAT_FILE" -out "$WIN_TARGET_DIR"

        # Instantly run cleanup for this base output folder
        cleanup_blacklisted_dirs "$TARGET_DIR"

        CAT_DURATION=$((SECONDS - CAT_START))
        echo "  [Time taken for $CAT_NAME: $(format_time $CAT_DURATION)]"
    done
else
    echo "Warning: No .cat files found directly in $X4_DIR"
fi

# ==============================================================================
# PHASE 2: EXTRACT EXTENSIONS
# ==============================================================================
X4_EXT_DIR="$X4_DIR/extensions"

if [ -d "$X4_EXT_DIR" ]; then
    echo "--- Scanning Extensions Directory ---"

    for ext_sub_dir in "$X4_EXT_DIR"/*/; do
        EXT_NAME=$(basename "$ext_sub_dir")

        # Only official Egosoft DLC (ego_*) belong in a clean vanilla unpack. Skip
        # installed mods - our own "after_the_fall", subscribed "ws_*" workshop
        # items, etc. - so they can't contaminate the extracted data. The
        # verification harness treats everything under extensions/ as stock game +
        # DLC, so a mod leaking in there makes every mod selector miss or collide.
        case "$EXT_NAME" in
            ego*) ;;
            *)
                echo "Skipping non-official extension: extensions/$EXT_NAME"
                continue
                ;;
        esac

        if compgen -G "$ext_sub_dir"/*.cat > /dev/null; then
            echo "Processing extension: extensions/$EXT_NAME"
            SUBDIR_START=$SECONDS

            TARGET_EXT_SUBDIR="$TARGET_DIR/extensions/$EXT_NAME"
            mkdir -p "$TARGET_EXT_SUBDIR"
            WIN_TARGET_EXT_SUBDIR=$(winepath -w "$TARGET_EXT_SUBDIR")

            for ext_cat in "$ext_sub_dir"/*.cat; do
                echo "  -> Extracting: $(basename "$ext_cat")"
                WIN_EXT_CAT=$(winepath -w "$ext_cat")

                wine "$WIN_CAT_TOOL" -in "$WIN_EXT_CAT" -out "$WIN_TARGET_EXT_SUBDIR"
            done

            # Instantly clean up the specific extensions folder right after its cats process
            cleanup_blacklisted_dirs "$TARGET_EXT_SUBDIR"

            SUBDIR_DURATION=$((SECONDS - SUBDIR_START))
            echo "  [Time taken for extensions/$EXT_NAME: $(format_time $SUBDIR_DURATION)]"
        fi
    done
else
    echo "No extensions subdirectory found inside X4 Foundations. Skipping."
fi

# ==============================================================================
# GLOBAL TIMER OUTPUT
# ==============================================================================
GLOBAL_DURATION=$((SECONDS - GLOBAL_START))

echo "--- Extraction Process Complete! ---"
echo "Data fully unpacked into: $TARGET_DIR"
echo "Total Execution Time: $(format_time $GLOBAL_DURATION)"
