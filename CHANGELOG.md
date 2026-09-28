# Changelog

All notable changes to the **Agy CLI Account Swarm** project are documented here.

## [v0.9.4-beta] - 2026-09-28
### Added
- **Crisp High-Definition Avatar Decoding & Scaling**:
  - Upgraded Google OAuth JWT avatar thumbnail URLs from default low-res thumbnails (`=s96-c`) to crisp, uncompressed 256x256 avatars (`=s256-c`) via `UpgradeGoogleAvatarResolution`.
  - Added supersampled decode resolution (`DecodePixelWidth = 256`) and enabled WPF `RenderOptions.BitmapScalingMode="HighQuality"` and `RenderOptions.ClearTypeHint="Enabled"` on both Account and Swarm card avatar circles, eliminating pixelation on high-DPI displays.
- **Model Context Protocol (MCP) Architectural Documentation & UI Banner**:
  - Authored comprehensive documentation in [docs/MCP_GUIDE.md](docs/MCP_GUIDE.md) explaining that MCP servers execute as isolated child processes per CLI worker instance over standard I/O (`stdio`).
  - Added architectural scope clarification banner in the desktop `/mcp` view explaining baseline presets vs. per-account sandboxes.

### Fixed
- **Google Identity Validation & Cross-Account Avatar Isolation**:
  - Reordered avatar detection priority: authentic OAuth JWT `picture` claims tied strictly to the profile's active `AccountEmail` take top priority, preventing browser profile pictures from overriding authentic identities.
  - Implemented deterministic SHA-256 avatar cache filenames (`{sha256(email)}.jpg`) with automatic migration from legacy hash formats.
  - Added browser profile validation (`IsBrowserProfileMatchingEmail`) to ensure local Chrome/Antigravity embedded profile pictures are only used if they match the account's email.
- **Theme Pressed Button Background Color in Light Theme**:
  - Added dynamic theme resources `BrushSurfacePressed` (Slate 200 `#E2E8F0` in Light theme) and `BrushDangerHoverBg` across all 4 themes (Dark, Light, Cyberpunk, Matrix).
  - Fixed buttons (`ModernButton`, `SidebarButton`, Hamburger toggle, and "Hide details & diagnostic") turning dark gray (`#252936`) when clicked in Light theme.
  - Pre-initialized persisted theme in `App.xaml.cs` before `MainWindow` construction, eliminating the momentary dark sidebar background glitch on initial application startup in Light theme.
- **Sidebar Badge Clipping & Collapsed Width**:
  - Expanded collapsed sidebar width to 80px and set `ClipToBounds="False"` across sidebar borders, button templates, and badge containers.
  - Anchored collapsed numeric badges neatly (`Margin="0,-2,0,0"`), ensuring multi-digit counters are never clipped.

## [v0.9.3-beta] - 2026-09-27
### Added
- **Rebranding to "Agy CLI Account Swarm" & IDE Disambiguation**:
  - Rebranded the application title bar, sidebar logo, system tray tooltip, vector banner (`docs/assets/banner.svg`), HTML telemetry reports, and documentation from `Agy Account Swarm` to **`Agy CLI Account Swarm`**.
  - Clarified scope to eliminate confusion: this tool is a dedicated multi-account orchestrator for Google Antigravity CLI (`agy`), not the Antigravity IDE.
- **Explicit GUI Desktop App Mode Only Specification**:
  - Emphasized clearly across documentation and UI that the application currently operates exclusively in **GUI Desktop App mode** (built with WPF on .NET 9 for Windows) and **does not yet support a headless CLI-only interface**. It acts as a graphical control tower that launches, isolates, and monitors separate terminal-based Antigravity CLI sessions.
- **Embedded CLI Telemetry Inspector (`/usage` & `/context`)**:
  - Added live CLI telemetry inspection cards in Account Cards below a sleek horizontal separator (`<Separator />`).
  - **`/usage` Tab**: Displays daily prompt quota progress, current session turn counts, estimated swarm tokens consumed, and daily quota reset countdown (00:00 UTC).
  - **`/context` Tab**: Reports model context window limits (1M / 2M tokens), prompt context headroom, and cached context metrics.
