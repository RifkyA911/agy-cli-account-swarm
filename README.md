# Agy Account Swarm ⚡

> **A minimalist, high-performance desktop orchestrator & multi-account manager for Antigravity CLI (`agy`).**  
> Run multiple Antigravity AI agent sessions concurrently with strictly isolated Google accounts and workspace environments.

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/UI-WPF%20XAML-0078D4?style=flat&logo=windows)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![Architecture](https://img.shields.io/badge/Pattern-MVVM-10B981?style=flat)]()
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D7?style=flat&logo=windows)](https://microsoft.com/windows)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

## 💡 Overview

Google's **Antigravity CLI (`agy`)** stores its OAuth tokens, history, and configuration inside the user's home directory (`~/.gemini/antigravity-cli`). Running multiple CLI windows or agents at the same time on different Google accounts typically causes:

- **Auth Collisions**: One account's session overwrites the other's OAuth token.
- **Lock Contention**: Process locking conflicts on `~/.gemini/antigravity-cli/presence/*.lock`.
- **Quota Clashes**: Inability to parallelize tasks across multiple quota limits.

**Agy Account Swarm** solves this cleanly by isolating each account into dedicated sandbox environments using selective runtime environment redirection (`USERPROFILE` and `HOME`). Each profile operates with its own credentials, settings, and conversation logs, allowing **true simultaneous multi-agent execution**.

---

## ✨ Features

- **🛡️ Strict Environment Isolation**: Each profile points to its own sandbox (`~/.gemini-profiles/{profile-name}`) with independent tokens and sessions.
- **⚡ 1-Click Launchers**: Open any account in **Windows Terminal** (tabs or split panes), **PowerShell**, or **Command Prompt** with pre-configured environment variables.
- **🐝 Swarm Mode (Batch Multi-Launch)**: Launch 2, 4, or more account sessions concurrently with a single click. In Windows Terminal, Swarm Mode automatically organizes sessions into split panes or separate tabs!
- **🔍 Live Auth & Status Inspector**: Automatically inspects profile storage to display:
  - Status indicator (🟢 *Authenticated*, 🟡 *Needs Login*, ⚪ *Not Initialized*).
  - Detected Google account email (extracted from active token or Google identity cache).
- **📂 Workspace Anchoring**: Assign dedicated project folders to each account profile (e.g. Profile A always starts in `D:\Works\Project-A`, Profile B in `D:\Works\Project-B`).
- **📋 Instant CLI Snippet Copy**: 1-click copy of the exact command-line snippet for CMD or PowerShell so you can paste it into any custom script or external automation.
- **🎨 Minimalist Developer-First UI**: Clean dark theme inspired by modern developer tooling (VS Code, Linear) with zero AI bloat, high-contrast badges, and instant (<100ms) startup time.

---

## 🛠️ How Environment Isolation Works

Under the hood on Windows, `agy` utilizes `os.UserHomeDir()` to locate its root directory:

```text
Host Machine
├── C:\Users\rifky\.gemini\                   --> Default Account (Primary)
│   ├── antigravity-cli/
│   │   ├── antigravity-oauth-token          --> Token for Account 1
│   │   └── settings.json
│   └── google_accounts.json
│
└── C:\Users\rifky\.gemini-profiles\
    ├── worker-alpha\.gemini\                --> Isolated Account 2
    │   ├── antigravity-cli\
    │   │   └── antigravity-oauth-token      --> Token for Account 2
    │   └── google_accounts.json
    │
    └── worker-beta\.gemini\                 --> Isolated Account 3
        └── antigravity-cli\
            └── antigravity-oauth-token      --> Token for Account 3
```

When launching an account session, **Agy Account Swarm** creates and injects localized environment variables into the spawned terminal process:

```cmd
set "USERPROFILE=C:\Users\rifky\.gemini-profiles\worker-alpha"
set "HOME=C:\Users\rifky\.gemini-profiles\worker-alpha"
cd /d "D:\Works\YourProject"
agy
```

Because environment variables are inherited only by the child process, your global Windows environment remains clean and untouched!

---

## 🚀 Getting Started

### Prerequisites
- Windows 10 / 11 (64-bit)
- [.NET 9.0 Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) (or [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0))
- [Antigravity CLI (`agy`)](https://antigravity.google/docs/cli) installed and in `PATH` (or at `%LOCALAPPDATA%\agy\bin\agy.exe`)
- *(Optional)* [Windows Terminal](https://aka.ms/terminal) for multi-tab and split-pane swarm execution

### Running the App
Pre-built binaries are located in the `publish/` directory:
```powershell
D:\Works\Project\C#\agy-cli-account-swarm\publish\AgyAccountSwarm.exe
```

Or run via `dotnet`:
```powershell
cd "D:\Works\Project\C#\agy-cli-account-swarm"
dotnet run
```

---

## 🏗️ Project Architecture

Built using modern **C# 13** and **.NET 9 WPF** adhering to clean MVVM (Model-View-ViewModel) design principles:

```text
agy-cli-account-swarm/
├── Models/
│   ├── AccountProfile.cs            # Profile data model & path resolver
│   ├── ProfileAuthStatus.cs         # Auth state & Google email metadata
│   ├── TerminalType.cs              # Windows Terminal / PowerShell / CMD enums
│   └── AppSettings.cs               # App settings & preferences
├── Services/
│   ├── IProfileStorageService.cs    # Storage abstraction (%APPDATA%\AgyAccountSwarm)
│   ├── ProfileStorageService.cs     # JSON persistence & auto-seeding
│   ├── IAuthDetectorService.cs      # Token & Google profile detector
│   ├── AuthDetectorService.cs       # Token verification implementation
│   ├── ITerminalLauncherService.cs  # Terminal execution abstraction
│   └── TerminalLauncherService.cs   # Windows Terminal / WT split-pane builder
├── ViewModels/
│   ├── MainViewModel.cs             # Primary orchestrator, stats, swarm dispatch
│   ├── ProfileItemViewModel.cs      # Reactive card item with launch & copy commands
│   └── ProfileEditViewModel.cs      # Modal viewmodel for creating/editing profiles
├── Views/
│   ├── MainWindow.xaml              # Modern dark WPF main dashboard
│   ├── MainWindow.xaml.cs
│   ├── ProfileEditDialog.xaml       # Modal dialog for profile configuration
│   └── ProfileEditDialog.xaml.cs
├── Converters/
│   └── Converters.cs                # WPF UI binding converters
├── Resources/
│   └── Theme.xaml                   # Custom dark theme dictionary & controls
├── AgyAccountSwarm.csproj
└── README.md
```

---

## 💻 Tech Stack & Dependencies

- **Framework**: .NET 9.0 (`net9.0-windows`)
- **UI Platform**: Windows Presentation Foundation (WPF) with XAML
- **MVVM Toolkit**: `CommunityToolkit.Mvvm` (8.4.2)
- **JSON Serialization**: `System.Text.Json`
- **Zero Heavy Dependencies**: Completely native, instant cold start, lightweight footprint (~450 KB total binary).

---

## 📝 First-Time Account Login Walkthrough

1. Open **Agy Account Swarm**.
2. Click **+ New Profile** and enter a name (e.g., `Personal Swarm`).
3. Click **▶ Launch agy** on the new card.
4. An isolated terminal opens. Because it's a new profile, `agy` will launch the browser login page.
5. Sign in with your secondary Google account.
6. Return to **Agy Account Swarm** and click **Refresh Auth** — the status turns to `🟢 Authenticated (your-email@gmail.com)`.
7. You're ready to run both accounts concurrently!

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
