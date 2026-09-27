#!/usr/bin/env bash
# ==============================================================================
# Uninstaller for Agy CLI Account Swarm (macOS)
# ==============================================================================
set -e

GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${CYAN}============================================================${NC}"
echo -e "${CYAN}   Agy CLI Account Swarm — macOS Uninstaller                 ${NC}"
echo -e "${CYAN}============================================================${NC}"

APP_NAME="Agy CLI Account Swarm"
if [ -d "/Applications/$APP_NAME.app" ]; then
    APP_DIR="/Applications/$APP_NAME.app"
    BIN_DIR="/usr/local/bin"
else
    APP_DIR="$HOME/Applications/$APP_NAME.app"
    BIN_DIR="$HOME/.local/bin"
fi

echo -e "${YELLOW}Removing launcher symlink...${NC}"
rm -f "$BIN_DIR/agy-cli-account-swarm"

echo -e "${YELLOW}Removing application bundle from $APP_DIR...${NC}"
rm -rf "$APP_DIR"

echo -e "${GREEN}============================================================${NC}"
echo -e "${GREEN}✓ Agy CLI Account Swarm was successfully removed from your Mac.${NC}"
echo -e "${GREEN}============================================================${NC}"
