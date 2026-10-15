#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
RID="${1:-linux-x64}"
case "$RID" in win-x64|linux-x64|osx-x64|osx-arm64) ;; *) echo "Unsupported RID: $RID" >&2; exit 2;; esac
PUBLISH="out/publish-$RID"
BUNDLE="out/PhotoSafeCheck-$RID-portable"
if [[ -e "$PUBLISH" || -e "$BUNDLE" ]]; then
 echo "Refusing to overwrite an existing build directory. Use a fresh output location." >&2; exit 1
fi
dotnet publish PhotoSafeCheck/PhotoSafeCheck.csproj -c Release -r "$RID" --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$PUBLISH"
python3 scripts/package_preview.py --publish "$PUBLISH" --dest "$BUNDLE" --rid "$RID"
echo "Portable bundle ready: $BUNDLE"
