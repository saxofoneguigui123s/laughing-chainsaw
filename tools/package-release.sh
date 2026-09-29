#!/usr/bin/env bash
#
# Packages the Nightfall Survival Expansion U3 SDK tree into the release zip.
#
# Usage:  tools/package-release.sh [version]        (default: 3.26.3.12.2)
#         OUT_DIR=/some/path tools/package-release.sh
#
# The archive is built deterministically: every entry gets the same fixed
# timestamp, entries are written in sorted order, and extra fields are stripped.
# The same source tree therefore always produces the same sha256, which is
# published next to the asset so downloads can be verified.
#
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${1:-3.26.3.12.2}"
ASSET="U3-SDK-Nightfall-Survival-Expansion-v${VERSION}.zip"
OUT_DIR="${OUT_DIR:-$REPO_ROOT/dist}"
STAMP="${STAMP:-2000-01-01 00:00:00 UTC}"

if [ ! -d "$REPO_ROOT/U3-SDK" ]; then
	echo "error: $REPO_ROOT/U3-SDK not found" >&2
	exit 1
fi

mkdir -p "$OUT_DIR"
rm -f "$OUT_DIR/$ASSET" "$OUT_DIR/$ASSET.sha256"

# Unity-generated folders and OS junk must never ship.
EXCLUDES=(
	"U3-SDK/Library/*"
	"U3-SDK/Temp/*"
	"U3-SDK/obj/*"
	"U3-SDK/Logs/*"
	"U3-SDK/UserSettings/*"
	"U3-SDK/MemoryCaptures/*"
	"*/__MACOSX/*"
	"*.DS_Store"
	"*Thumbs.db"
)

# Fixed timestamps keep the archive byte-identical between machines.
find "$REPO_ROOT/U3-SDK" -print0 | xargs -0 touch -h -d "$STAMP"

cd "$REPO_ROOT"
find U3-SDK -print | LC_ALL=C sort | zip -q -9 -X -@ "$OUT_DIR/$ASSET" -x "${EXCLUDES[@]}"

# Fail loudly if the archive is damaged.
unzip -tq "$OUT_DIR/$ASSET" > /dev/null

SHA="$(sha256sum "$OUT_DIR/$ASSET" | cut -d' ' -f1)"
printf '%s  %s\n' "$SHA" "$ASSET" > "$OUT_DIR/$ASSET.sha256"

echo "asset:  $ASSET"
echo "path:   $OUT_DIR/$ASSET"
echo "bytes:  $(stat -c %s "$OUT_DIR/$ASSET")"
echo "sha256: $SHA"
echo "entries: $(unzip -Z1 "$OUT_DIR/$ASSET" | wc -l)"
