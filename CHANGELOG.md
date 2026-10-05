# Changelog

All notable changes to the **Agy CLI Account Swarm** project are documented here.

## [v0.9.11-beta] - 2026-10-05
### Added & Changed
- **Dedicated 1-on-1 Personal Chat Studio (`/personal-chat`)**:
  - Implemented interactive single personal chat GUI directly connected to the Google Antigravity CLI (`agy`).
  - Native multi-turn conversational memory via `agy --conversation <conversation_id> -p "<prompt>" --output-format json`, maintaining full context across turns without history loss.
  - Per-profile session isolation stored in `%APPDATA%\AgyAccountSwarm\personal_chats\{profileId}\` with automatic history persistence and fast JSON loading.
  - Live session management: create new chat sessions (`+ New Chat`), delete sessions, and export sessions to clean Markdown documents.
  - Accurate token & execution duration telemetry per turn (e.g. `1,420 tokens • 2.5s • gemini-2.5-pro`) displayed directly within assistant message bubbles.
  - Integrated model selector (`gemini-2.5-pro`, `gemini-2.5-flash`, `claude-3-7-sonnet`, `claude-3-5-sonnet`, `claude-3-5-haiku`, `gpt-4o`, `o3-mini`, `o1`) and reasoning effort selector (`low`, `medium`, `high`).
- **High-Performance Rust-Powered Project RAG (`arag-cli`) & Multi-Tier Fallback**:
  - Zero heavy external dependencies (no LangChain, ChromaDB, or Python overhead; ~0 MB idle memory footprint).
  - Integrates directly with pre-indexed local Rust `arag-cli.exe` binary (`~/.cargo/bin/arag-cli.exe` or system PATH).
  - High-speed hybrid & BM25 keyword search (1-9ms latency) querying indexed knowledge bases (e.g. `agy-swarm`).
  - Seamless automatic fallback pipeline: Hybrid Vector/BM25 Search -> BM25 Keyword Search -> Hermetic local Markdown paragraph scanner (`docs/` and root `*.md`).
  - Transparent RAG chunk badges in assistant responses showing Chunk ID, source knowledge base, keyword matches, and relevance score percentages.
  - Augmented prompt synthesis cleanly injecting project documentation into the prompt without corrupting user intent.
- **Real-Time Swarm Task Status Ingestion**:
  - Live monitoring of active swarm worker tasks and message bus streams via native .NET 9 file streaming of `.swarm/tasks.json` and `.swarm/bus.jsonl`.
  - Live Swarm Task Status banner in Personal Chat header with one-click manual telemetry refresh.
  - Checkbox toggle (`Inject Live Swarm Tasks`) in the chat input toolbar to dynamically include real-time task counts, execution progress, and worker assignments in prompt context.
- **Account Card Direct Shortcuts & UI Cleanliness Standards**:
  - Added dedicated `💬 Personal Chat` outline button on every account card in the Accounts view (`AccountsView`), allowing users to jump directly into a 1-on-1 session bound to that specific account sandbox.
  - Separated Real-Time Swarm & Personal Chat header toolbars into dedicated secondary rows with generous spacing, preventing layout crowding.
  - Enforced strict icon cleanliness guidelines across all views: replaced all download icons with document/report icons (`HeroIconDocumentText`).
- **Expanded Hermetic Test Suite (167/167 Passed)**:
  - Added `tests/AgyAccountSwarm.Tests/PersonalChatAndRagTests.cs` verifying RAG prompt augmentation, session JSON persistence, token telemetry formatting, and swarm task telemetry parsing.
  - 100% green test pass rate (167 passed, 0 failed, 0 skipped).
- **Clean Release Publishing**:
  - `dotnet build` and `dotnet publish -c Release -o publish` completed with zero warnings and zero errors.

## [v0.9.10-beta] - 2026-10-05
### Added & Changed
- **Ergonomic Window Height Calibration & High-DPI Scaling Fix**:
  - Calibrated default window dimensions to `Width="1240" Height="660"` with `MinWidth="1000" MinHeight="500"`, establishing a balanced visual viewport that eliminates taskbar crowding on 1080p and 1440p displays while preserving ample space for multi-card rosters.
  - Fixed Windows Avalonia physical-pixel vs device-independent-pixel (DIP) scaling bug where `Screen.WorkingArea.Height` was multiplied by the display scale factor (125%-150%) instead of dividing by `screen.Scaling`, inflating the window height beyond the viewport.
  - Window loaded clamp logic now cleanly prevents screen bottom overflow (`usableHeightDips - 40`) without forcibly expanding window height.
  - Sized secondary modal dialogs comfortably (`ProfileEditWindow`: 620x520, `ImportChatWindow`: 680x500) and enabled window resizing (`CanResize="True"`).
- **Agent Skills Hub Ownership & Multi-Scope Discovery**:
  - Enhanced `Models/SkillItem.cs` with explicit ownership and accessibility metadata: `OwnerTitle`, `AccessibleBy`, `ScopeCategory`, `OwnerIcon`, and `OwnerBadgeColor`.
  - Upgraded `Services/SkillService.cs` discovery engine to scan per-profile custom skill directories (`~/.gemini-profiles/{id}/skills`) in addition to built-in skills, workspace skills (`.agents/skills/`), and plugin extensions.
  - Added visual ownership badges and access scope pills directly onto Skill Cards and within the comprehensive Skill Details modal inspector.
- **Optional Background Periodic Swarm Audio (`AutoSyncAudioEnabled`)**:
  - Decoupled manual sync button sound from automatic periodic background sync.
  - Suppressed tactile click sounds during background periodic synchronizations.
  - Introduced `AutoSyncAudioEnabled` setting (default: `false` / silent) to ensure background sync cycles run completely unobtrusively unless the user explicitly opts in.
  - Updated localization strings in English and Indonesian to clarify the setting behavior.
- **Artifacts & Dead Workspace Pruning**:
  - Reclaimed over 805 MB of disk space by pruning obsolete build output folders (`publish-crossplatform/`) and legacy release archives (`dist/`).
  - Purged intermediate test-host dependencies and transient testing DLLs from the production release directory, ensuring `publish/` contains only the clean, isolated flagship `AgyCliAccountSwarmGUI.exe` bundle.
- **Real-Time Chat Studio Architecture Documentation**:
  - Added thorough architectural documentation detailing the purpose of the Real-Time Chat Studio: human-in-the-loop steering, inter-agent timeline streaming from `.swarm/bus.jsonl`, shared blackboard synchronization, and per-profile direct conversations.
- **100% Hermetic Test Suite & Verification**:
  - Updated unit test assertions in `AvaloniaParityAndPendingLoginTests.cs` matching the calibrated dimensions (1240x660).
  - Maintained 100% green test pass rate (162/162 passed, 0 failures, 0 warnings, 0 errors).

## [v0.9.9-beta] - 2026-10-04
### Added & Changed
- **100% Comprehensive Bilingual Localization (Indonesian 🇮🇩 & English 🇬🇧)**:
  - Eliminated all hardcoded English strings across the entire user interface and modal dialogs.
  - Fully expanded dictionary in `Services/LocalizationService.cs` with over 150+ bilingual dictionary keys covering navigation sidebar, metrics, sentinels, tabs, cards, tables, charts, dialogs, and splash screens.
  - Seamless instant live language switching between Indonesian and English at runtime without requiring an application restart.
  - Perfect Avalonia compiled binding compliance within nested `DataTemplate` scopes using `$parent[Window].((vm:AvaloniaMainViewModel)DataContext).Strings[...]`.
- **Dedicated Real-Time Chat Studio (`/realtime-chat`) & Profile Shortcuts**:
  - Introduced dedicated full workspace for real-time human-in-the-loop conversation and inter-agent communication.
  - Added direct profile shortcut button `💬 Chat Realtime` on each account card in the Accounts view to instantly navigate into real-time session chat.
  - Integrated channel tabs, quick tips, interactive blackboard viewer, and message feed streaming from `.swarm/bus.jsonl`.
- **Authentic Live Activity Telemetry ("dia lagi ngapain")**:
  - Real-time command tracking and live output stream chips showing the exact command and execution output of each worker.
  - Authentic telemetry parsing with zero synthetic fake data, strictly respecting `GEMINI.md` guidelines.
- **Antigravity Skills Management Hub**:
  - Implemented `Models/SkillItem.cs` and `Services/SkillService.cs` with multi-source skill discovery (builtin, plugins, workspace skills).
  - Added interactive Skill Details modal inspector with markdown preview and schema information.
- **Swarm Audio Feedback & Sonic Sentinel Engine**:
  - Integrated `Services/AudioService.cs` for synthesized audio alerts on swarm lifecycle events: worker dispatch, swarm completion, quota exhaustion alarms, and warnings.
  - Configurable audio toggle and test sound action in Settings.
- **Header Split-Button Precision Height Alignment**:
  - Locked top-right header `🚀 Launch Swarm` split-button height to an exact `36px` to maintain strict visual alignment with adjacent controls.
- **Expanded Test Suite (162/162 Passed)**:
  - Added hermetic unit tests in `SkillsAndThemesSettingsTests.cs` validating skill item parsing, theme dictionaries, and settings persistence.
  - Test suite expanded to 162 passing tests (0 failures, 0 skipped, 100% hermetic).
- **Zero Warnings & Clean Release Publish**:
  - Verified 0 warnings and 0 errors across `dotnet build` and `dotnet publish -c Release -o publish`.

## [v0.9.8-beta] - 2026-10-04
### Added & Changed
- **Consolidated Single Solid Executable (`AgyCliAccountSwarmGUI.exe`)**:
  - Full migration to Avalonia UI as the single production desktop application, published directly to `publish\AgyCliAccountSwarmGUI.exe`.
  - Archived legacy WPF codebase as pseudo-prototype only. Removed obsolete executable files from publish folder.
- **Rendering & Avatar Crispness Overhaul**:
  - Replaced `Ellipse.Fill` + `ImageBrush` with anti-aliased clipped borders (`CornerRadius` + `ClipToBounds="True"`) and `RenderOptions.BitmapInterpolationMode="HighQuality"`.
  - Implemented non-locking image loading using memory streams in `PathToBitmapConverter`, preventing file lock collisions.
- **Concurrent Swarm Orchestrator Refinement**:
  - Reorganized Launch Mode selection and Launch/Stop Swarm actions into a dedicated controls row with clear visual separation.
- **Live Swarm Chat & Inter-Agent Bus Upgrades**:
  - Integrated profile avatars directly into message bubbles.
  - Added distinct role badge styling (System, User/Commander, Architect, Implementer, Reviewer, Security) with color-coded foreground, background, and border converters.
- **Real-Time Swarm Workflow Topology Canvas**:
  - Implemented high-contrast dot-grid mesh background brush (`MeshGridBrush`).
  - Added smooth mouse drag-to-move hand panning and horizontal mouse-wheel scrolling across the node topology graph.
- **Logging Subsystem Complete Overhaul**:
  - Daily rotating log files on disk (`agyswarm_YYYY-MM-DD.log`) under `%APPDATA%\AgyAccountSwarm\logs\`.
  - Structured log parser (`LogEntryItem`) with color-coded badges for `INFO`, `DEBUG`, `WARN`, `ERROR`, and `SUCCESS`.
  - Real-time search filter and level filter dropdown (`ALL`, `INFO`, `DEBUG`, `WARN`, `ERROR`, `SUCCESS`).
  - Added `Clear Buffer` and `Prune Files (>7d)` maintenance actions.
  - Modernized Excel export to prompt the user for target file destination via `SaveFilePickerAsync`.
- **System Settings & Tray Hardening**:
  - Added interactive `Browse...` file picker (`OpenFilePickerAsync`) for Antigravity CLI binary executable (`agy.exe`).
  - Fixed minimize-to-tray toggle persistence (`Mode=TwoWay` and settings deserialization restore).
- **Startup Auto-Sync & Visual Loading State**:
  - Automatically triggers background swarm synchronization on application startup without terminal console flicker.
  - Added rotating keyframe spin animation (`Path.spin`) to the navbar Sync Swarm button with clear loading indicator.
- **Documentation & Visual Tour Expansion**:
  - Overhauled `README.md` with an extensive 11-menu visual screenshot tour, comprehensive technical architecture breakdown, and data flow diagrams.
  - Removed outdated versioning and release instructions.

## [v0.9.7-beta] - 2026-10-03
### Added & Changed
- **Avalonia Flagship Edition & WPF Archived as Pseudo Prototype**:
  - Promoted Avalonia cross-platform UI (`AgyAccountSwarm.Avalonia`) to the primary active production application for Windows, Linux, and macOS.
  - Formally archived the legacy Windows Presentation Foundation (WPF) edition as a reference pseudo prototype (`[Archived Prototype — Use Avalonia Edition]`).
  - Aligned window dimensions to exact 1:1 parity: `Width="1240" Height="760"` (min `1050 × 640`).
- **High-Resolution Avatar Rendering & Google CDN Upgrade**:
  - Eliminated pixelated/blurry profile pictures across Dashboard, Accounts, Task & Dispatch, and Swarm Worker cards.
  - Upgraded Google profile photo CDN resolution from low-res `=s96-c` to crisp `=s384-c` Super Retina.
  - Enabled `RenderOptions.BitmapInterpolationMode="HighQuality"` globally across all avatar `Image` and `Ellipse` elements.
  - Implemented self-healing cache re-downloading to replace stale low-res thumbnails (< 12KB) with crisp high-res profile photos.
- **Active Fleet Engine Layout & Connected Accounts Collision Fix**:
  - Replaced unconstrained horizontal stack with a responsive `WrapPanel` featuring `TextWrapping="Wrap"` and ample column separation (`Margin="0,0,16,0"`).
  - Multi-account rosters now wrap cleanly without overlapping or colliding into Pool Capacity, Burn Rate, or Status badges.
- **Executive Telemetry Reports - Dedicated Action Toolbar Row**:
  - Transformed the report export card from a cramped single-row layout into a generous multi-row card structure.
  - Created a dedicated action toolbar row housing `Download PDF Report`, `Export to Excel (.xlsx)`, and `Refresh Telemetry` with generous breathing room.
  - Hardened PDF export stylesheet with high-contrast `#334155` headers, `#ffffff` print paper backgrounds, exact color preservation, and `--no-pdf-header-footer` Edge print flags.