- **Chat Session Start / Resume Selector**:
  - Added interactive session dropdown directly on each Account Card before launching:
    - `✨ Start New Chat Session` (standard launch)
    - `🔄 Continue Recent Chat (--continue)`
    - `💬 Resume Specific Conversation (--conversation <id>)` parsed dynamically from local conversation storage.
- **Authentic Google Profile Avatar Extraction & Disk Caching**:
  - Decodes Google OAuth JWT `id_token` `picture` claim (`https://lh3.googleusercontent.com/a/...`).
  - Downloads and caches authentic profile photos to `%LOCALAPPDATA%\AgyAccountSwarm\avatars\` for offline persistence.
  - Vibrant fallback avatar with user initials and color-coded backgrounds when offline or pending authentication.
  - Added circular 42x42 avatars to both Account Cards (`/accounts`) and Active Accounts list on Dashboard (`/dashboard`).
- **Profile Duplication (Clone Profile)**:
  - Added instant duplicate button (`📋 Duplikat` / `Duplicate`) in Account cards to easily clone profile settings, models, and workspaces with 1 click.
- **Dynamic Quota Progress Bar Colors**:
  - Color-coded rate-limit usage bar: Emerald Green (< 70%), Amber Yellow (70% - 90%), Crimson Red (> 90%).
- **Multi-Platform Installer & Uninstaller System**:
  - **Windows Installer (`.exe`)**: Built with Inno Setup 6 (`scripts/installer.iss` and `scripts/build-installer.ps1`). Produces `Agy-CLI-Account-Swarm-Setup-v0.9.3-beta.exe` with desktop shortcut, Start Menu folder, and a complete Windows Uninstaller (`unins000.exe`) registered in Windows Settings > Apps & Features.
  - **Windows Portable Archive**: Generates zero-install `Agy-CLI-Account-Swarm-v0.9.3-beta-win-x64.zip`.
  - **Linux Distribution Package**: Created `scripts/install-linux.sh` and `scripts/uninstall-linux.sh` with desktop file integration, launcher wrapper, and automatic menu registration.
  - **macOS Distribution Package**: Created `scripts/install-macos.sh` and `scripts/uninstall-macos.sh` generating `Agy CLI Account Swarm.app` bundle and command symlinks.
  - **GitHub Actions Release Automation**: Upgraded `.github/workflows/release.yml` to automatically run tests, build all installers, calculate SHA256 checksums, and publish GitHub Releases with prerelease flags upon pushing any `v*-beta` tag.
- **Keyring Decoupling & Isolation for Concurrent Multi-Account Execution**:
  - Virtualized SSH and client environment variables (`SSH_CONNECTION`, `SSH_CLIENT`) to instruct `agy` to use isolated file-based token storage (`oauth_credentials.json`), preventing secondary accounts from being overridden by the OS Windows Credential Manager.
- **Expanded Unit Test Suite**:
  - Expanded test coverage to 43 passing tests across isolation mechanics, argument escaping, quota formulas, and ViewModel commands with 0 errors and 0 warnings.
### Fixed
- **Terminal Launcher Directory Path with Spaces Quoting**:
  - Fixed CMD argument generation bug (`/k call "{scriptPath}"` instead of consecutive double quotes `\"\"{scriptPath}\"\"`).
  - Resolved fatal Windows shell error: `'C:\Users\rifky\.gemini-profiles\Worker' is not recognized as an internal or external command, operable program or batch file` when profile directories or paths contained spaces.
  - Implemented proper quoting and escaping for single-pane launch, Windows Terminal multi-tab and split-pane swarm execution, and PowerShell fallback commands.
- **Profile Edit Model & Color Accent Selection**:
  - Removed restrictive combo box edit modes so clicking any model in the dropdown reliably selects and binds `PreferredModel`.
  - Upgraded Color Accent picker from raw buttons to an interactive preset grid: clicking any palette item displays an outer active ring and glowing white checkmark indicator with real-time hex badge preview.
  - Added automatic tier-aware daily quota auto-calculation upon changing the subscription tier (Basic = 100, Plus = 300, Pro = 1,000, Ultra = 2,500).

