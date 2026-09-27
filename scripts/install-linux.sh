#!/usr/bin/env bash
# ==============================================================================
# Installer for Agy CLI Account Swarm (Linux)
# Version: 0.9.3-beta
# ==============================================================================
set -e

GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${CYAN}============================================================${NC}"
echo -e "${CYAN}    Agy CLI Account Swarm — Linux Installer (v0.9.3-beta)    ${NC}"
echo -e "${CYAN}============================================================${NC}"

# Detect install target
if [ "$(id -u)" -eq 0 ]; then
    INSTALL_DIR="/opt/agy-cli-account-swarm"
    BIN_DIR="/usr/local/bin"
    DESKTOP_DIR="/usr/share/applications"
    ICON_DIR="/usr/share/icons/hicolor/scalable/apps"
else
    INSTALL_DIR="$HOME/.local/share/agy-cli-account-swarm"
    BIN_DIR="$HOME/.local/bin"
    DESKTOP_DIR="$HOME/.local/share/applications"
    ICON_DIR="$HOME/.local/share/icons/hicolor/scalable/apps"
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PAYLOAD_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

echo -e "${YELLOW}[1/4] Creating installation directories...${NC}"
mkdir -p "$INSTALL_DIR"
mkdir -p "$BIN_DIR"
mkdir -p "$DESKTOP_DIR"
mkdir -p "$ICON_DIR"

echo -e "${YELLOW}[2/4] Copying files to $INSTALL_DIR...${NC}"
if [ -d "$PAYLOAD_DIR/publish" ]; then
    cp -r "$PAYLOAD_DIR/publish"/* "$INSTALL_DIR/"
elif [ -d "$PAYLOAD_DIR/bundle" ]; then
    cp -r "$PAYLOAD_DIR/bundle"/* "$INSTALL_DIR/"
else
    # Current payload directory copy
    cp -r "$PAYLOAD_DIR"/* "$INSTALL_DIR/" 2>/dev/null || true
fi

# Copy icon if available
if [ -f "$PAYLOAD_DIR/docs/assets/banner.svg" ]; then
    cp "$PAYLOAD_DIR/docs/assets/banner.svg" "$ICON_DIR/agy-cli-account-swarm.svg"
fi

# Copy uninstaller alongside installed files
cp "$SCRIPT_DIR/uninstall-linux.sh" "$INSTALL_DIR/uninstall.sh"
chmod +x "$INSTALL_DIR/uninstall.sh"

echo -e "${YELLOW}[3/4] Creating command wrapper in $BIN_DIR...${NC}"
cat << 'EOF' > "$BIN_DIR/agy-cli-account-swarm"
#!/usr/bin/env bash
# Agy CLI Account Swarm Launch Wrapper
INSTALL_DIR_RESOLVED="$(dirname "$(readlink -f "$0")")/../share/agy-cli-account-swarm"
[ -d "$INSTALL_DIR_RESOLVED" ] || INSTALL_DIR_RESOLVED="/opt/agy-cli-account-swarm"
[ -d "$INSTALL_DIR_RESOLVED" ] || INSTALL_DIR_RESOLVED="$HOME/.local/share/agy-cli-account-swarm"

echo "⚡ Starting Agy CLI Account Swarm Desktop GUI..."
if command -v wine >/dev/null 2>&1 && [ -f "$INSTALL_DIR_RESOLVED/AgyAccountSwarm.exe" ]; then
    wine "$INSTALL_DIR_RESOLVED/AgyAccountSwarm.exe" "$@"
elif command -v dotnet >/dev/null 2>&1 && [ -f "$INSTALL_DIR_RESOLVED/AgyAccountSwarm.dll" ]; then
    dotnet "$INSTALL_DIR_RESOLVED/AgyAccountSwarm.dll" "$@"
else
    echo "Notice: Agy CLI Account Swarm GUI requires Wine or .NET 9 Desktop runtime on Linux."
    echo "To run natively on Linux, please ensure Wine or .NET runtime is installed."
fi
EOF
chmod +x "$BIN_DIR/agy-cli-account-swarm"

echo -e "${YELLOW}[4/4] Registering Linux Desktop Application entry...${NC}"
cat << EOF > "$DESKTOP_DIR/agy-cli-account-swarm.desktop"
[Desktop Entry]
Version=1.0
Type=Application
Name=Agy CLI Account Swarm
GenericName=Antigravity CLI Multi-Account Orchestrator
Comment=Multi-account orchestrator and real telemetry matrix for Google Antigravity CLI
Exec=$BIN_DIR/agy-cli-account-swarm
Icon=agy-cli-account-swarm
Terminal=false
Categories=Development;Utility;
Keywords=antigravity;gemini;cli;swarm;ai;
EOF

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$DESKTOP_DIR" 2>/dev/null || true
fi

echo -e "${GREEN}============================================================${NC}"
echo -e "${GREEN}✓ Installation Complete!${NC}"
echo -e "${GREEN}  Binary: $BIN_DIR/agy-cli-account-swarm${NC}"
echo -e "${GREEN}  Installed at: $INSTALL_DIR${NC}"
echo -e "${GREEN}  To uninstall, run: $INSTALL_DIR/uninstall.sh${NC}"
echo -e "${GREEN}============================================================${NC}"