- **Robust Abort Fleet & Stop Worker Lifecycle with Responsive Button States**:
  - Added dynamic `IsEnabled="{Binding CanAbortFleet}"` and `IsEnabled="{Binding CanStop}"` to prevent user confusion and dead clicks.
  - Enhanced `AbortFleetAsync` in `FleetDispatcherService` to reliably terminate all active, dispatched, and detached worker process trees.
  - Prominently captures and highlights `RESOURCE_EXHAUSTED` / `quota exceeded` / `rate limit` errors in headless silent mode, setting worker status to red `"Quota Exceeded"` and `CurrentActivity` to `"🚨 Quota Exceeded / Rate Limit"`.
  - Added pre-dispatch quota warnings informing users when selected accounts have exhausted daily allowances before launch.


### Added
- **Fleet Prompt Dispatcher &amp; Git Worktree Orchestration [EXPERIMENTAL]**:
  - Orchestrate multiple authentic `agy` CLI accounts simultaneously from a single high-level prompt objective without LangChain or LangGraph dependencies.
  - **Native Git Worktree Isolation**: Automatically provisions dedicated worktree directories per worker on unique timestamped branches (`swarm/{worker}_{timestamp}`), completely eliminating `.git/index.lock` collisions while sharing Git commit object history.
  - **Pre-Flight Sentinel &amp; Robustness Matrix**:
    - *Fallback Handling*: Gracefully detects non-Git workspaces and falls back to isolated per-worker sandbox workspaces.
    - *Out-of-Resources (OOR) Guard*: Evaluates disk headroom and GC memory limits before launch. Blocks dispatch when free disk < 2.0 GB; issues active warnings if < 5.0 GB.
    - *Staggered Process Launch*: Enforces a 600ms stagger between worker terminal spawns to prevent thread contention and token spikes.
    - *Process Tree Abort*: Cleanly terminates entire process trees (`proc.Kill(entireProcessTree: true)`) to prevent orphaned background instances.
  - **Flexible Prompt Synthesizer**: Supports 3 distinct operational modes:
    - `RoleTailored`: Synthesizes persona-specific roles (Architect, Implementer, QA/Reviewer, Security Auditor).
    - `Consensus`: Dispatches divergent implementations (Approach A, Approach B, Independent Reviewer) for competitive consensus.
    - `Broadcast`: Dispatches identical task instructions across all selected workers.
  - Interactive UI controls across both WPF and Avalonia with live branch tracking, worktree pruning (`git worktree prune -v`), and 1-click branch merging.
  - Authored comprehensive documentation in [docs/FLEET_DISPATCHER_SPEC.md](docs/FLEET_DISPATCHER_SPEC.md) and interactive in-app guide [docs/html/fleet_dispatcher.html](docs/html/fleet_dispatcher.html).
