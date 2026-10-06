# Troubleshooting & Diagnostic Guide 🩺

> **Target Version**: v0.9.13-beta+  
> **Diagnostic Tools**: Built-in **Profile Doctor**, Terminal Launcher Diagnostics, & In-App Log Viewer (`/logs`)

---

## 📌 1. Quick Diagnostic Checklist

Before troubleshooting manually, perform an automated audit:
1. Open **Agy CLI Account Swarm**.
2. Click the **🔄 Quick Sync** button on the problematic account card header, or open the **Profile Doctor** audit modal.
3. Review the 5 checkpoint results (CLI Binary, OAuth Freshness, Process Locks, Workspace Trust, Usage Schema).
4. If a 1-click remediation is available (such as `🧹 Clear Stuck Lock(s)`), click it directly.
5. Inspect the live application log stream under the **`/logs`** tab for detailed exception traces.

---

## 🔍 2. Common Issues & Solutions

### 1. `agy: The term 'agy' is not recognized as the name of a cmdlet`
- **Symptom**: Spawning an account terminal opens a window that immediately prints command not found and exits.
- **Root Cause**: The Google Antigravity CLI binary directory is not registered in your system `PATH` environment variable.
- **Solution**:
  1. Open **Settings** in the desktop application.
  2. In the **Custom Antigravity CLI Executable Path** field, click **Browse** and locate `agy.exe` (Windows) or `agy` (Linux/macOS).
  3. Typical locations:
     - Windows: `%LOCALAPPDATA%\Programs\antigravity-cli\bin\agy.exe` or `%APPDATA%\npm\agy.cmd`
     - Linux: `/usr/local/bin/agy` or `~/.local/bin/agy`
     - macOS: `/opt/homebrew/bin/agy` or `~/.local/bin/agy`
  4. Click **Save Settings**. The application will now use this absolute path for all launches and background telemetry queries.

---

### 2. `keyringAuth: failed to load stored token: invalid character '\ufeff'`
- **Symptom**: Worker terminal immediately errors out on launch citing an invalid character `\ufeff`.
- **Root Cause**: A Windows text editor or script saved `antigravity-oauth-token` with a UTF-8 Byte Order Mark (BOM). The Go-compiled `agy` CLI binary strictly rejects BOM prefixes in JSON files.
- **Solution**:
  - **Automatic (v0.9.7-beta+)**: `AuthDetectorService` automatically detects and strips `\ufeff` BOM prefixes on startup. Simply click **🔄 Quick Sync** on the account card.
  - **Manual Fix via PowerShell**:
    ```powershell
    $tokenPath = "$HOME\.gemini-profiles\<profile-name>\.gemini\antigravity-cli\antigravity-oauth-token"
    $raw = [System.IO.File]::ReadAllText($tokenPath)
    [System.IO.File]::WriteAllText($tokenPath, $raw, [System.Text.UTF8Encoding]::new($false))
    ```

---

### 3. Batch Script Error: `]] was unexpected at this time`
- **Symptom**: Launching a swarm or sandbox terminal outputs `]] was unexpected at this time.` and closes.
- **Root Cause**: Windows Command Prompt (`cmd.exe`) syntax crash when an `if (...)` block parses command arguments containing parentheses (such as `Default (Main Account)`).
- **Solution**: Handled automatically in v0.9.6-beta+ by replacing parenthesized blocks with linear label branching (`goto :cli_only`). If using an older generated launcher, click **Edit Profile** and save, or re-run `scripts\setup-profile.ps1` to regenerate `run-agy.cmd`.

---

### 4. Secondary Account Logs into the Primary Google Account
- **Symptom**: You authenticate Account 2, but when launching `agy`, it displays the primary account's email or workspace.
- **Root Cause**: The process inherited the host Windows Credential Manager keyring because `SSH_CONNECTION` and `SSH_CLIENT` were missing or cleared.
- **Solution**:
  1. Inspect the profile's `run-agy.cmd` or `run-agy.sh`:
     Ensure lines contain:
     ```cmd
     set "SSH_CONNECTION=1"
     set "SSH_CLIENT=1"
     ```
  2. Verify that the profile name is not set to `Default (Main Account)` or identified as the primary host profile. Only the genuine host profile should have SSH variables cleared.

---

### 5. Lingering Locks: `database is locked` or `Resource temporarily unavailable`
- **Symptom**: CLI hangs on boot or prints SQLite locking errors.
- **Root Cause**: An earlier terminal instance was closed abruptly while actively writing to `presence/*.lock` or `conversations/*.db-journal`.
- **Solution**:
  1. On the affected Account Card, click **🧹 Clear Stuck Lock(s)**.
  2. Alternatively, run the cleanup script from PowerShell:
     ```powershell
     powershell -ExecutionPolicy Bypass -File scripts\clean-sandbox.ps1 -TargetProfile "Worker Alpha"
     ```

---

