# Cross-Platform Architecture Roadmap: Linux GUI (Avalonia UI) & macOS Status Assessment

## 1. Executive Summary & Reality Check

### Current Desktop Architecture
The current **Agy CLI Account Swarm** application is engineered in **C# / .NET 9** utilizing **WPF (Windows Presentation Foundation)** (`net9.0-windows`). WPF is architecturally bound to the Windows operating system:
- **Graphics Pipeline**: Hardware-accelerated rendering through DirectX 9/11 via Windows Media Integration Layer (`milcore.dll`).
- **Windowing & Message Loop**: Deep Win32 integration (`HWND`, `User32.dll`, `Gdi32.dll`, `ComCtl32.dll`).
- **System Integration**: Native Windows Data Protection API (DPAPI via `crypt32.dll`), Explorer shell integration, and Windows Terminal (`wt.exe`).

Because of these deep Windows subsystem dependencies, **WPF binaries cannot run natively on Linux or macOS**.

---

## 2. Is macOS Safe and Issue-Free? (Direct Assessment)

> **Direct Answer**: **NO, macOS is NOT currently safe or issue-free.** Running the existing WPF binary on macOS is impossible, and porting the application to macOS requires dedicated platform-specific adaptations.

### Deep-Dive Analysis of macOS Barriers & Required Work:

| Challenge Area | Windows (Current) | macOS Reality | Engineering Solution Required |
|---|---|---|---|
| **UI Framework** | WPF (.NET 9 Windows) | WPF does not exist on Darwin. Wine/CrossOver causes severe DirectX/WPF glitches. | Port UI to **Avalonia UI 11+** with native Metal rendering backend. |
| **Terminal Ecosystem** | `wt.exe`, `powershell.exe`, `cmd.exe` | macOS lacks `wt.exe`. Default shell is `/bin/zsh`. Terminals are `Terminal.app`, `iTerm2`, `Kitty`, `Ghostty`, or `Alacritty`. | Implement `MacTerminalLauncherService` utilizing AppleScript (`osascript`) or iTerm2 Python API to spawn tabs and split-panes. |
| **Default Account Keyring** | Windows Credential Manager (`gemini:antigravity`) | macOS Keychain Services (`security` CLI / Keychain API). | Adapt primary profile detector to read active Google session from macOS Keychain or `~/.gemini/google_accounts.json`. |
| **Worker Sandbox Isolation** | File-based token via `set "SSH_CONNECTION=1"` | `export SSH_CONNECTION=1` forces `agy` Go binary into file-based token mode (`~/.gemini-profiles/{id}/.gemini/antigravity-cli/antigravity-oauth-token`). | **Already Supported**: `run-agy.sh` exports `SSH_CONNECTION=1` and `SSH_CLIENT=1` to ensure sandbox isolation on POSIX systems. |
| **Filesystem Paths** | `%USERPROFILE%`, `C:\Users\{user}\` | `/Users/{username}/`, Unix forward slashes. | Use `Environment.GetFolderPath(SpecialFolder.UserProfile)` and `Path.Combine` (already fully path-agnostic). |
| **Code Signing & Gatekeeper** | Standard Windows executable | macOS Gatekeeper strictly quarantines unsigned downloaded binaries ("App is damaged and can't be opened"). | Requires Apple Developer ID certificate, Xcode code signing (`codesign --deep -s`), and Apple Notarization (`xcrun notarytool`). |
| **Packaging & Bundle** | Single `.exe` / publish folder | Standard macOS `.app` bundle directory containing `Contents/MacOS/`, `Contents/Info.plist`, and `Resources/app.icns`. | Build `.app` bundle via `dotnet publish -r osx-arm64` and `dotnet-bundle`. |

---

## 3. Linux GUI Roadmap: Avalonia UI 11+

To achieve full first-class native Linux desktop GUI support, the application will use **Avalonia UI** (v11.2+), the premier modern open-source cross-platform XAML UI framework for .NET.

### Architectural Breakdown

```
+---------------------------------------------------------------------------------+
|                       AgyAccountSwarm.Core (Shared Library)                     |
|  - Models (AccountProfile, ProfileAuthStatus, UsageReport, etc.)                |
|  - ViewModels (MainViewModel, ProfileItemViewModel, ProfileEditViewModel)       |
|  - Services (AuthDetectorService, ProfileStorageService, DoctorService)         |
+----------------------------------------+----------------------------------------+
                                         |
            +----------------------------+----------------------------+
            |                                                         |
