# Antigravity CLI (`agy`) Command Reference & Orchestration Contract ⚡

> **Target CLI**: Google Antigravity CLI (`agy`)  
> **Tested Version**: `v1.2.12` (Verified September 2026)  
> **Scope**: Detailed reference of CLI commands, orchestration flags, launcher script generation, and environment contracts.

---

## 📌 1. Verified Antigravity CLI Facts

Before orchestrating `agy`, the following operational characteristics were empirically verified:

| Characteristic | Verified Metric / Behavior |
| :--- | :--- |
| **CLI Binary** | `agy` (Go-compiled executable with embedded Node/V8 and gRPC components) |
| **Zero-Cost Usage Command** | `agy -p "/usage" --output-format json` |
| **Turns Consumed by `/usage`** | **0 turns** (`num_turns: 0`, does not advance agent conversation) |
| **Tokens Consumed by `/usage`** | **0 tokens** (`total_tokens: 0`, input/output/thinking = 0) |
| **History Pollution** | **None** (`conversation_id: ""`, zero records added to `history.jsonl`) |
| **Response Latency** | ±5–8 seconds depending on Google API routing |
| **Default Host Storage** | `%USERPROFILE%\.gemini\antigravity-cli\` |

---

## 🚀 2. CLI Command Invocations Supported by Agy Swarm

Agy Account Swarm executes or prepares `agy` in multiple distinct modes:

### 1. Interactive Swarm Launch (Default)
```bash
agy
```
Starts an interactive terminal session inside the designated workspace. If `--dangerously-skip-permissions` is enabled on the profile, it is automatically passed as:
```bash
agy --dangerously-skip-permissions
```

### 2. Live Telemetry Extraction (`/usage`)
```bash
agy -p "/usage" --output-format json
```
Used by `AgyUsageParser` to fetch active capacity limits without spending tokens or altering user conversation state.
**Sample JSON Response**:
```json
{
  "conversation_id": "",
  "status": "SUCCESS",
  "num_turns": 0,
  "usage": { "total_tokens": 0 },
  "command": {
    "name": "usage",
    "data": {
      "groups": [
        {
          "name": "Gemini Models",
          "buckets": [
            {
              "id": "gemini-weekly",
              "name": "Weekly Limit Remaining",
              "window": "weekly",
              "remaining_fraction": 0.5702,
              "reset_time": "2026-10-02T02:01:21Z"
            },
            {
              "id": "gemini-5h",
              "name": "Five Hour Limit Remaining",
              "window": "5h",
              "remaining_fraction": 0.9590,
              "reset_time": "2026-09-28T13:12:17Z"
            }
          ]
        },
        {
          "name": "Claude and GPT models",
          "buckets": [
            {
              "id": "3p-weekly",
              "name": "Weekly Limit Remaining",
              "window": "weekly",
              "remaining_fraction": 0.3852,
              "reset_time": "2026-10-02T01:07:40Z"
            },
            {
              "id": "3p-5h",
              "name": "Five Hour Limit Remaining",
              "window": "5h",
              "remaining_fraction": 0.8855,
              "reset_time": "2026-09-28T13:11:39Z"
            }
          ]
        }
      ]
    }
  }
}
```

### 3. Dynamic Model Discovery (`agy models`)
```bash
agy models
```
Used by `IAgyModelService` to discover available models in real-time from the active Google Antigravity CLI without hardcoded model lists. Output lists models supported for your tier (`gemini-3.8-flash-high`, `gemini-3.7-flash`, `claude-sonnet-4-6`, `claude-opus-4-6-thinking`, `gpt-oss-120b-medium`).

### 4. Verified Reasoning Effort Flag
The CLI supports the `--reasoning-effort` parameter:
```bash
agy -m gemini-3.8-flash-high --reasoning-effort high -p "Your prompt"
```
> [!IMPORTANT]
> Empirical verification confirms that Antigravity CLI strictly supports `low`, `medium`, and `high`. Values like `xhigh` or `max` are rejected with exit code 1.

### 5. Context Capacity Probe (`/context`)
```bash
agy -p "/context" --output-format json
```
Extracts model context limits (e.g. 1M or 2M tokens for Gemini Pro/Flash), cached token counts, and current prompt headroom.

### 6. Resume Most Recent Session
```bash
agy -p "/continue"
```
Bypasses session initialization and continues the most recent conversation transcript in the active workspace.

### 7. Resume Specific Conversation by ID
```bash
agy --conversation 7b9a528e-59c4-42b7-a36b-6772a441e8f2
```
Instantly loads a selected conversation transcript and trajectory history from the sandbox's `brain/` directory.

### 8. Sandbox Terminal Shell (CLI-Only Mode)
```bash
# Windows
run-agy.cmd --cli-only

