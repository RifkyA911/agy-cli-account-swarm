# Changelog

All notable changes to the **Agy Account Swarm** project are documented here.

## [v1.3.0] - 2026-09-26
### Added
- **Multi-Language Support**: Complete English (Primary) and Bahasa Indonesia (ID) runtime localization.
- **Model Context Protocol (MCP) Manager (`/mcp`)**: Native scanner and manager for tool servers like `context7` and `filesystem`.
- **In-App Documentation (`/docs`)**: Interactive reference covering system architecture, swarm workflow, and configuration layout.
- **Dynamic Chart Modes**: Toggle between **Bar Chart**, **Line Chart**, and **Area Chart** with gradient fills.
- **Claude 3 Opus Support**: Added `claude-3-opus`, `claude-3.5-sonnet`, `gemini-2.5-pro`, and `gemini-1.5-pro` model options.
- **Real Telemetry Parser**: Direct timestamp and turn parser for `history.jsonl` (no synthetic dummy data).
- **Fluffy Cat Greeting Startup Overlay**: 3-second animated welcome screen with synthesized acoustic purr.
- **About & Open Source Credits**: GitHub link (`https://github.com/RifkyA911`) and MIT license metadata.

### Fixed
- **Unauthenticated Tier Badge**: Changed unauthenticated accounts from "Pro" to "Pending Login" / "Basic".
- **Unauthenticated Model Display**: Shows "Not Connected (Login Required)" instead of hardcoded default model.
- **Settings Layout**: Centered card layout for cleaner desktop presentation.
- **Icon Update**: Updated binary and taskbar icon to `favicon.ico`.

---

## [v1.2.0] - 2026-09-26
### Added
- **Dedicated Sidebar Navigation**: 5 distinct views (`/dashboard`, `/accounts`, `/analytic`, `/logs`, `/settings`).
- **Sound Synthesizer**: In-memory synthesized WAV audio feedback for clicks, launches, success, and quota warnings.
- **Quota Exhaustion Alerts**: High-visibility banner and audio alarm when an account hits quota threshold.
- **Light Theme Color Sync**: Complete dynamic brush system eliminating white-on-white text glitches.

### Fixed
- **UseShellExecute Environment Issue**: Migrated variable injection to `run-agy.cmd` avoiding .NET runtime exception.

---

## [v1.1.0] - 2026-09-26
### Added
- **Swarm Launch Modes**: Split Panes, Separate Tabs, and Separate Windows.
- **Theme Switcher**: Dark and Light theme toggle.
- **System Tray Integration**: Minimize and close to background tray.

---

## [v1.0.0] - 2026-09-26
### Added
- Initial release with profile management and directory sandboxing.
