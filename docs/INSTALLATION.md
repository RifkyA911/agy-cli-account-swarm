# 📦 Multiplatform Installation & Deployment Guide
**Agy CLI Account Swarm — Windows, Linux, and macOS (x64 & ARM64)**

This guide provides end-to-end installation, verification, and setup instructions for all supported operating systems and CPU architectures.

---

## 🚀 Available Distribution Packages

Every official release includes pre-compiled, optimized binaries ready for production use:

| Platform | Architecture | Format | Package Filename | Description |
| :--- | :--- | :--- | :--- | :--- |
| **Windows** | **x64** (Intel/AMD) | Installer GUI (`.exe`) | `Agy-CLI-Account-Swarm-Setup-v<ver>.exe` | Recommended Inno Setup wizard with desktop shortcut & uninstaller |
| **Windows** | **x64** (Intel/AMD) | Portable (`.zip`) | `Agy-CLI-Account-Swarm-v<ver>-win-x64.zip` | Zero-install standalone archive (extract & run) |
| **Windows** | **ARM64** | Portable (`.zip`) | `Agy-CLI-Account-Swarm-v<ver>-win-arm64.zip` | Native ARM64 for Snapdragon X Elite & Surface Pro Copilot+ PCs |
| **Linux** | **x64** (x86_64) | Tarball (`.tar.gz`) | `Agy-CLI-Account-Swarm-v<ver>-linux-x64.tar.gz` | Includes standalone binaries, `install.sh`, and `.desktop` launcher |
| **Linux** | **ARM64** (aarch64) | Tarball (`.tar.gz`) | `Agy-CLI-Account-Swarm-v<ver>-linux-arm64.tar.gz` | Native aarch64 build for ARM64 servers, Raspberry Pi 5, & VMs |
| **macOS** | **Apple Silicon** (M1–M4) | Tarball (`.tar.gz`) | `Agy-CLI-Account-Swarm-v<ver>-macos-arm64.tar.gz` | `Agy CLI Account Swarm.app` bundle with Gatekeeper bypass helper |
| **macOS** | **Intel** (x86_64) | Tarball (`.tar.gz`) | `Agy-CLI-Account-Swarm-v<ver>-macos-x64.tar.gz` | `Agy CLI Account Swarm.app` bundle for Intel-based Macs |
| **All Platforms** | — | Checksums (`.txt`) | `SHA256SUMS.txt` | Cryptographic SHA256 integrity hash verification |

---

## 🪟 1. Windows Installation (x64 & ARM64)