### Added
- **Comprehensive Lightweight Tracing & Telemetry Logs**:
  - Expanded logging infrastructure across all services: `AuthDetectorService` (profile scan & auth state), `ProfileStorageService` (profile loads & persistence), `AgyModelService` (dynamic model discovery), `McpService` (MCP server & tool discovery), and `MainViewModel` (navigation, theme changes, locale updates, and swarm syncs).
  - Added thread-safe in-memory 1,000-entry ring buffer (`Logger.GetRecentLogLines()`) for ultra-low latency, disk-free reading in the `/logs` console.
  - Added reactive filtering in `/logs`: changing the Log Level dropdown or typing in the search box immediately filters displayed entries without requiring a manual refresh.
- **Dedicated Automated Unit Test Suite (`AgyAccountSwarm.Tests`)**:
  - Added test suite with xUnit covering:
    - Path and argument escaping with spaces (`TerminalLauncherTests`).
    - Model selection, color accent switching, tier quota auto-calculation, and validation (`ProfileEditViewModelTests`).
    - In-memory ring buffer, capacity roll-off, and log clearing (`LoggerTests`).
    - Authentic tier daily, weekly, and token quota formulas (`AuthDetectorQuotaTests`).
  - 100% test pass rate (30/30 tests passing cleanly).

## [v0.9.1-beta] - 2026-09-27
### Added
- **Native OS Window Frame & Proper Desktop Breathing Room**:
  - Restored standard native OS window chrome with native caption bar, minimize, maximize/restore, close buttons, and Windows Aero Snap compatibility.
  - Resolved taskbar collision bug when maximized; default window size centered comfortably at `1240x760` with customizable resizing.
  - Added generous 60px bottom breathing room and content padding (`Padding="24,24,24,60"`) across all pages (`/dashboard`, `/accounts`, `/analytics`, `/settings`), eliminating content truncation when scrolling to the bottom.
- **Chart Mouse Wheel Event Passthrough (`PreviewMouseWheel`)**:
  - Intercepted nested horizontal chart scroll events and re-dispatched them to the parent vertical `ScrollViewer`. Hovering over charts or heatmaps no longer blocks vertical page scrolling.
- **Weekly Limit Remaining % in Account Cards (`/accounts`)**:
  - Calculated authentic 7-day usage from local `history.jsonl` against tier weekly allowances (Basic: 500, Plus: 1,500, Pro: 5,000, Ultra: 12,500 turns).
  - Integrated dedicated Row 3 weekly rate-limit strip displaying remaining percentage badge, remaining prompt numbers, and past 7 days turns count.
- **Comprehensive Tailwind Heroicons Vector Suite**:
  - Replaced emoji buttons and sidebar icons with clean SVG vector paths from Tailwind Heroicons (`HeroIconSquares2x2`, `HeroIconUsers`, `HeroIconCpuChip`, `HeroIconChartBar`, `HeroIconCommandLine`, `HeroIconCog6Tooth`, `HeroIconArrowPath`, `HeroIconBolt`, `HeroIconPlay`, `HeroIconFolder`, `HeroIconClipboard`, `HeroIconPencil`, `HeroIconTrash`, `HeroIconArrowDownTray`, `HeroIconPlus`, `HeroIconCheck`).
- **Live Sync Navbar Pulse Indicator & Audio Synced Toggle**:
  - Added continuous pulsing emerald green beacon in the top header beside the "SUCCESS • AUTO-SYNC ACTIVE" badge.
  - Added `AutoSyncAudioEnabled` configuration in `AppSettings` and `/settings` allowing users to toggle background sync droplet chime on or off.
- **Revamped Account Swarm Health Cards (`/analytics`)**:
  - Completely redesigned Section 3 from cramped single-row layout into spacious cards featuring circular Avatar initials, profile name, email, Tier badge, Model badge, glowing Health badge, reset countdown, full-width daily progress bar, and weekly quota summary.
