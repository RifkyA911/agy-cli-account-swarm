# Agy Account Swarm ⚡

> **A minimalist, high-performance desktop orchestrator & multi-account manager for Google Antigravity CLI (`agy`).**  
> Run multiple Antigravity AI agent sessions concurrently with strictly isolated Google accounts, real-time quota tracking, and workspace sandboxing.

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/UI-WPF%20XAML-0078D4?style=flat&logo=windows)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![Pattern: MVVM](https://img.shields.io/badge/Pattern-MVVM-10B981?style=flat)]()
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D7?style=flat&logo=windows)](https://microsoft.com/windows)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Author: RifkyA911](https://img.shields.io/badge/Author-RifkyA911-blueviolet?logo=github)](https://github.com/RifkyA911)

---

## 💡 Overview

Google's **Antigravity CLI (`agy`)** stores its OAuth tokens, history, and configuration inside the host user's home directory (`~/.gemini/antigravity-cli`). Running multiple CLI windows simultaneously on different Google accounts typically causes:

- **Auth Collisions**: One account's session overwrites the other's OAuth token.
- **Lock Contention**: Concurrency errors on `~/.gemini/antigravity-cli/presence/*.lock`.
- **Quota Clashes**: Inability to parallelize tasks across independent subscription quotas.

**Agy Account Swarm** solves this cleanly by virtualizing environment variables (`USERPROFILE` and `HOME`) to dedicated per-worker sandboxes (`~/.gemini-profiles/{id}`). Each profile operates with its own credentials, settings, and conversation logs, enabling **true parallel multi-agent execution**.

---

## ✨ Features

- **🛡️ Strict Environment Isolation**: Each profile points to its own sandbox (`~/.gemini-profiles/{id}`) with independent tokens, brain logs, and history files.
- **🌐 Bilingual Multi-Language Support**: Seamless instant switching between **English (EN - Default)** and **Bahasa Indonesia (ID)**.
- **📊 Real Telemetry & Tall Multi-Mode Charts**:
  - Direct timestamp parsing from local `history.jsonl` (no synthetic dummy data, authentic zero-baselines).
  - Multi-mode rendering: **Bar Chart**, **Line Chart**, and **Area Chart** with gradient fills.
  - Interactive multi-dimensional filters: **Account Selector**, Timeframe (*24h*, *3d*, *7d*, *14d*, *30d*, *90d*, *All Time*), Model, and Tier.
  - Expanded 340px tall telemetry canvas in `/analytics`.
- **📥 Native PDF Report Download**:
  - One-click PDF export using Microsoft Edge headless vector rendering engine.
  - Generates comprehensive executive summaries with metrics, inline SVG charts, and per-profile activity breakdowns.
- **⏱️ Configurable Auto-Sync Telemetry**:
  - Periodic background telemetry and MCP polling (1 min, 5 min, 15 min, 30 min, or manual).
- **🧩 Model Context Protocol (MCP) Manager**:
  - Live inspection and management of local MCP tool servers (`context7`, `filesystem`, etc.).
  - Automatic tool discovery and schema inspection.
- **🧠 Full Model Support & Pro Tier Detection**:
  - Automatic Pro badge assignment for authenticated Google accounts (e.g. `rifkyakhmad911@gmail.com`).
  - Supports `claude-3-opus`, `claude-3.5-sonnet`, `claude-3.7-sonnet`, `gemini-2.5-pro`, `gemini-2.5-flash`, `gemini-1.5-pro`, and `gpt-4o`.
- **🚨 Quota Exhaustion Alerts & Audio Synthesizer**:
  - High-visibility warning banner and synthesized acoustic alarm when an account reaches 100% daily quota.
  - In-memory synthesized audio tones for clicks, launches, sync droplet chimes, and soft welcome purrs.
- **🐱 Fluid Cat Welcome Animation**:
  - Elegant 3-second animated vector cat greeting on startup with satisfying fluffy purr chime.
- **🐝 Flexible Swarm Launch Arrangements**:
  - **Split Panes**: Auto-arranges parallel workers into a tiled matrix inside a single Windows Terminal.
  - **Separate Tabs**: Spawns workers as distinct tabs in Windows Terminal.
  - **Separate Windows**: Spawns decoupled windows for multi-monitor setups.
- **📖 Interactive In-App & Browser Documentation (`/docs`)**:
  - Centered cards with built-in guides covering Architecture, Swarm Workflow, Database DDL/DML, and MCP integration.
  - "🌐 Open Interactive Spec in Browser" opens rich dark-themed HTML documents with client-rendered Mermaid.js flowcharts and SQL schemas.
- **🌓 Theme & System Tray**:
  - Instant toggle between Dark Mode and high-contrast Light Mode.
  - Background tray integration (Minimize to Tray and Close to Tray).

---

## 🏗️ System Architecture

```mermaid
graph TD
    App["Agy Account Swarm (WPF .NET 9)"]
    Storage["ProfileStorageService (%APPDATA%/AgyAccountSwarm)"]
    Launcher["TerminalLauncherService"]
    Auth["AuthDetector & TelemetryService"]
    MCP["McpService"]
    
    App --> Storage
    App --> Launcher
    App --> Auth
    App --> MCP
    
    Launcher --> WT["Windows Terminal (wt.exe)"]
    Launcher --> PS["PowerShell"]
    Launcher --> CMD["Command Prompt"]
    
    WT --> W1["Worker 1 (Main Profile)"]
    WT --> W2["Worker 2 (worker-alpha)"]
    
    W1 --> Dir1["~/.gemini/ (Default Credentials)"]
    W2 --> Dir2["~/.gemini-profiles/worker-alpha/.gemini/"]
```

---

## 🚀 Getting Started

### Prerequisites
- Windows 10 / 11 (64-bit)
- [.NET 9.0 Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) (or [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0))
- [Antigravity CLI (`agy`)](https://antigravity.google/docs/cli) installed and in `PATH`
- *(Recommended)* [Windows Terminal](https://aka.ms/terminal) for split-pane matrix orchestration

### Running from Published Output
Pre-built executable binaries are located in the `publish/` directory:
```powershell
& "D:\Works\Project\C#\agy-cli-account-swarm\publish\AgyAccountSwarm.exe"
```

### Building from Source
```powershell
# Clone the repository
git clone https://github.com/RifkyA911/agy-cli-account-swarm.git
cd agy-cli-account-swarm

# Build solution
dotnet build AgyAccountSwarm.sln -c Release

# Run application
dotnet run --project AgyAccountSwarm.csproj
```

---

## 📖 In-App Documentation

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) / [`docs/html/architecture.html`](docs/html/architecture.html): Deep-dive into process isolation and environment virtualization.
- [`docs/SWARM_WORKFLOW.md`](docs/SWARM_WORKFLOW.md) / [`docs/html/swarm_workflow.html`](docs/html/swarm_workflow.html): Step-by-step launch state machine and terminal tiling.
- [`docs/MCP_GUIDE.md`](docs/MCP_GUIDE.md) / [`docs/html/mcp_guide.html`](docs/html/mcp_guide.html): Model Context Protocol configuration and tool discovery.
- [`docs/DATABASE_CONFIG.md`](docs/DATABASE_CONFIG.md) / [`docs/html/database_config.html`](docs/html/database_config.html): SQLite database DDL/DML schemas, telemetry tables, and configuration options.

---

## 🤝 Open Source & Contributions

Contributions, bug reports, and feature requests are very welcome!  
Feel free to open an issue or pull request:

- **Repository**: [https://github.com/RifkyA911/agy-cli-account-swarm](https://github.com/RifkyA911/agy-cli-account-swarm)
- **Author**: [RifkyA911](https://github.com/RifkyA911)

---

## 📄 License

This project is licensed under the [MIT License](LICENSE) - Copyright (c) 2026 RifkyA911.
