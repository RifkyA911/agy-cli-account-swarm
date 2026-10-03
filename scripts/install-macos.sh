#!/usr/bin/env bash
# ==============================================================================
# Installer for Agy CLI Account Swarm (macOS)
# Version: 0.9.8-beta
# ==============================================================================
set -e

GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${CYAN}============================================================${NC}"
echo -e "${CYAN}    Agy CLI Account Swarm — macOS Installer (v0.9.8-beta)    ${NC}"
echo -e "${CYAN}============================================================${NC}"

APP_NAME="Agy CLI Account Swarm"
if [ "$(id -u)" -eq 0 ]; then
    APP_DIR="/Applications/$APP_NAME.app"
    BIN_DIR="/usr/local/bin"
else
    APP_DIR="$HOME/Applications/$APP_NAME.app"
    BIN_DIR="$HOME/.local/bin"
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PAYLOAD_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

echo -e "${YELLOW}[1/4] Preparing macOS App Bundle structure at $APP_DIR...${NC}"
mkdir -p "$APP_DIR/Contents/MacOS"
mkdir -p "$APP_DIR/Contents/Resources"
mkdir -p "$BIN_DIR"

echo -e "${YELLOW}[2/4] Copying application payload...${NC}"
if [ -d "$PAYLOAD_DIR/publish" ]; then
    cp -r "$PAYLOAD_DIR/publish"/* "$APP_DIR/Contents/Resources/"
elif [ -d "$PAYLOAD_DIR/bundle" ]; then
    cp -r "$PAYLOAD_DIR/bundle"/* "$APP_DIR/Contents/Resources/"
else
    cp -r "$PAYLOAD_DIR"/* "$APP_DIR/Contents/Resources/" 2>/dev/null || true
fi

# Copy uninstaller into app bundle
cp "$SCRIPT_DIR/uninstall-macos.sh" "$APP_DIR/Contents/Resources/uninstall.sh"
chmod +x "$APP_DIR/Contents/Resources/uninstall.sh"

echo -e "${YELLOW}[3/4] Generating macOS Info.plist & Launchers...${NC}"
cat << EOF > "$APP_DIR/Contents/Info.plist"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>agy-cli-account-swarm</string>
    <key>CFBundleIdentifier</key>
    <string>com.rifkya911.agy-cli-account-swarm</string>
    <key>CFBundleName</key>
    <string>Agy CLI Account Swarm</string>
    <key>CFBundleVersion</key>
    <string>0.9.8-beta</string>
    <key>CFBundleShortVersionString</key>
    <string>0.9.8-beta</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
</dict>
</plist>
EOF

cat << 'EOF' > "$APP_DIR/Contents/MacOS/agy-cli-account-swarm"
#!/usr/bin/env bash
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RES_DIR="$(cd "$SCRIPT_DIR/../Resources" && pwd)"

# Native .NET 9 Avalonia execution on macOS (Metal / Cocoa)
if [ -x "$RES_DIR/AgyCliAccountSwarmGUI" ]; then
    exec "$RES_DIR/AgyCliAccountSwarmGUI" "$@"
elif [ -f "$RES_DIR/AgyCliAccountSwarmGUI.dll" ]; then
    exec dotnet "$RES_DIR/AgyCliAccountSwarmGUI.dll" "$@"
elif command -v wine >/dev/null 2>&1 && [ -f "$RES_DIR/AgyCliAccountSwarmGUI.exe" ]; then
    exec wine "$RES_DIR/AgyCliAccountSwarmGUI.exe" "$@"
else
    echo "Error: Could not locate AgyCliAccountSwarmGUI in $RES_DIR"
    exit 1
fi
EOF
chmod +x "$APP_DIR/Contents/MacOS/agy-cli-account-swarm"

# Create symlink in bin directory
ln -sf "$APP_DIR/Contents/MacOS/agy-cli-account-swarm" "$BIN_DIR/agy-cli-account-swarm"

echo -e "${GREEN}============================================================${NC}"
echo -e "${GREEN}✓ macOS Installation Complete!${NC}"
echo -e "${GREEN}  Application: $APP_DIR${NC}"
echo -e "${GREEN}  Terminal Command: $BIN_DIR/agy-cli-account-swarm${NC}"
echo -e "${GREEN}  To uninstall, run:${NC}"
echo -e "${GREEN}    \"$APP_DIR/Contents/Resources/uninstall.sh\"${NC}"
echo -e "${GREEN}============================================================${NC}"