- **Full Avalonia UI 12 Cross-Platform Architecture (`AgyAccountSwarm.Avalonia`)**:
  - Implemented the cross-platform UI frontend targeting Linux (Wayland / X11) and macOS via Avalonia UI and .NET 9.
  - Complete custom window chrome with draggable titlebar, maximize/restore toggle with double-click support, and minimize/close controls.
  - Multi-row responsive layout with sidebar navigation, active page highlighting (`ActiveNavBgConverter`, `ActiveNavFgConverter`), and real-time swarm status pulse indicator.
  - Accounts management view with tier filter combo, search bar with `PlaceholderText`, card-based profile list, primary default badge, single-account on-demand sync button (`🔄 Sync`), and dual authentic quota bars (Gemini Models weekly limit &amp; Claude/GPT Models weekly limit).
  - Swarm Fleet multiplexer view with window layout selector (split-pane, separate tabs, separate windows), worker fleet selection card with checkboxes (`IsSelectedForSwarm`), select all / deselect all controls, and dynamic 1-click Swarm launch/stop lifecycle.
  - Real-time application logs viewer with automated refresh and Excel export (`.xlsx`).
  - Documentation view linking to all 9 HTML architecture specifications.
  - Full model and profile edit dialog (`ProfileEditWindow`) with `--dangerously-skip-permissions` toggle, model dropdown, subscription tier selection, and sandbox directory configuration.
  - Audio feedback service hardened with `OperatingSystem.IsWindows()` platform guards to prevent runtime crashes on Linux and macOS environments.