### Method A: Setup Wizard Installer (Recommended for x64)
1. Download **`Agy-CLI-Account-Swarm-Setup-v<ver>.exe`** from [GitHub Releases](https://github.com/RifkyA911/agy-cli-account-swarm/releases).
2. Run the `.exe` installer. If Windows SmartScreen appears (*Unknown Publisher*), click **More info** ➔ **Run anyway**.
3. Follow the installation wizard:
   - Select optional Desktop shortcut creation.
   - The application installs cleanly into `%LOCALAPPDATA%\Programs\Agy CLI Account Swarm` without requiring Administrator (UAC) elevation.
4. Click **Finish** to launch the application.
5. To uninstall, use **Windows Settings ➔ Installed Apps** or the Start Menu shortcut **Uninstall Agy CLI Account Swarm**.

### Method B: Portable ZIP (Windows x64 / ARM64)
1. Download `Agy-CLI-Account-Swarm-v<ver>-win-x64.zip` or `Agy-CLI-Account-Swarm-v<ver>-win-arm64.zip`.
2. Right-click ➔ **Extract All...** to your desired directory (e.g., `C:\Tools\AgyAccountSwarm`).
3. Double-click **`AgyCliAccountSwarmGUI.exe`** to launch.

> [!TIP]
> Verify that the Google Antigravity CLI (`agy`) is accessible in your environment:
> ```powershell
> agy --version
> ```

---

## 🐧 2. Linux Installation (x64 & ARM64)

Supports all modern Linux distributions (Ubuntu, Debian, Fedora, Arch Linux, openSUSE) across both **Wayland** and **X11** sessions.

### Prerequisites:
Ensure standard desktop graphics libraries are installed:
- **Ubuntu / Debian**: `sudo apt install libx11-6 libice6 libsm6 libfontconfig1`
- **Fedora / RHEL**: `sudo dnf install libX11 libICE libSM fontconfig`
- **Arch Linux**: `sudo pacman -S libx11 libice libsm fontconfig`

### Installation Steps:
1. Download the archive for your architecture:
   - For Intel/AMD 64-bit systems: `Agy-CLI-Account-Swarm-v<ver>-linux-x64.tar.gz`
   - For ARM64 / aarch64 systems: `Agy-CLI-Account-Swarm-v<ver>-linux-arm64.tar.gz`
2. Extract the archive:
   ```bash
   tar -xzf Agy-CLI-Account-Swarm-v*-linux-*.tar.gz
   cd agy-cli-account-swarm-v*-linux-*
   ```
3. Run the installer script:
   ```bash
   bash install.sh
   ```
   *The installer automatically:*
   - Copies files to `~/.local/share/agy-cli-account-swarm` (or `/opt` if executed with `sudo`).
   - Provisions a launch wrapper at `~/.local/bin/agy-cli-account-swarm`.
   - Registers a desktop menu entry (`.desktop`) with high-resolution vector icon in your system's application launcher.
4. Launch the application from your desktop launcher or run:
   ```bash
   agy-cli-account-swarm
   ```

### Uninstallation:
Run the uninstaller script:
```bash
~/.local/share/agy-cli-account-swarm/uninstall.sh
```

---

## 🍎 3. macOS Installation (Apple Silicon & Intel)

Supports macOS Sonoma, Sequoia, and earlier versions on Apple Silicon (M1/M2/M3/M4) and Intel Macs.

### Installation Steps:
1. Download the archive for your architecture:
   - Apple Silicon (M1/M2/M3/M4): `Agy-CLI-Account-Swarm-v<ver>-macos-arm64.tar.gz`
   - Intel-based Macs: `Agy-CLI-Account-Swarm-v<ver>-macos-x64.tar.gz`
2. Extract the archive:
   ```bash
   tar -xzf Agy-CLI-Account-Swarm-v*-macos-*.tar.gz
   cd agy-cli-account-swarm-v*-macos-*
   ```
3. Run the installer script:
   ```bash
   bash install.sh
   ```
   *The installer automatically:*
   - Installs `Agy CLI Account Swarm.app` into `/Applications` (or `~/Applications`).
   - Clears macOS Gatekeeper quarantine attributes (`xattr -cr`).
   - Creates a terminal launcher symlink at `~/.local/bin/agy-cli-account-swarm` (or `/usr/local/bin`).
4. Launch the application from **Launchpad**, **Spotlight** (`Cmd + Space` ➔ `Agy CLI Account Swarm`), or via terminal:
   ```bash
   agy-cli-account-swarm
   ```

> [!NOTE]
> If macOS presents a security dialog stating *"Agy CLI Account Swarm cannot be opened because the developer cannot be verified"*:
> Open **System Settings ➔ Privacy & Security**, scroll down to the **Security** section, and click **Open Anyway**. Alternatively, run the following terminal command:
> ```bash
> xattr -cr "/Applications/Agy CLI Account Swarm.app"
> ```

### Uninstallation:
Run the bundled uninstaller:
```bash
"/Applications/Agy CLI Account Swarm.app/Contents/Resources/uninstall.sh"
```

---

## 🔐 4. Cryptographic SHA256 Verification

To verify the integrity of your downloaded package against tampering or corruption:

### Windows (PowerShell):
```powershell
Get-FileHash -Path .\Agy-CLI-Account-Swarm-Setup-v*.exe -Algorithm SHA256
```

### Linux & macOS:
```bash
sha256sum Agy-CLI-Account-Swarm-v*.tar.gz
```
Compare the resulting hash with the official values in `SHA256SUMS.txt` on the GitHub release page.

---

## ⚙️ 5. Prerequisites & Antigravity CLI Setup

Agy CLI Account Swarm serves as a desktop GUI orchestrator for the **Google Antigravity CLI (`agy`)**.

1. Ensure Google Antigravity CLI is installed and in your `PATH`:
   ```bash
   agy --version
   ```
2. Authenticate your default Google account:
   ```bash
   agy login
   ```
3. Launch **Agy CLI Account Swarm**:
   - Your primary account is detected automatically with active 5-hour and weekly capacity quotas.
   - Click **`+ Add Profile`** to create secondary and tertiary account sandboxes with isolated directories (`~/.gemini-profiles/<id>`) and independent credentials with zero token collisions.

---

## 💬 Community & Support
- **Issue Tracker**: [GitHub Issues](https://github.com/RifkyA911/agy-cli-account-swarm/issues)
- **Troubleshooting Guide**: [`docs/TROUBLESHOOTING.md`](TROUBLESHOOTING.md)
