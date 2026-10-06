# Developer Scripts & Automation Guide 🛠️

> **Directory**: `scripts/`  
> **Supported Environments**: Windows PowerShell 5.1 / 7+, Linux Bash, macOS Zsh/Bash  
> **Scope**: Multiplatform packaging, release builds, desktop deployment, sandbox maintenance, and MCP verification.

---

## 📌 1. Overview of Automation Scripts

The `scripts/` directory provides essential automation tools for developers, release maintainers, and end users:

| Script | Platform | Primary Purpose |
| :--- | :--- | :--- |
| **`build-installer.ps1`** | Windows PowerShell 5.1 / 7+ | **Master Multiplatform Release Builder**: Compiles and packages 6 distribution targets (Windows x64/ARM64, Linux x64/ARM64, macOS Apple Silicon/Intel), runs Inno Setup, generates `.tar.gz` and `.zip` archives, and produces `SHA256SUMS.txt`. |
| **`build-release.ps1`** | Windows PowerShell | Lightweight single-target release compilation to local `publish/`. |
| **`clean-sandbox.ps1`** | Windows PowerShell | Purges stale lock files (`presence/*.lock`), SQLite journal locks, and temporary caches across profile sandboxes. |
| **`setup-profile.ps1`** | Windows PowerShell | Scaffolds a new account sandbox directory, creates launchers (`run-agy.cmd` and `run-agy.sh`), and seeds default configs. |
| **`verify-mcp.ps1`** | Windows PowerShell | Verifies MCP server connections and validates JSON schemas against `mcp_config.json`. |
| **`install-linux.sh`** | Linux (Bash) | **Linux Desktop Application Installer**: Copies application payload to `~/.local/share` (or `/opt`), registers `.desktop` launcher and SVG icon, and provisions `agy-cli-account-swarm` terminal command. |
| **`uninstall-linux.sh`** | Linux (Bash) | Removes application binaries, desktop launcher entries, and command wrapper on Linux. |
| **`install-macos.sh`** | macOS (Zsh/Bash) | **macOS Application Installer**: Deploys `Agy CLI Account Swarm.app` into `/Applications`, bypasses Gatekeeper quarantine (`xattr -cr`), and creates terminal symlinks. |
| **`uninstall-macos.sh`** | macOS (Zsh/Bash) | Uninstalls the macOS `.app` bundle and removes terminal launcher symlinks. |
| **`installer.iss`** | Windows (Inno Setup 6) | Inno Setup compiler script generating the Windows Setup Wizard (`Agy-CLI-Account-Swarm-Setup-v<ver>.exe`). |

---

## 📦 2. Production Packaging: `build-installer.ps1`

`build-installer.ps1` is the authoritative multiplatform build engine used both locally and in CI/CD release workflows.

### Usage:
```powershell
# Build complete release matrix (v0.9.13-beta)
powershell -ExecutionPolicy Bypass -File scripts\build-installer.ps1 -Version "0.9.13-beta"

# Build Windows-only targets
powershell -ExecutionPolicy Bypass -File scripts\build-installer.ps1 -Version "0.9.13-beta" -WindowsOnly
```

### Automation Steps:
1. **Directory Preparation**: Cleans output directories (`dist/` and `publish/`) to prevent stale artifacts or test leftovers from polluting archives.
2. **Windows x64 Packaging**:
   - Publishes .NET 9 Avalonia binaries to `publish/`.
   - Locates Inno Setup 6 compiler (`ISCC.exe`) and compiles `scripts/installer.iss` ➔ `dist/Agy-CLI-Account-Swarm-Setup-v<ver>.exe`.
   - Compresses `publish/` ➔ `dist/Agy-CLI-Account-Swarm-v<ver>-win-x64.zip`.
3. **Windows ARM64 Packaging**:
   - Compiles native `win-arm64` binaries via `dotnet publish -r win-arm64`.
   - Compresses output ➔ `dist/Agy-CLI-Account-Swarm-v<ver>-win-arm64.zip`.