## [v0.9.7-beta] - 2026-09-29
### Added
- **Integrated In-Popup Conversation Search**:
  - Replaced the external card-row search textbox with a clean, searchable dropdown popup.
  - Clicking the conversation selector opens a dropdown popup with a search input at the top (`HeroIconMagnifyingGlass`, live text filter, and [x] clear button) and a pixel-smooth scrollable list of filtered sessions below it.
- **Dedicated `--dangerously-skip-permissions` Toggle**:
  - Added an explicit toggle card in `ProfileEditDialog` with badge `--dangerously-skip-permissions`.
  - Automatically appends `--dangerously-skip-permissions` to launcher scripts, PowerShell/Cmd execution, and CLI snippets when enabled.
- **Cross-Platform Architecture Roadmap & Linux/macOS Assessment**:
  - Authored [docs/CROSS_PLATFORM_AVALONIA_ROADMAP.md](docs/CROSS_PLATFORM_AVALONIA_ROADMAP.md) and companion in-app documentation [docs/html/cross_platform.html](docs/html/cross_platform.html) (TAB 7 in Documentation view).
  - Detailed feasibility assessment answering whether macOS is safe/issue-free (honest answer: NO, due to missing WPF runtime on Darwin, lack of Windows Terminal, Apple Keychain differences, and Gatekeeper notarization requirements).
  - Detailed the Avalonia UI 11+ migration roadmap for native Linux (Wayland / X11) and macOS Metal.
  - Automatically generates cross-platform POSIX launcher script (`run-agy.sh`) in every account sandbox alongside `run-agy.cmd`.

