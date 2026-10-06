# Agy CLI Account Swarm v0.9.13-beta

Multiplatform Desktop Orchestrator & Sandbox Session Manager for Google Antigravity CLI (`agy`).

---

### 📦 Official Release Packages (Windows, Linux, macOS | x64 & ARM64)

| Platform | Architecture | Binary / Package | Format | Description |
| :--- | :--- | :--- | :--- | :--- |
| **Windows** | **x64** | `Agy-CLI-Account-Swarm-Setup-v0.9.13-beta.exe` | Inno Setup GUI | Setup wizard with Start Menu / Desktop shortcuts & uninstaller |
| **Windows** | **x64** | `Agy-CLI-Account-Swarm-v0.9.13-beta-win-x64.zip` | Standalone ZIP | Portable archive (extract and launch `AgyCliAccountSwarmGUI.exe`) |
| **Windows** | **ARM64** | `Agy-CLI-Account-Swarm-v0.9.13-beta-win-arm64.zip` | Standalone ZIP | Native ARM64 for Snapdragon X Elite & Surface Pro Copilot+ PCs |
| **Linux** | **x64** | `Agy-CLI-Account-Swarm-v0.9.13-beta-linux-x64.tar.gz` | Tarball + Scripts | Standalone binaries, `.desktop` launcher, and `install.sh` |
| **Linux** | **ARM64** | `Agy-CLI-Account-Swarm-v0.9.13-beta-linux-arm64.tar.gz` | Tarball + Scripts | Native aarch64 build for ARM64 servers, Raspberry Pi 5, & VMs |
| **macOS** | **Apple Silicon** | `Agy-CLI-Account-Swarm-v0.9.13-beta-macos-arm64.tar.gz` | .app Bundle | Pre-packaged `Agy CLI Account Swarm.app` (M1/M2/M3/M4) + `install.sh` |
| **macOS** | **Intel** | `Agy-CLI-Account-Swarm-v0.9.13-beta-macos-x64.tar.gz` | .app Bundle | Pre-packaged `Agy CLI Account Swarm.app` (x86_64) + `install.sh` |
| **Integrity** | — | `SHA256SUMS.txt` | Checksums | Cryptographic SHA256 hashes for binary integrity verification |

📖 **Full Installation Guide**: See [`docs/INSTALLATION.md`](https://github.com/RifkyA911/agy-cli-account-swarm/blob/master/docs/INSTALLATION.md).

---

### 🚀 What's New in v0.9.13-beta

#### 1. Complete Multiplatform Installers & Binaries
- **Windows**: Modern Inno Setup wizard with clean uninstaller registration, per-user installation (`%LOCALAPPDATA%\Programs`) avoiding unnecessary UAC prompts, alongside zero-install portable ZIP archives for both x64 and ARM64.
- **Linux**: Self-contained packages with an automated `install.sh` script that provisions desktop application launchers (`.desktop`), scalable icon registration, and a `/usr/local/bin` / `~/.local/bin` shell wrapper `agy-cli-account-swarm`.
- **macOS**: Native `Agy CLI Account Swarm.app` application bundle structure with automated Gatekeeper quarantine resolution (`xattr -cr`) and CLI launcher symlinking.

#### 2. Re-Engineered Personal Chat Studio (`/chat`)
- **Streamlined 1-Row Control Bar**: Consolidated chat session selector, sandbox account profile picker, live CLI status badge, dynamic model selector, verified reasoning effort (`low`, `medium`, `high`), Markdown export, and session management.
- **Instant Windowed Pagination**: Replaced heavy batch loading with responsive windowed history paging (rendering the 35 most recent messages initially with an on-demand pagination bar for earlier messages), completely eliminating UI thread layout freeze.
- **Seamless RAG Attachments**: Integrated project document indexing (`Attach Doc`) directly in the message composer toolbar, backed by local Rust RAG indexing (`arag-cli`).
- **Authentic Terminal History Continuity**: Seamless 1-click historical conversation sync from `history.jsonl` with full multi-turn `--conversation <id>` context retention.

#### 3. Swarm Workers Event Bus Consolidation (`/dispatcher`)
- Integrated multi-agent real-time bus chat directly into the Swarm Workers hub (`/dispatcher`, Tab 0), aligning worker dispatching, shared architecture blackboard specifications, and live agent telemetry streams.

#### 4. Rigorous Quality Standards
- **170/170 hermetic unit tests passing** (100% green pass rate).
- **Zero build warnings, zero compiler errors**.
- Isolated profile sandboxing with DPAPI encryption and sensitive token redaction.