+-----------v-----------------------+     +---------------------------v-----------+
|    AgyAccountSwarm.Wpf (Windows)  |     |  AgyAccountSwarm.Avalonia (Cross-Plat)|
|  - WPF XAML Views & Controls      |     |  - Avalonia XAML Views (Linux & macOS)|
|  - WindowsTerminalLauncherService |     |  - LinuxTerminalLauncherService       |
|  - Windows Notification & Tray    |     |  - MacTerminalLauncherService         |
|  - DirectX / Windows Native Shell |     |  - Skia / Wayland / X11 / Metal       |
+-----------------------------------+     +---------------------------------------+
```

### Linux Features & Platform Handlers:

1. **Wayland & X11 Display Server Support**:
   Avalonia renders natively via **SkiaSharp** directly to Wayland buffers or X11 windows, supporting Fractional Scaling (125%, 150%, 200%) on modern desktop environments like GNOME 45+, KDE Plasma 6, and Hyprland/Sway.

2. **Linux Terminal Orchestration**:
   `LinuxTerminalLauncherService` automatically detects installed terminal emulators in order of capability:
   - `ptyxis` (Modern GNOME / Fedora terminal with container and tab support)
   - `gnome-terminal` (`--tab --title="..." -- bash run-agy.sh`)
   - `konsole` (`--new-tab -e bash run-agy.sh`)
   - `alacritty` / `kitty` / `xterm` (`-e bash run-agy.sh`)

3. **POSIX Sandbox Launcher (`run-agy.sh`)**:
   Every account profile sandbox automatically generates an isolated, executable `run-agy.sh` script:
   ```bash
   #!/usr/bin/env bash
   # AGY Sandbox Shell - Generated by Agy Account Swarm
   export USERPROFILE="$HOME/.gemini-profiles/worker-1"
   export HOME="$HOME/.gemini-profiles/worker-1"
   export ANTIGRAVITY_APP_DATA_DIR="$HOME/.gemini-profiles/worker-1/.gemini/antigravity-cli"
   export JETSKI_APP_DATA_DIR="$HOME/.gemini-profiles/worker-1/.gemini/antigravity-cli"
   export SSH_CONNECTION=1
   export SSH_CLIENT=1
   cd "$HOME/workspace" || exit 1
   if [ "$1" = "--cli-only" ]; then
       echo "[AGY Sandbox Shell - Profile: Worker 1]"
       exec "${SHELL:-bash}"
   else
       exec agy --dangerously-skip-permissions "$@"
   fi
   ```

4. **Linux Distribution Packaging**:
   - **AppImage**: Single self-contained binary running across Ubuntu, Debian, Fedora, Arch, and openSUSE without installation.
   - **Flatpak**: Sandboxed distribution hosted on Flathub with permission portals for terminal spawning.
   - **Native Packages**: `.deb` (Debian/Ubuntu/Pop!_OS) and `.rpm` (Fedora/RHEL).

---

## 4. Immediate Next Steps & Milestones

- [x] **Milestone 1**: Generate executable `run-agy.sh` POSIX launcher script alongside `run-agy.cmd` for every account sandbox.
- [x] **Milestone 2**: Strip all UTF-8 BOM and Windows-only format assumptions from `antigravity-oauth-token` reading and writing.
- [x] **Milestone 3**: Shared architecture across ViewModels, Models, and Services verified with 162/162 hermetic unit tests.
- [x] **Milestone 4**: Complete Avalonia UI (`AgyAccountSwarm.Avalonia`) flagship edition with 100% bilingual localization (ID/EN), dedicated Real-Time Chat Studio, authentic live activity telemetry, and published as single solid executable (`publish\AgyCliAccountSwarmGUI.exe`).
- [ ] **Milestone 5**: Implement macOS `osascript` terminal launcher and package native macOS `.app` bundle.