### Fixed
- **Cloned Profile Session Reset & Persistence Loss in Swarm Launch**:
  - Diagnosed and resolved the root cause of OAuth token reset on cloned accounts: `AuthDetectorService` previously attempted to encrypt `antigravity-oauth-token` at rest using Windows DPAPI with UTF-8 BOM (`\ufeff`), which corrupted the token for the Go-compiled `agy` CLI binary (`keyringAuth: failed to load stored token: failed to unmarshal token: invalid character '\ufeff'`).
  - Completely removed destructive DPAPI encryption of `antigravity-oauth-token`, preserving pure valid UTF-8 JSON without BOM as required by `agy`.
  - Implemented automatic self-healing migration in `AuthDetectorService` that detects and restores any previously scrambled DPAPI/BOM tokens on disk, ensuring 100% login persistence across Swarm launches.
  - Sanitized profile titles and working directories in Windows Terminal split-pane and new-tab arguments (`LaunchSwarmAsync`).

## [v0.9.6-beta] - 2026-09-29
### Added
- **Dynamic Swarm Process Lifecycle (Launch & Stop Swarm)**:
  - Track active background terminal PIDs spawned by Swarm execution.
  - Dynamically toggles topbar and dashboard action buttons between `Launch Swarm` (emerald green) and `Stop Swarm` (crimson red), allowing 1-click graceful termination of all active swarm worker processes.