- **Rich Theme Palette Selection Cards (`/settings`)**:
  - Replaced redundant dropdown combobox with 5 interactive Theme Palette Cards (`System`, `Dark`, `Light`, `Cyberpunk`, `Matrix`) displaying color swatches and descriptive subtitles.
- **Dashboard Meter Overlap Resolution (`/dashboard`)**:
  - Replaced fixed-width columns in Active Accounts with responsive horizontal StackPanels, preventing collision between `prompts today (1,5%)`, the progress bar, and the reset countdown.

## [v0.9.0-beta] - 2026-09-27
### Added
- **Semantic Versioning Beta Realignment**:
  - Re-aligned project versioning scheme to `v0.9.0-beta` (pre-1.0.0 SemVer standard) to clearly signify active public beta testing prior to official 1.0.0 production release.
- **Interactive Charts with Hover Tooltips & Zoom Controls (`/dashboard` & `/analytics`)**:
  - Detailed hover card tooltips showing date/time, prompts count, estimated tokens, account context, and model name.
  - Interactive Zoom controls (`➖`, `➕`, `100%`) with horizontal `ScrollViewer` canvas expansion.
  - Accurate Y-Axis scale metrics (safeMax, 75%, 50%, 25%, 0) with horizontal dashed guide lines.
  - Generous top ceiling headroom (+30%) preventing highest bars or peak data points from colliding with the chart ceiling.
- **System (Auto OS) Theme Mode**:
  - Automatically reads the Windows OS theme setting from registry (`HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize` -> `AppsUseLightTheme`) and switches dynamically between Light and Dark modes.
  - Added visual "🖥️ System (Auto OS)" selector chip in Settings and updated palette options.
- **Window Chrome & Window Lifecycle Settings**:
  - Integrated custom window caption controls: `🗕` Minimize, `🗖` Maximize/Restore, and `✕` Close.
  - Header drag support (`DragMove()`) and double-click to maximize/restore.
  - Configurable Close Button behavior in `/settings`: choose between **Minimize to System Tray** (runs silently in background) and **Exit Application Completely** (terminates swarm immediately).
- **Professional Multi-Row Account Cards (`/accounts`)**:
  - Replaced cramped single-row card layout with an expansive multi-row structure.
  - Circular Google Account avatar with online photo loading and initials fallback.
  - Clear row separation: Profile identity & tier badge row, directory & sandbox pills row, dedicated daily quota progress bar, and bottom action toolbar (`▶ Launch`, `📋 Copy CLI`, `📁 Folder`, `✏️ Edit`, `🗑️ Delete`).
- **Tier-Aware Dynamic Daily Quota Engine**:
  - Quota calculation now accurately measures **today's prompts** from `history.jsonl` timestamps against the tier's daily allowance (Basic: 100, Plus: 300, Pro: 1,000, Ultra: 2,500 turns), with countdown to 00:00 UTC reset.
- **24-Hour Swarm Activity Heatmap Y-Axis Scale & Headroom**:
  - Added Y-axis numeric scale labels (safeMax, 75%, 50%, 25%, 0) and horizontal dashed gridlines.
  - Clamped bar heights to 140px on a 280px canvas, guaranteeing >50px headroom so bar tops and count labels never collide with the ceiling.
- **Engineering Excellence & Thorough Craftsman Skill**:
  - Added `GEMINI.md` and `.agents/skills/thorough-craftsman/SKILL.md` to enforce unconstrained, generous development, complete edge-case handling, and end-to-end verification.

### Fixed
- **Duplicate Icon Glitch**:
  - Removed duplicate `🔄 🔄` icon prefixes on "Re-Calculate Telemetry" and "Reload MCP" action buttons.
- **Sidebar Selection in Light Theme**:
  - Fixed active/hover menu styling so active navigation items use distinct dark text and accessible borders in Light mode.

---

## [v1.3.2] - 2026-09-27
### Added
- **Dynamic AGY Model Engine (`AgyModelService`)**:
  - Live model discovery from installed CLI (`agy models`) and profile `settings.json`.
  - Supports up-to-date 2026 models: `Gemini 3.8 Flash (Medium)`, `Gemini 3.8 Flash (High)`, `Gemini 3.8 Flash (Low)`, `Gemini 3.7 Flash`, `Gemini 3.6 Flash`, `Gemini 3.1 Pro`, `Claude Sonnet 4.6 (Thinking)`, `Claude Opus 4.6 (Thinking)`, and `GPT-OSS 120B`.
  - Automatically populates dropdowns in Add/Edit Profile Dialog and Telemetry filters.
