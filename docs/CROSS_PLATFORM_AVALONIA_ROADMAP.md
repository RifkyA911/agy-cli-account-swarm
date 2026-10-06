# Cross-Platform Architecture & Deployment Status: Avalonia UI (Windows, Linux, macOS)

> **Status**: **Fully Delivered & Released (v0.9.13-beta+)**  
> **Framework**: **Avalonia UI 12+ (.NET 9)**  
> **Supported Runtimes**: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`  
> **Production Binaries**: `AgyCliAccountSwarmGUI.exe` (Windows) / `AgyCliAccountSwarmGUI` (Linux & macOS)  
> **Packaging**: Inno Setup (`.exe`), Portable ZIP (`.zip`), Linux Tarball with Desktop Entry (`.tar.gz`), macOS App Bundle (`.app` in `.tar.gz`)

---

## 1. Executive Summary

**Agy CLI Account Swarm** has successfully migrated from a Windows-only prototype to a **first-class cross-platform desktop flagship** powered by **Avalonia UI** on **.NET 9**. 

The legacy WPF codebase has been permanently archived. All current development, releases, and distribution pipelines target Avalonia UI, delivering identical UI fidelity, native hardware-accelerated rendering, and full multi-account sandboxing across **Windows**, **Linux**, and **macOS**.

---

## 2. Platform Architecture Matrix

| Capability Area | Windows (x64 / ARM64) | Linux (x64 / ARM64) | macOS (Apple Silicon & Intel) |
| :--- | :--- | :--- | :--- |
| **Rendering Subsystem** | Direct3D 11 / SkiaSharp | Wayland / X11 via SkiaSharp | Metal / Cocoa via SkiaSharp |
| **Window Chrome** | Fluent window styling, minimize, maximize, drag-to-move | Native window decorations & Wayland client-side decorations | macOS native window traffic lights & titlebar integration |
| **Terminal Host** | Windows Terminal (`wt.exe`), PowerShell, CMD | `ptyxis`, `gnome-terminal`, `konsole`, `alacritty`, `kitty` | `Terminal.app`, `iTerm2`, `kitty`, `ghostty` |
| **Sandbox Keyring** | File-based token via `SSH_CONNECTION=1` | File-based token via `export SSH_CONNECTION=1` | File-based token via `export SSH_CONNECTION=1` |
| **Sandbox Launchers** | `run-agy.cmd` (UTF-8 codepage 65001) | `run-agy.sh` (`chmod +x`, POSIX bash) | `run-agy.sh` (`chmod +x`, POSIX zsh/bash) |
| **Desktop Integration** | Start Menu shortcut, Desktop icon, Settings uninstaller | `.desktop` menu entry in `/usr/share/applications` or `~/.local/share/applications` | Pre-assembled `Agy CLI Account Swarm.app` in `/Applications` |
| **CLI Wrapper** | `AgyCliAccountSwarmGUI.exe` in `publish\` | `agy-cli-account-swarm` wrapper in `~/.local/bin` | `agy-cli-account-swarm` symlink in `~/.local/bin` |
| **Security Controls** | DPAPI encryption for local settings | POSIX permissions (`0600` / `0700`) on token files | Keychain decoupling + Gatekeeper quarantine bypass (`xattr -cr`) |

---

## 3. Implementation Breakdown

### A. Linux Desktop (Wayland & X11)
1. **Graphics Engine**: Avalonia renders natively via SkiaSharp to Wayland buffers or X11 surfaces, supporting fractional scaling (100%, 125%, 150%, 200%) on modern desktop environments like GNOME 45+, KDE Plasma 6, and tiling compositors (Hyprland, Sway).
2. **Terminal Multiplexing**: `LinuxTerminalLauncherService` detects the active terminal emulator and spawns tabs or standalone windows running `run-agy.sh`.
3. **Automated Desktop Registration**: The included `install.sh` provisions `agy-cli-account-swarm.desktop` with vector SVG iconography and executable wrapper links.

### B. macOS (Apple Silicon & Intel)
1. **Native Metal Graphics**: Avalonia leverages SkiaSharp with Metal acceleration on macOS Sonoma and Sequoia, offering buttery-smooth 60/120fps animations on ProMotion displays.
2. **Standard `.app` Bundle**: Pre-packaged structure:
   ```
   Agy CLI Account Swarm.app/
     Contents/
       Info.plist
       MacOS/
         agy-cli-account-swarm
       Resources/
         AgyCliAccountSwarmGUI
         AgyCliAccountSwarmGUI.dll
         favicon.ico
         uninstall.sh
   ```
3. **Gatekeeper Quarantine Resolution**: The automated `install.sh` script runs `xattr -cr "/Applications/Agy CLI Account Swarm.app"` to strip quarantine attributes, ensuring zero friction for developers installing from GitHub releases.

### C. Windows (x64 & ARM64)
1. **Modern Inno Setup Installer**: Ultra-compact lzma2 compression, per-user installation without Administrator prompt requirements, and integrated uninstaller.
2. **Native Windows on ARM64 Support**: Dedicated standalone package for Snapdragon X Elite and Surface Pro Copilot+ PCs running native ARM64 instructions without x86 emulation overhead.

---

## 4. Completed Milestones

- [x] **Milestone 1**: Generate executable `run-agy.sh` POSIX launcher script alongside `run-agy.cmd` for every account sandbox.
- [x] **Milestone 2**: Strip all UTF-8 BOM and Windows-only format assumptions from `antigravity-oauth-token` reading and writing.
- [x] **Milestone 3**: Shared architecture across ViewModels, Models, and Services verified with hermetic unit tests (170/170 passed).
- [x] **Milestone 4**: Complete Avalonia UI (`AgyAccountSwarm.Avalonia`) flagship edition with 100% bilingual localization (ID/EN), dedicated Personal Chat Studio, authentic live activity telemetry, and windowed pagination.
- [x] **Milestone 5**: Implement multiplatform release automation (`scripts/build-installer.ps1`) producing installers and standalone packages for Windows, Linux, and macOS (x64 and ARM64) with cryptographic SHA256 verification.