- **Dedicated Single-Account Quick Sync**:
  - Added a compact sync button on every individual Account Card header allowing instant refresh of OAuth authentication status, quota metrics, and doctor health without triggering a full multi-account swarm sync.
- **Interactive Conversation Search & Smooth Scrolling**:
  - Added search input inside the session dropdown menu to easily filter and locate specific past chat sessions.
  - Disabled discrete pixel jumping (`ScrollViewer.CanContentScroll="False"`) on dropdowns for smooth, natural scrolling.
- **Online / Idle Swarm Status Dot Indicator**:
  - Replaced the previous static Google `(G)` badge on avatar pictures with an active status dot: glowing emerald green when actively executing in swarm, and subtle slate gray when idle.
- **Swarm Propagation & Git Worktree Specification**:
  - Authored comprehensive architectural roadmap in [docs/NEXT_FEATURES.md](docs/NEXT_FEATURES.md) and companion [docs/html/next_features.html](docs/html/next_features.html).
  - Designed end-to-end inflow pipeline combining Git Worktree parallelization (shared `.git` object store with zero `.git/index.lock` collisions), master-to-worker config propagation, conversation forking, and dynamic quota failover relay (estafet kuota) in 100% compliance with Google Terms of Service.
- **In-App Documentation TAB 6 (Next Features)**:
  - Added interactive tab inside the desktop `/docs` view outlining Git Worktree architecture, configuration propagation, multi-model fan-out, and failover relay with 1-click browser inspection.

### Fixed
- **Batch Script Parenthesis Parsing Crash & Accidental OAuth Prompts**:
  - Replaced parenthesized block `if "%1"=="--cli-only" (...)` with linear label branching (`if /i "%~1"=="--cli-only" goto :cli_only`), preventing fatal `]] was unexpected at this time.` syntax crashes when titles contained parentheses (e.g. `Default (Main Account)`).
  - Explicitly clear `SSH_CONNECTION` and `SSH_CLIENT` for the primary default profile in both batch and PowerShell launchers, guaranteeing that the default account always reads from Windows Credential Manager and never triggers unwanted OAuth reauthentication prompts.
- **Documentation Header Tabs Collision**:
  - Redesigned the `/docs` navigation bar with a responsive 2-row layout using `WrapPanel`, ensuring that tab buttons never collide with or overflow the page title on narrow viewports.
- **Vector Banner Layout Overlap**:
  - Restructured `docs/assets/banner.svg` hero badges from a cramped horizontal strip into a balanced 2x2 grid with ample horizontal clearance (+300px), eliminating badge collisions with the terminal window mockup.
- **Realistic About Page Language**:
  - Grounded claims in the `/about` view, replacing overconfident "100%" assertions with honest, realistic descriptions of local process sandboxing.
- **Platform Scope Transparency**:
  - Explicitly clarified in `README.md` that native Avalonia GUI is planned for future cross-platform releases, while the current codebase runs on Windows WPF (.NET 9). Headless CLI automation will live in `agy-swarm`.