### 6. Automated Swarm Freezes Waiting for Confirmation
- **Symptom**: Swarm starts, but workers stop making progress on multi-turn tasks.
- **Root Cause**: The Antigravity CLI pauses execution awaiting interactive user confirmation to write files, run terminal commands, or invoke MCP tools.
- **Solution**:
  1. Click **✏️ Edit Profile** on the account card.
  2. Enable the **`--dangerously-skip-permissions`** toggle.
  3. Save. Swarm workers will now bypass confirmation prompts and execute automated workflows autonomously.

---

### 7. Untrusted Workspace Security Modal Halts Execution
- **Symptom**: CLI displays `Do you trust the authors of the files in this folder?` and refuses to accept automated inputs.
- **Root Cause**: The target `DefaultWorkspace` is not registered in the profile's `trusted_workspaces.json`.
- **Solution**:
  - Run **Profile Doctor** on the profile card and click **Register Workspace Trust**.
  - Alternatively, launch the account once interactively (`Launch agy`) and select **Yes, I trust this folder**.

---

### 8. Windows Terminal (`wt.exe`) Not Found
- **Symptom**: Clicking **Launch Swarm** produces error: `Windows Terminal (wt.exe) could not be located`.
- **Root Cause**: Windows Terminal is not installed or the App Execution Alias is disabled.
- **Solution**:
  1. Go to **Settings** in the desktop application.
  2. Change **Preferred Terminal Host** from `Windows Terminal` to `PowerShell` or `Command Prompt`.
  3. Or install Windows Terminal via winget:
     ```powershell
     winget install Microsoft.WindowsTerminal
     ```

---

### 9. macOS: "App is damaged and can't be opened" or "Unidentified Developer"
- **Symptom**: Double-clicking `Agy CLI Account Swarm.app` on macOS produces a security popup preventing launch.
- **Root Cause**: macOS Gatekeeper applies a quarantine flag (`com.apple.quarantine`) to downloaded unsigned archives.
- **Solution**:
  1. Open Terminal and run:
     ```bash
     xattr -cr "/Applications/Agy CLI Account Swarm.app"
     ```
  2. Or run the included installer script (`bash install.sh`) which automatically clears quarantine flags.
  3. Alternatively, open **System Settings ➔ Privacy & Security**, scroll down to **Security**, and click **Open Anyway**.

---

### 10. Linux: Missing Shared Libraries (`libfontconfig.so.1`, `libX11.so.6`)
- **Symptom**: Running `agy-cli-account-swarm` on Linux exits immediately with `DllNotFoundException` or `libfontconfig.so.1: cannot open shared object file`.
- **Root Cause**: Minimal Linux server or container installations lack standard desktop font and display rendering libraries.
- **Solution**:
  - **Ubuntu / Debian**: `sudo apt install libx11-6 libice6 libsm6 libfontconfig1`
  - **Fedora / RHEL**: `sudo dnf install libX11 libICE libSM fontconfig`
  - **Arch Linux**: `sudo pacman -S libx11 libice libsm fontconfig`

---

### 11. Linux: Wayland Display Scaling or Rendering Artifacts
- **Symptom**: Window borders appear misaligned or blurry under Wayland on high-DPI displays.
- **Root Cause**: Fractional scaling friction between Wayland compositor and Skia rendering surface.
- **Solution**:
  - Launch with integer scaling environment variable:
    ```bash
    AVALONIA_SCREEN_SCALE_FACTORS="1" agy-cli-account-swarm
    ```
  - Or force X11 / XWayland mode:
    ```bash
    AVALONIA_PLATFORM=x11 agy-cli-account-swarm
    ```

---

### 12. Personal Chat: Model Selector Empty or Slow to Populate
- **Symptom**: In the `/chat` tab, the dynamic Model selector dropdown is empty or only displays default items.
- **Root Cause**: The application invokes `agy models` asynchronously in the background. If the Google API takes a few seconds or the CLI is busy, dynamic model discovery may be pending.
- **Solution**:
  - The built-in verified fallback catalog (`gemini-3.8-flash-high`, `gemini-3.7-flash`, `claude-sonnet-4-6`, `claude-opus-4-6-thinking`) activates automatically.
  - Verify in your terminal that `agy models` returns valid JSON:
    ```bash
    agy models
    ```
  - Check that an active Google account is authenticated on the selected sandbox.

---

### 13. RAG Document Indexing Not Triggering
- **Symptom**: Clicking `Attach Doc` in Personal Chat selects a file, but responses do not include knowledge context references.
- **Root Cause**: Local Rust RAG CLI (`arag-cli`) is either not installed or document format is not supported.
- **Solution**:
  - Supported document formats: `.md`, `.txt`, `.json`, `.csv`, `.pdf`, `.docx`, `.yaml`.
  - Ensure documents contain extractable text (scanned PDFs without OCR are skipped).
  - Inspect `/logs` tab for `PersonalChatService` ingestion events.
