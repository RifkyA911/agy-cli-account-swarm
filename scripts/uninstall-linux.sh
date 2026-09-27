#!/usr/bin/env bash
# ==============================================================================
# Uninstaller for Agy CLI Account Swarm (Linux)
# ==============================================================================
set -e

GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${CYAN}============================================================${NC}"
echo -e "${CYAN}   Agy CLI Account Swarm — Linux Uninstaller                 ${NC}"
echo -e "${CYAN}============================================================${NC}"

# Detect install paths
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

echo -e "${YELLOW}Removing launcher symlinks and desktop entries...${NC}"
rm -f "$BIN_DIR/agy-cli-account-swarm"
rm -f "$DESKTOP_DIR/agy-cli-account-swarm.desktop"
rm -f "$ICON_DIR/agy-cli-account-swarm.svg"

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$DESKTOP_DIR" 2>/dev/null || true
fi

echo -e "${YELLOW}Removing application files from $INSTALL_DIR...${NC}"
rm -rf "$INSTALL_DIR"

echo -e "${GREEN}============================================================${NC}"
echo -e "${GREEN}✓ Agy CLI Account Swarm has been cleanly uninstalled.${NC}"
echo -e "  (Note: Your account sandbox data in ~/.gemini-profiles was preserved.)${NC}"
echo -e "${GREEN}============================================================${NC}"
