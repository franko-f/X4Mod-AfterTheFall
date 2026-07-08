#!/usr/bin/env bash

# Uploads the generated "after the fall" mod to the Steam Workshop, updating the
# existing item ws_3306781107. Uses Egosoft's WorkshopTool.exe from "X Tools",
# run through wine - the same tool/pattern as extract_x4_data.sh.
#
# WorkshopTool reads the published-file id from the mod's content.xml
# (<content id="ws_3306781107" ...>), so `update` targets the right item
# automatically. Steam must be running and logged in while this runs.
#
#   Usage: ./upload_to_workshop.sh [-m <mod_dir>] [-n "<changenote>"]
#     -m, --mod         path to the generated mod (default: ./mod/after_the_fall)
#     -n, --changenote  workshop changelog text (default: version + today's date)
#
#   Override the Steam library with STEAM_COMMON_DIR=... if it isn't the default.

set -e

# ---- performance / wine tuning (mirrors extract_x4_data.sh) ----
export WINEFSYNC=1
export WINEDEBUG="-all"

# ---- configuration ----
DEFAULT_STEAM_COMMON="/home/franko/.local/share/Steam/steamapps/common"
STEAM_COMMON="${STEAM_COMMON_DIR:-$DEFAULT_STEAM_COMMON}"

X4_DIR="$STEAM_COMMON/X4 Foundations"
X_TOOLS_DIR="$STEAM_COMMON/X Tools"
X4_MOD_DIR="~/.config/EgoSoft/X4/22462428/extensions"

WORKSHOP_TOOL="$X_TOOLS_DIR/WorkshopTool.exe"

WORKSHOP_ID="ws_3306781107"   # must match <content id="..."> in the mod

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MOD_DIR="$X4_MOD_DIR/after_the_fall"
CHANGENOTE=""

# ---- command line parsing ----
usage() {
    echo "Usage: $0 [-m <mod_dir>] [-n \"<changenote>\"]"
    exit 1
}

while [[ "$#" -gt 0 ]]; do
    case "$1" in
        -m|--mod)        MOD_DIR="$2"; shift 2 ;;
        -n|--changenote) CHANGENOTE="$2"; shift 2 ;;
        -h|--help)       usage ;;
        *)               echo "Unknown option: $1"; usage ;;
    esac
done

MOD_DIR=$(realpath -m "$MOD_DIR")
CONTENT_XML="$MOD_DIR/content.xml"

# ---- validation ----
if [ ! -f "$WORKSHOP_TOOL" ]; then
    echo "Error: WorkshopTool.exe not found at: $WORKSHOP_TOOL"
    echo "       (set STEAM_COMMON_DIR if your Steam library is elsewhere)"
    exit 1
fi

if [ ! -f "$CONTENT_XML" ]; then
    echo "Error: no content.xml in mod folder: $MOD_DIR"
    echo "       Generate the mod first (dotnet run)."
    exit 1
fi

# The tool derives the target item from the content.xml id - make sure it's ours.
CONTENT_ID=$(grep -oE 'id="[^"]+"' "$CONTENT_XML" | head -1 | cut -d'"' -f2)
if [ "$CONTENT_ID" != "$WORKSHOP_ID" ]; then
    echo "Error: content.xml id is '$CONTENT_ID' but expected '$WORKSHOP_ID'."
    echo "       Refusing to upload to the wrong workshop item."
    exit 1
fi

# Warn (don't block) if Steam doesn't look like it's running.
if command -v pgrep > /dev/null && ! pgrep -x steam > /dev/null 2>&1; then
    echo "Warning: Steam client doesn't appear to be running. The upload will fail"
    echo "         unless Steam is running and logged in."
fi

# Default changenote from the mod's version + today's date.
if [ -z "$CHANGENOTE" ]; then
    VERSION=$(grep -oE 'version="[0-9]+"' "$CONTENT_XML" | head -1 | cut -d'"' -f2)
    CHANGENOTE="Update $(date +%Y-%m-%d)${VERSION:+ (version $VERSION)}"
fi

# ---- upload ----
echo "Uploading to Steam Workshop item: $WORKSHOP_ID"
echo "  mod folder : $MOD_DIR"
echo "  changenote : $CHANGENOTE"
echo

WIN_TOOL=$(winepath -w "$WORKSHOP_TOOL")
WIN_MOD=$(winepath -w "$MOD_DIR")

# -buildcat packs the folder into ext_01.cat/.dat for the workshop item.
# A preview image is optional on update; include it if one is present.
PREVIEW_ARGS=()
if [ -f "$MOD_DIR/preview.jpg" ]; then
    PREVIEW_ARGS=(-preview "$(winepath -w "$MOD_DIR/preview.jpg")")
elif [ -f "$MOD_DIR/preview.png" ]; then
    PREVIEW_ARGS=(-preview "$(winepath -w "$MOD_DIR/preview.png")")
fi

# WorkshopTool prompts "Start upload to the Steam cloud (y/n)?" - left interactive
# on purpose so the publish is a deliberate confirmation.
wine "$WIN_TOOL" update \
    -path "$WIN_MOD" \
    -buildcat \
    "${PREVIEW_ARGS[@]}" \
    -changenote "$CHANGENOTE"

echo
echo "Done. Verify at: https://steamcommunity.com/sharedfiles/filedetails/?id=${WORKSHOP_ID#ws_}"