4. **Linux x64 & ARM64 Packaging**:
   - Compiles native `linux-x64` and `linux-arm64` binaries via `dotnet publish -r linux-*`.
   - Bundles `install.sh`, `uninstall.sh`, and vector icons alongside the binaries.
   - Archives via System32 tar into `dist/Agy-CLI-Account-Swarm-v<ver>-linux-*.tar.gz`.
5. **macOS Apple Silicon & Intel Packaging**:
   - Compiles native `osx-arm64` and `osx-x64` binaries via `dotnet publish -r osx-*`.
   - Assembles standard `Agy CLI Account Swarm.app` bundle (`Contents/Info.plist`, `Contents/MacOS/agy-cli-account-swarm`, and `Contents/Resources`).
   - Bundles `install.sh` and `uninstall.sh` into `dist/Agy-CLI-Account-Swarm-v<ver>-macos-*.tar.gz`.
6. **Integrity Checksums**:
   - Computes SHA256 hashes for all generated release artifacts and writes them to `dist/SHA256SUMS.txt`.

---

## 🐧 3. Linux Desktop Installation: `install-linux.sh`

The Linux installer can be executed either directly from an extracted release tarball or from the repository root:

```bash
tar -xzf Agy-CLI-Account-Swarm-v0.9.13-beta-linux-x64.tar.gz
cd agy-cli-account-swarm-v0.9.13-beta-linux-x64
bash install.sh
```

### What It Does:
- **Target Resolution**: Installs to `~/.local/share/agy-cli-account-swarm` for regular users, or `/opt/agy-cli-account-swarm` if run as `root`/`sudo`.
- **Command Wrapper**: Provisions `~/.local/bin/agy-cli-account-swarm` which launches the native executable or falls back to `dotnet AgyCliAccountSwarmGUI.dll`.
- **Desktop Entry**: Installs `agy-cli-account-swarm.desktop` with high-resolution vector icon in `~/.local/share/applications/` and triggers `update-desktop-database`.
- **Executable Permissions**: Automatically marks `AgyCliAccountSwarmGUI` with `chmod +x`.

---

## 🍎 4. macOS Desktop Installation: `install-macos.sh`

```bash
tar -xzf Agy-CLI-Account-Swarm-v0.9.13-beta-macos-arm64.tar.gz
cd agy-cli-account-swarm-v0.9.13-beta-macos-arm64
bash install.sh
```

### What It Does:
- Copies `Agy CLI Account Swarm.app` into `/Applications` (or `~/Applications`).
- Clears macOS Gatekeeper quarantine attributes via `xattr -cr` so the application opens without "developer cannot be verified" blocks.
- Symlinks the launch wrapper into `~/.local/bin/agy-cli-account-swarm`.

---

## 🧹 5. Sandbox Maintenance: `clean-sandbox.ps1`

```powershell
# Clean a single profile
powershell -ExecutionPolicy Bypass -File scripts\clean-sandbox.ps1 -TargetProfile "Worker Alpha"

# Clean all sandboxes
powershell -ExecutionPolicy Bypass -File scripts\clean-sandbox.ps1 -All
```

### What It Does:
- Scans `~/.gemini-profiles/` for lingering `.lock` files in `presence/` directories.
- Removes orphaned `*.db-journal` and `*.db-wal` SQLite files.
- Ensures running `agy` processes are not disturbed before clearing locks.
- Strictly preserves all OAuth tokens (`antigravity-oauth-token`) and session histories.

---

## 🔍 6. MCP Verification: `verify-mcp.ps1`

```powershell
powershell -ExecutionPolicy Bypass -File scripts\verify-mcp.ps1
```

### What It Does:
- Discovers `mcp_config.json` in global `%USERPROFILE%\.gemini\` and per-profile sandboxes.
- Probes defined MCP server executables (e.g. `node`, `npx`, `python`).
- Validates JSON schemas under `~/.gemini/antigravity-cli/mcp/` to detect malformed tools before launching agent turns.
