# Changelog

All notable changes to the **Agy Account Swarm** project are documented here.

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
