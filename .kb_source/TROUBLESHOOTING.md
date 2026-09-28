# Troubleshooting Guide

## 1. "mkdir C:\Users\rifky \.gemini: The system cannot find the path specified"
**Root Cause**: Trailing whitespace in `%USERPROFILE%` variable or profile name passed to Google Antigravity CLI.
**Solution**: Handled automatically in v1.3 via `path.Trim()` sanitization before process startup.

## 2. "The Process object must have the UseShellExecute property set to false"
**Root Cause**: Attempting to pass custom environment variables (`USERPROFILE`, `HOME`) while `UseShellExecute = true`.
**Solution**: v1.3 sets `UseShellExecute = false` on every process creation.

## 3. Account shows "Pending Login"
**Root Cause**: The profile directory does not yet contain a valid OAuth token in `.gemini`.
**Solution**: Click "Launch agy" for that account to complete the initial Google login prompt in the terminal.

## 4. MCP Tools not showing
**Root Cause**: The MCP tool definitions are not located in `%USERPROFILE%\.gemini\antigravity-cli\mcp` or `mcp_config.json`.
**Solution**: Check that the server directory exists and contains JSON schemas or instructions.
