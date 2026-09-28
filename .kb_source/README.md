# Agy CLI Account Swarm

Multi-account session manager for Google Antigravity CLI (`agy`) on Windows.

Run multiple `agy` sessions side-by-side using isolated account sandboxes, separate credential files, and configurable tier quotas.

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D7?style=flat&logo=windows)](https://microsoft.com/windows)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

## What It Does

By default, Google Antigravity CLI (`agy`) stores tokens and settings in `~/.gemini/antigravity-cli`. Switching accounts overwrites tokens or triggers file locks.

Agy CLI Account Swarm isolates each account:
- **Sandbox Folders**: Redirects `USERPROFILE` and `HOME` to `~/.gemini-profiles/{profile_name}`.
- **Credential Protection**: Uses Windows DPAPI to protect token files at rest, avoiding shared Windows Credential Manager collisions.
- **Live Usage Telemetry**: Reads remaining quota buckets and reset countdowns directly from `agy -p "/usage" --output-format json`.
- **Configurable Quotas**: Quotas load from `%APPDATA%\AgyAccountSwarm\quota_config.json`.
- **Terminal Integration**: Launches profiles into Windows Terminal tabs/panes, PowerShell, or Command Prompt.

---

## Platform Support

- **Windows 10 / 11 (64-bit)**: Supported natively (built with WPF on .NET 9).
- **Linux & macOS**: Not natively supported. WPF requires the Windows Desktop runtime. Running via Wine or a future cross-platform build is experimental/planned.

---

## Prerequisites

1. **Google Antigravity CLI (`agy`)** installed and available in your `PATH` or at `%LOCALAPPDATA%\agy\bin\agy.exe`.
   - Verify by running `agy --version` in terminal.
2. **.NET 9 Desktop Runtime** (Windows x64):
   ```powershell
   winget install Microsoft.DotNet.DesktopRuntime.9
   ```
3. *(Optional)* **Windows Terminal (`wt.exe`)** for multi-tab and split-pane swarm launching:
   ```powershell
   winget install Microsoft.WindowsTerminal
   ```

---

## Installation

### Option A: Windows Installer (.exe)
1. Download `Agy-CLI-Account-Swarm-Setup-v0.9.3-beta.exe` from [GitHub Releases](https://github.com/RifkyA911/agy-cli-account-swarm/releases).
2. Run the setup wizard.
3. To uninstall: Use **Windows Settings > Installed apps** or run `unins000.exe` in the installation directory.

### Option B: Portable ZIP
1. Download `Agy-CLI-Account-Swarm-v0.9.3-beta-win-x64.zip`.
2. Extract and run `AgyAccountSwarm.exe`.
3. To uninstall: Delete the extracted folder.

---

## Building from Source

```powershell
git clone https://github.com/RifkyA911/agy-cli-account-swarm.git
cd agy-cli-account-swarm

# Run tests
dotnet test

# Build and run
dotnet run --project AgyAccountSwarm.csproj -c Release
```

---

## Troubleshooting

- **`agy: command not found` / Not detected**:
  Ensure `agy.exe` is inside `%LOCALAPPDATA%\agy\bin` or included in system `PATH`. Restart the application after modifying `PATH`.
- **Profile Shows "Needs Login"**:
  Launch the profile terminal. Antigravity will display an authentication URL. Complete login in your browser; the app detects the new token automatically.
- **Corrupt Token Warning**:
  Delete the token file in `~/.gemini-profiles/{profile_name}/.gemini/antigravity-cli/antigravity-oauth-token` and re-authenticate.

---

## Disclaimer & Terms of Service Notice

This tool is an independent open-source project and is not affiliated with, sponsored by, or endorsed by Google LLC.

Users are solely responsible for ensuring that their use of multiple Google accounts complies with [Google Terms of Service](https://policies.google.com/terms) and Google Antigravity acceptable use policies.

---

## License

[MIT License](LICENSE) - Copyright (c) 2026 RifkyA911.