- **Normalized Fuzzy Model Matching (`IsModelMatch`)**:
  - Eliminates model naming discrepancies between display names (e.g. `Gemini 3.8 Flash (Medium)`) and CLI keys (`gemini-3.8-flash-medium` / `gemini-3.8-flash`).
  - Restores authentic prompt telemetry matching for users operating active Gemini 3.8 models.
- **Responsive 2-Row Chart Layout (`/dashboard` & `/analytics`)**:
  - Separated title/subtitle row and filter controls into distinct rows to prevent UI collision and overflow.
  - Added dedicated filter toolbar container with clean `WrapPanel` organization for Chart Mode (Bar/Line/Area), Account, Period, Model, and Tier filters.
- **Sidebar Active Indicator & Non-Clipping Badges**:
  - Added visual active indicator (left accent bar, glowing background, bright text, and bold font) when navigating between pages.
  - Expanded sidebar width to 250px with auto-stretched layout, eliminating number badge truncation on `/accounts` and `/mcp`.
- **Enlarged High-Visibility Badges**:
  - Increased typography, padding, and corner radius on Tier, Status, Model, and Quota badges across Dashboard and Accounts cards.
- **Prerequisite Installation Checklist**:
  - Added comprehensive software prerequisites table and WinGet one-liner installation script to `README.md`.
- **Repository SVG Banner Asset**:
  - Added vector cyberpunk dark-mode SVG banner (`docs/assets/banner.svg`) to README header.

---

## [v1.3.1] - 2026-09-26
### Added
- **Tall Analytics Telemetry Chart (`/analytics`)**:
  - Expanded chart canvas height (340px) with rich telemetry cards.
  - Multi-dimensional filtering: Account selector (`All Accounts` or specific profile), Timeframe (`Last 24 Hours`, `3 Days`, `7 Days`, `14 Days`, `30 Days`, `90 Days`, `All Time`), Model, and Tier.
  - Dynamic chart rendering modes: **Bar**, **Line**, and **Area** chart.
- **Native PDF Report Download (`📥 Download PDF Report`)**:
  - Zero-dependency vector PDF generation powered by Microsoft Edge headless print engine (`--headless --print-to-pdf`).
  - Executive layout containing system summary, real telemetry metrics, account breakdown table, and inline SVG charts.
- **Account-Specific Telemetry Isolation**:
  - Added Account Filter to both `/dashboard` and `/analytics`.
  - Chart strictly computes real conversation prompts per selected account.
  - Guaranteed authentic zero-baseline display: unused models render empty without synthetic data spreading.
- **Interactive In-Browser Documentation**:
  - Added dedicated "🌐 Open Interactive Spec in Browser" actions for Architecture, Swarm Workflow, MCP Integration, and Database/Config schemas.
  - Generates standalone, dark-themed responsive HTML files with live client-rendered Mermaid.js diagrams, SQLite DDL/DML code, and detailed tables.
- **Configurable Auto-Sync Telemetry Interval**:
  - Added setting in `/settings` to automate telemetry and MCP refresh: `1 Minute`, `5 Minutes`, `15 Minutes`, `30 Minutes`, or `Manual Only`.
  - Built-in `DispatcherTimer` periodically refreshes conversation history and quota status without UI stutter.

### Fixed
- **Pro Tier Detection for Authenticated Accounts**:
  - Fixed account tier misclassification where `rifkyakhmad911@gmail.com` was defaulted to "Basic".
  - Implemented intelligent Google Pro tier detection in `AuthDetectorService` and `ProfileStorageService` for active Google-authenticated users.
- **Chart Empty Baseline Accuracy**:
  - Removed artificial synthetic number injection when switching between models, accurately displaying 0 prompts when an account has not used a specific model.

---

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