# Linux / macOS
./run-agy.sh --cli-only
```
Drops the developer directly into an isolated shell with all sandbox environment variables configured, ready for custom scripting, batch commands, or manual `agy` prompts.

---

## 📜 3. Generated Sandbox Launcher Scripts

To ensure terminal processes maintain complete sandboxing regardless of how they are invoked, `TerminalLauncherService` generates launcher scripts inside each profile's root directory:

### Windows Batch Launcher (`run-agy.cmd`)
```cmd
@echo off
setlocal
chcp 65001 >nul 2>&1
set "USERPROFILE=C:\Users\username\.gemini-profiles\worker-alpha"
set "HOME=C:\Users\username\.gemini-profiles\worker-alpha"
set "ANTIGRAVITY_APP_DATA_DIR=C:\Users\username\.gemini-profiles\worker-alpha\.gemini\antigravity-cli"
set "JETSKI_APP_DATA_DIR=C:\Users\username\.gemini-profiles\worker-alpha\.gemini\antigravity-cli"
set "SSH_CONNECTION=1"
set "SSH_CLIENT=1"

cd /d "D:\Works\Projects\App"

if /i "%~1"=="--cli-only" goto :cli_only

agy --dangerously-skip-permissions %*
goto :eof

:cli_only
echo =======================================================
echo  Agy Sandbox Shell [Profile: Worker Alpha]
echo =======================================================
cmd.exe /k
```

> [!NOTE]
> Notice the linear label branching (`if /i "%~1"=="--cli-only" goto :cli_only`). This avoids Windows batch parenthesis-parsing crashes (`]] was unexpected at this time`) when profile names contain brackets or parentheses (e.g. `Default (Main Account)`).

### POSIX Shell Launcher (`run-agy.sh`)
```bash
#!/usr/bin/env bash
# AGY Sandbox Shell - Generated by Agy Account Swarm
export USERPROFILE="$HOME/.gemini-profiles/worker-alpha"
export HOME="$HOME/.gemini-profiles/worker-alpha"
export ANTIGRAVITY_APP_DATA_DIR="$HOME/.gemini-profiles/worker-alpha/.gemini/antigravity-cli"
export JETSKI_APP_DATA_DIR="$HOME/.gemini-profiles/worker-alpha/.gemini/antigravity-cli"
export SSH_CONNECTION=1
export SSH_CLIENT=1

cd "$HOME/workspace" || exit 1

if [ "$1" = "--cli-only" ]; then
    echo "[AGY Sandbox Shell - Profile: Worker Alpha]"
    exec "${SHELL:-bash}"
else
    exec agy --dangerously-skip-permissions "$@"
fi
```

---

## 🌐 4. Environment Variable Contract

The following environment variables govern how the Antigravity CLI resolves identity, storage, and keyring access:

| Variable | Target Scope | Value in Sandbox | Impact on CLI |
| :--- | :--- | :--- | :--- |
| `USERPROFILE` | Windows User Root | `~/.gemini-profiles/{id}` | Directs root file discovery to sandbox. |
| `HOME` | POSIX User Root | `~/.gemini-profiles/{id}` | Directs POSIX path discovery to sandbox. |
| `ANTIGRAVITY_APP_DATA_DIR` | CLI Config Root | `~/.gemini-profiles/{id}/.gemini/antigravity-cli` | Anchors conversation DB, brain transcripts, and logs. |
| `JETSKI_APP_DATA_DIR` | Core Runtime Data | `~/.gemini-profiles/{id}/.gemini/antigravity-cli` | Anchors runtime caches and telemetry. |
| `SSH_CONNECTION` | Network Environment | `1` *(Omitted for Main Profile)* | **Keyring Decoupler**: Forces CLI to store OAuth tokens in local files rather than Windows Credential Manager. |
| `SSH_CLIENT` | Network Client | `1` *(Omitted for Main Profile)* | Secondary indicator enforcing file-based token isolation. |
| `TERM` | Terminal Type | `dumb` *(During background checks)* | Disables ANSI animation loops for headless parsing. |
| `CI` | Continuous Integration | `1` *(During background checks)* | Prevents CLI from spawning interactive spinners or update prompts. |

---

## 🛡️ 5. Default Profile Special Exception

The **Default (Main Account)** profile requires special handling:
- Its profile path points to the host user's actual `%USERPROFILE%`.
- `SSH_CONNECTION` and `SSH_CLIENT` are **explicitly cleared** (`set "SSH_CONNECTION="`).
- This allows the main account to seamlessly utilize the developer's existing Windows Credential Manager session without triggering unneeded reauthentication.
