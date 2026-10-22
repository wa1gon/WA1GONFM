#!/usr/bin/env bash
set -euo pipefail

# Build, test, publish and install filepilot on Linux x64.
# Usage: ./scripts/build-install.sh [INSTALL_DIRECTORY]
# Default: /usr/local/bin/filepilot
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if (( $# > 1 )); then
    echo "Usage: $0 [INSTALL_DIRECTORY]" >&2
    exit 2
fi
INSTALL_DIR="${1:-/usr/local/bin}"
if [[ -z "$INSTALL_DIR" ]]; then
    echo "Install directory cannot be empty" >&2
    exit 2
fi
# Make the desktop entry independent of the working directory.
mkdir -p "$INSTALL_DIR" 2>/dev/null || true
if [[ -d "$INSTALL_DIR" ]]; then
    INSTALL_DIR="$(cd "$INSTALL_DIR" && pwd -P)"
else
    # Directory may need sudo to create; require an absolute path.
    if [[ "$INSTALL_DIR" != /* ]]; then
        echo "Custom installation directory must be absolute or already creatable" >&2
        exit 2
    fi
fi

cd "$ROOT_DIR"
echo "Building and testing filepilot..."
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build

echo "Publishing self-contained linux-x64 single-file executable..."
bash "$ROOT_DIR/scripts/publish-linux-x64.sh"
BINARY="$ROOT_DIR/dist/linux-x64/filepilot"
if [[ ! -f "$BINARY" ]]; then
    echo "Publish failed: missing $BINARY" >&2
    exit 1
fi

if [[ -d "$INSTALL_DIR" && -w "$INSTALL_DIR" ]]; then
    install -m 755 "$BINARY" "$INSTALL_DIR/filepilot"
else
    sudo install -d -m 755 "$INSTALL_DIR"
    sudo install -m 755 "$BINARY" "$INSTALL_DIR/filepilot"
fi
echo "Installed: $INSTALL_DIR/filepilot"

# Desktop integration for the invoking user, not root.
DATA_HOME="${XDG_DATA_HOME:-$HOME/.local/share}"
ICON_DIR="$DATA_HOME/icons/hicolor/256x256/apps"
DESKTOP_DIR="$DATA_HOME/applications"
ICON_SOURCE="$ROOT_DIR/src/WA1GONFM.App/Assets/filepilot-256.png"
ICON_DEST="$ICON_DIR/filepilot.png"
if [[ ! -f "$ICON_SOURCE" ]]; then
    echo "Missing application icon: $ICON_SOURCE" >&2
    exit 1
fi
install -d "$ICON_DIR" "$DESKTOP_DIR"
install -m 644 "$ICON_SOURCE" "$ICON_DEST"

cat > "$DESKTOP_DIR/filepilot.desktop" <<EOF_DESKTOP
[Desktop Entry]
Type=Application
Name=filepilot
Comment=File manager
Exec=$INSTALL_DIR/filepilot %U
Icon=$ICON_DEST
Terminal=false
Categories=System;FileTools;FileManager;
StartupNotify=true
StartupWMClass=filepilot
MimeType=inode/directory;
EOF_DESKTOP
chmod 644 "$DESKTOP_DIR/filepilot.desktop"

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$DESKTOP_DIR"
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -f -t "$DATA_HOME/icons/hicolor" >/dev/null 2>&1 || true
fi

echo "Installed desktop launcher: $DESKTOP_DIR/filepilot.desktop"
echo "Done. Run: filepilot (if the install directory is on your PATH)"
