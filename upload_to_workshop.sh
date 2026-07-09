#!/usr/bin/env bash

# Uploads the generated "after the fall" mod to the Steam Workshop, updating the
# existing item ws_3306781107. Uses Egosoft's WorkshopTool.exe from "X Tools".
#
# Unlike XRCatTool (an offline archiver that runs fine under bare wine),
# WorkshopTool links the Steamworks SDK and must reach the running Steam client.
# Bare wine has no bridge to native Linux Steam ("Steam is not running"), while
# `proton run` has the bridge but breaks console I/O (silent hang at the y/n
# prompt). The working combination is PROTON'S BUNDLED WINE run directly against
# X4's Proton prefix (steamapps/compatdata/392160/pfx): the prefix carries the
# lsteamclient bridge, and invoking wine directly keeps normal terminal I/O.
#
# WorkshopTool reads the published-file id from the mod's content.xml
# (<content id="ws_3306781107" ...>), so `update` targets the right item
# automatically. Steam must be running and logged in while this runs.
#
#   Usage: ./upload_to_workshop.sh [-m <mod_dir>] [-n "<changenote>"]
#     -m, --mod         path to the generated mod (default: the installed
#                       extensions/after_the_fall under the X4 user directory)
#     -n, --changenote  workshop changelog text (default: version + today's date)
#
#   Environment overrides:
#     STEAM_COMMON_DIR  Steam library "common" dir (X4 Foundations / X Tools live here)
#     STEAM_ROOT        Steam install root (default: ~/.local/share/Steam) - used to
#                       find X4's Proton prefix (steamapps/compatdata/392160)
#     PROTON_DIR        path to a specific proton binary; default is newest Proton*
#                       installed. Set this if X4 is pinned to a particular Proton.

set -e

export WINEDEBUG="-all"   # suppress wine's fixme/err noise

# ---- configuration ----
DEFAULT_STEAM_COMMON="/home/franko/.local/share/Steam/steamapps/common"
STEAM_COMMON="${STEAM_COMMON_DIR:-$DEFAULT_STEAM_COMMON}"

X_TOOLS_DIR="$STEAM_COMMON/X Tools"
X4_MOD_DIR="$HOME/.config/EgoSoft/X4/22462428/extensions"

WORKSHOP_TOOL="$X_TOOLS_DIR/WorkshopTool.exe"

WORKSHOP_ID="ws_3306781107"   # must match <content id="..."> in the mod

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

# Warn (don't block) if Steam doesn't look like it's running. The live client is
# often 'steamwebhelper' (or launched via steam.sh) rather than a bare 'steam',
# so check a few known names before warning.
if command -v pgrep > /dev/null \
   && ! pgrep -x steam > /dev/null 2>&1 \
   && ! pgrep -x steamwebhelper > /dev/null 2>&1 \
   && ! pgrep -f steam.sh > /dev/null 2>&1; then
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

# Run Proton's bundled wine directly against X4's Proton prefix (see header): the
# prefix supplies the Steam bridge, direct wine keeps console I/O intact.
STEAM_ROOT="${STEAM_ROOT:-$HOME/.local/share/Steam}"
export WINEPREFIX="$STEAM_ROOT/steamapps/compatdata/392160/pfx"

if [ ! -d "$WINEPREFIX" ]; then
    echo "Error: X4's Proton prefix not found at: $WINEPREFIX"
    echo "       Launch X4 through this Steam at least once so the prefix exists,"
    echo "       or set STEAM_ROOT to the right Steam install."
    exit 1
fi

# Locate Proton only to borrow its bundled wine: honour $PROTON_DIR, else the
# newest Proton* installed. tail keeps the pipeline exit 0 when the glob matches
# nothing (set -e safe).
PROTON="${PROTON_DIR:-$(ls -d "$STEAM_ROOT"/steamapps/common/Proton*/proton 2>/dev/null | sort -V | tail -1)}"
if [ ! -x "$PROTON" ]; then
    echo "Error: Proton not found under $STEAM_ROOT/steamapps/common/Proton*"
    echo "       (set PROTON_DIR=/path/to/Proton.../proton)"
    exit 1
fi
WINE="$(dirname "$PROTON")/files/bin/wine"
if [ ! -x "$WINE" ]; then
    echo "Error: Proton's bundled wine not found at: $WINE"
    exit 1
fi

# WorkshopTool is a Windows program: -path must be a Windows path, or its parser
# reads a leading "/" as the start of another switch ("Parameter missing for
# switch 'path'"). Z:\ maps to / in the prefix, so translate directly.
to_win() { printf 'Z:%s' "${1//\//\\}"; }
WIN_MOD=$(to_win "$MOD_DIR")

# -buildcat packs the folder into ext_01.cat/.dat for the workshop item.
# A preview image is optional on update; include it if one is present.
PREVIEW_ARGS=()
if [ -f "$MOD_DIR/preview.jpg" ]; then
    PREVIEW_ARGS=(-preview "$(to_win "$MOD_DIR/preview.jpg")")
elif [ -f "$MOD_DIR/preview.png" ]; then
    PREVIEW_ARGS=(-preview "$(to_win "$MOD_DIR/preview.png")")
fi

echo "  wine       : $WINE"
echo "  prefix     : $WINEPREFIX"
echo

# Run from the X Tools folder: Steamworks reads the app id from the shipped
# steam_appid.txt in the CURRENT WORKING DIRECTORY (without it the tool fails
# with "Failed to initialize Steamworks").
cd "$X_TOOLS_DIR"

# WorkshopTool prompts "Start upload to the Steam cloud (y/n)?" - answer it on
# stdin so the script is non-interactive (running it IS the confirmation). Tee the
# output so you see it live AND keep a copy. set -e can't see past the pipe (tee is
# the pipeline's exit), so read the tool's real status from PIPESTATUS.
UPLOAD_LOG="${UPLOAD_LOG:-$HOME/x4-workshop-upload.log}"
printf 'y\n' | "$WINE" WorkshopTool.exe update \
    -path "$WIN_MOD" \
    -buildcat \
    "${PREVIEW_ARGS[@]}" \
    -changenote "$CHANGENOTE" 2>&1 | tee "$UPLOAD_LOG"
UPLOAD_RC=${PIPESTATUS[1]}

echo
if [ "${UPLOAD_RC:-1}" -ne 0 ]; then
    echo "WorkshopTool exited with status $UPLOAD_RC - upload likely did NOT happen."
    echo "  tool output : $UPLOAD_LOG"
    exit "$UPLOAD_RC"
fi

echo "Done. Verify at: https://steamcommunity.com/sharedfiles/filedetails/?id=${WORKSHOP_ID#ws_}"
