#!/usr/bin/env bash
set -euo pipefail

# Usage: ./scripts/build-install.sh [INSTALL_DIRECTORY]
# Default destination: /usr/local/bin/filepilot
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
INSTALL_DIR="${1:-/usr/local/bin}"

if [[ $# -gt 1 ]]; then
    echo "Usage: $0 [INSTALL_DIRECTORY]" >&2
    exit 2
fi
if [[ -z "$INSTALL_DIR" ]]; then
    echo "Install directory cannot be empty" >&2
    exit 2
fi

cd "$ROOT_DIR"
echo "Building and testing filepilot..."
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build

echo "Publishing self-contained linux-x64 single-file executable..."
"$ROOT_DIR/scripts/publish-linux-x64.sh"
BINARY="$ROOT_DIR/dist/linux-x64/filepilot"
if [[ ! -f "$BINARY" ]]; then
    echo "Publish failed: missing $BINARY" >&2
    exit 1
fi

# Only elevate the installation operation, never the .NET build.
if [[ -d "$INSTALL_DIR" && -w "$INSTALL_DIR" ]]; then
    install -m 755 "$BINARY" "$INSTALL_DIR/filepilot"
elif [[ ! -e "$INSTALL_DIR" || -d "$INSTALL_DIR" ]]; then
    sudo install -d -m 755 "$INSTALL_DIR"
    sudo install -m 755 "$BINARY" "$INSTALL_DIR/filepilot"
else
    echo "Not a directory: $INSTALL_DIR" >&2
    exit 1
fi

echo "Installed filepilot to $INSTALL_DIR/filepilot"
