# Troubleshooting & Diagnostic Guide 🩺

> **Target Version**: v0.9.7-beta+  
> **Diagnostic Tool**: Built-in **Profile Doctor** & Terminal Launcher Diagnostics

---

## 📌 1. Quick Diagnostic Checklist

Before troubleshooting manually, run an automated diagnosis:
1. Open **Agy CLI Account Swarm**.
2. Click the **🔄 Quick Sync** button on the problematic account card header, or open the **Profile Doctor** audit.
3. Review the 5 checkpoint results (CLI Binary, OAuth Freshness, Process Locks, Workspace Trust, Usage Schema).
4. If a 1-click fix is available (such as `🧹 Clear Stuck Lock(s)`), click it directly.

---

## 🔍 2. Common Issues & Proven Solutions

### 1. `agy: The term 'agy' is not recognized as the name of a cmdlet`
- **Symptom**: Spawning an account terminal opens a window that immediately prints command not found and exits.
- **Root Cause**: The Google Antigravity CLI binary directory is not registered in your Windows system `PATH` environment variable.
- **Solution**:
  1. Open **Settings** in the desktop application.
  2. In the **Custom Antigravity CLI Executable Path** field, click **Browse** and locate `agy.exe` (or `agy.cmd`). Typical locations:
     - `C:\Program Files\Google\Antigravity\agy.exe`
     - `%APPDATA%\npm\agy.cmd`
     - `%LOCALAPPDATA%\Programs\antigravity-cli\bin\agy.exe`
  3. Click **Save Settings**. The application will now use this absolute path for all launches and background telemetry queries.

---

### 2. `keyringAuth: failed to load stored token: failed to unmarshal token: invalid character '\ufeff'`
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
  1. Inspect the profile's `run-agy.cmd`:
     Ensure lines 6–7 contain:
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

### 8. Blurry or Fuzzy Text on High-DPI Monitors (125%, 150%, 200%)
- **Symptom**: Fonts and card borders appear soft or blurry on 4K or high-resolution laptops.
- **Root Cause**: WPF's default vector text layout engine using non-subpixel antialiasing.
- **Solution**: Modern versions enforce subpixel ClearType rendering and integer coordinate snapping across all views:
  ```xml
  RenderOptions.ClearTypeHint="Enabled"
  TextOptions.TextFormattingMode="Display"
  TextOptions.TextRenderingMode="ClearType"
  ```
  Ensure your Windows display scaling is set to an integer increment (100%, 150%, 200%) for optimal sharpness.

---

### 9. Windows Terminal (`wt.exe`) Not Found
- **Symptom**: Clicking **Launch Swarm** produces error: `Windows Terminal (wt.exe) could not be located`.
- **Root Cause**: Windows Terminal is not installed (common on older Windows 10 LTSC editions) or the App Execution Alias is disabled.
- **Solution**:
  1. Go to **Settings** in the desktop application.
  2. Change **Preferred Terminal Host** from `Windows Terminal` to `PowerShell` or `Command Prompt`.
  3. Or install Windows Terminal via winget:
     ```powershell
     winget install Microsoft.WindowsTerminal
     ```

---

### 10. Trailing Whitespace in Profile Paths
- **Symptom**: `mkdir C:\Users\user \.gemini: The system cannot find the path specified`.
- **Root Cause**: Accidental trailing space when naming a profile.
- **Solution**: Fully guarded in v0.9.6+ via `SanitizeFolderName()` and automatic `.Trim()` before invoking `Directory.CreateDirectory`.
