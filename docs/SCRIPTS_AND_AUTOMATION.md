# Developer Scripts & Automation Guide 🛠️

> **Directory**: `scripts/`  
> **Supported Environments**: Windows PowerShell 5.1 / Core, Linux Bash, macOS Zsh/Bash  
> **Scope**: Packaging, release builds, sandbox maintenance, MCP verification, and multi-platform lifecycle automation.

---

## 📌 1. Overview

The `scripts/` directory provides essential automation tools for developers and system administrators:

| Script | Platform | Primary Purpose |
| :--- | :--- | :--- |
| **`build-installer.ps1`** | Windows PowerShell | Full production packaging: compiles release, runs Inno Setup, packages portable ZIP, generates SHA256 checksums. |
| **`build-release.ps1`** | Windows PowerShell | Lightweight release compilation: publishes binaries directly to `publish/`. |
| **`clean-sandbox.ps1`** | Windows PowerShell | Purges stale lock files, temporary caches, and SQLite journal locks across all profile sandboxes. |
| **`setup-profile.ps1`** | Windows PowerShell | Scaffolds a new account sandbox directory, creates launchers, and seeds default configs. |
| **`verify-mcp.ps1`** | Windows PowerShell | Verifies MCP server connections and validates JSON schemas against `mcp_config.json`. |
| **`install-linux.sh`** | Linux (Bash) | Configures POSIX sandbox directories, sets up `run-agy.sh`, and validates Wine/.NET environment. |
| **`uninstall-linux.sh`** | Linux (Bash) | Cleans up Linux profile directories and configuration files. |
| **`install-macos.sh`** | macOS (Zsh/Bash) | Sets up macOS sandbox directories, environment exports, and launcher scripts. |
| **`uninstall-macos.sh`** | macOS (Zsh/Bash) | Cleans up macOS sandbox directories and configuration files. |
| **`installer.iss`** | Windows (Inno Setup) | Inno Setup compiler script configuring the Windows GUI Setup Wizard. |

---

## 📦 2. Production Packaging: `build-installer.ps1`

### Usage:
```powershell
powershell -ExecutionPolicy Bypass -File scripts\build-installer.ps1 -Version "0.9.7-beta"
```

### Automation Steps:
1. **Verification**: Checks that .NET 9 SDK and Inno Setup 6 (`ISCC.exe`) are installed.
2. **Clean Artifacts**: Wipes previous output directories (`publish/` and `dist/`).
3. **Release Compilation**: Executes `dotnet publish -c Release -r win-x64 --self-contained false -o publish`.
4. **Installer Compilation**: Invokes `ISCC.exe scripts\installer.iss /DMyAppVersion=0.9.7-beta`, generating `dist/Agy-CLI-Account-Swarm-Setup-v0.9.7-beta.exe`.
5. **Portable Packaging**: Archives `publish/` into `dist/Agy-CLI-Account-Swarm-v0.9.7-beta-win-x64.zip`.
6. **Integrity Checksums**: Generates `dist/SHA256SUMS.txt` with cryptographic hashes for all distribution artifacts.

---

## 🧹 3. Sandbox Maintenance: `clean-sandbox.ps1`

### Usage:
```powershell
powershell -ExecutionPolicy Bypass -File scripts\clean-sandbox.ps1 -TargetProfile "Worker Alpha"
# Or clean all sandboxes:
powershell -ExecutionPolicy Bypass -File scripts\clean-sandbox.ps1 -All
```

### Automation Steps:
- Scans `~/.gemini-profiles/` for lingering `.lock` files in `presence/` directories.
- Removes orphaned `*.db-journal` and `*.db-wal` temporary SQLite files.
- Checks if running `agy.exe` processes are locking files before deletion.
- Leaves intact all OAuth credentials (`antigravity-oauth-token`) and conversation histories.

---

## ⚙️ 4. Profile Scaffolding: `setup-profile.ps1`

### Usage:
```powershell
powershell -ExecutionPolicy Bypass -File scripts\setup-profile.ps1 -ProfileName "Worker-Beta" -Workspace "D:\Projects\MyApp"
```

### Automation Steps:
- Creates the sandbox folder hierarchy:
  - `~/.gemini-profiles/Worker-Beta/.gemini/antigravity-cli/`
  - `~/.gemini-profiles/Worker-Beta/.gemini/antigravity-cli/brain/`
  - `~/.gemini-profiles/Worker-Beta/.gemini/antigravity-cli/conversations/`
- Generates `run-agy.cmd` with linear label branching and UTF-8 code page `65001`.
- Generates POSIX `run-agy.sh` with `SSH_CONNECTION=1` keyring decoupling.
- Seeds base `settings.json` and empty `history.jsonl`.

---

## 🔍 5. MCP Verification: `verify-mcp.ps1`

### Usage:
```powershell
powershell -ExecutionPolicy Bypass -File scripts\verify-mcp.ps1
```

### Automation Steps:
- Discovers `mcp_config.json` in global `%USERPROFILE%\.gemini\` and per-profile sandboxes.
- Probes each defined MCP server executable (e.g. `node`, `npx`, `python`).
- Validates JSON schemas under `~/.gemini/antigravity-cli/mcp/` to detect malformed tools before spawning agent sessions.

---

## 🐧 6. Cross-Platform Scripts: `install-linux.sh` & `install-macos.sh`

### Linux Sandbox Setup:
```bash
chmod +x scripts/install-linux.sh
./scripts/install-linux.sh
```
- Creates POSIX profile directories in `$HOME/.gemini-profiles/`.
- Sets executable permissions (`chmod +x`) on generated `run-agy.sh` scripts.
- Configures environment export profiles for terminal emulators (`ptyxis`, `gnome-terminal`, `konsole`, `kitty`).

### macOS Sandbox Setup:
```bash
chmod +x scripts/install-macos.sh
./scripts/install-macos.sh
```
- Sets up Darwin user directory hierarchy in `$HOME/.gemini-profiles/`.
- Configures `SSH_CONNECTION=1` and `SSH_CLIENT=1` exports to decouple macOS Keychain.
- Creates launcher stubs ready for `Terminal.app` or `iTerm2`.