- **Filter Toolbar Positioning**:
  - Moved the interactive Filter button and popup to the far-left position on the Accounts page toolbar for natural left-to-right visual hierarchy.
- **Zero-Flicker Background Telemetry & Concurrency Serialization**:
  - Serialized background usage checks (`agy -p "/usage"`) using `SemaphoreSlim(1,1)` to prevent CLI race conditions.
  - Suppressed all terminal/shell popup flickers by closing `StandardInput` immediately, running with `CreateNoWindow = true`, `UseShellExecute = false`, and stripping `WT_SESSION` / setting `CI=1` and `TERM=dumb`.
- **Subscription Tier Preservation (Basic vs Pro)**:
  - Fixed an issue where "Basic" tier accounts were erroneously overwritten with "Pro" tier during detection.
- **Collapsed Sidebar Badge Clipping**:
  - Removed left border shifting from collapsed sidebar buttons and refined padding/margins (`Margin="0,-2,-3,0"`), ensuring multi-digit number badges are never clipped at the top-left edge.
- **Dashboard Metric Card Bindings**:
  - Polished and synced all 5 dashboard metric card localization keys (`TOTAL PROFILES`, `AUTHENTICATED`, `SWARM WORKERS`, `SAVED CONVERSATIONS`, `MCP TOOLS & SERVERS`) in both English and Indonesian.
- **Clean Window Title & Version Badges**:
  - Removed redundant `(MIT Open Source)` text from window chrome titles, version pills, and HTML telemetry exports.

## [v0.9.5-beta] - 2026-09-29
### Added
- **Interactive Accounts Filter & Sorting Popup**:
  - Added filter popup in the Accounts page toolbar to filter accounts by Subscription Tier (`Basic`, `Plus`, `Pro`, `Ultra`) and Quota Status/Health (`Authenticated`, `Needs Login`, `Quota Exhausted`, `Warning / High Quota`).
  - Added multi-criteria sorting: `Name (A-Z)`, `Name (Z-A)`, `Quota Used (High to Low)`, and `Quota Used (Low to High)`.
  - Added active filter badge counter on the filter button and 1-click Reset button.
- **Cross-Account Chat History Migration (`Import Chat`)**:
  - Built `ConversationTransferService` and `ImportChatDialog` allowing users to migrate conversation sessions, prompt turns, and transcripts between account sandboxes with 1 click.
  - 100% compliant local file transfer: copies local `history.jsonl`, `conversations/`, and `brain/` logs strictly on disk without copying or sharing Google OAuth credentials.
- **Terms of Service, Google Policies & Risk Advisory Documentation**:
  - Authored comprehensive [docs/TERMS_OF_SERVICE_AND_RISKS.md](docs/TERMS_OF_SERVICE_AND_RISKS.md) covering Google ToS compliance, credential sandboxing, rate limits (5-hour and weekly limits), and disclaimer of affiliation with Google LLC.
- **Explicit GUI Desktop Only Scope & Upcoming 'agy-swarm' CLI Announcement**:
  - Emphasized across `README.md`, documentation, and application dialogs that this project is strictly GUI-only, and that a standalone headless CLI interface will be created in a future dedicated project named **`agy-swarm`**.

### Fixed
- **Terminal / Shell Flicker Suppression**:
  - Removed `AllocConsole()` from `App.xaml.cs` which previously spawned a momentary conhost console window on startup.
  - Enforced `CreateNoWindow = true` and `ProcessWindowStyle.Hidden` across all background telemetry and model discovery processes (`agy -p "/usage"`, `agy models`), providing a completely smooth GUI experience without console popups.
- **Dynamic Tier & Quota by System Session in Profile Editor**:
  - Refactored `ProfileEditDialog.xaml` and `ProfileEditViewModel.cs`: Subscription Tier and Daily Quota are now strictly dynamic, read-only telemetry calculated from the active session rather than user-entered values.
  - Expanded Description input field to 2 rows (56px) with automatic multiline text wrapping.

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
  - Fixed account tier misclassification where authenticated accounts could default to "Basic".
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
