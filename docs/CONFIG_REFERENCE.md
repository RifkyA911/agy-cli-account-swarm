# Configuration Reference & Persistence Schema ⚙️

> **Directory**: `%APPDATA%\AgyAccountSwarm\`  
> **Schema Version**: 2.0 (v0.9.7-beta+)  
> **Scope**: Authoritative reference for persistent application settings, account profiles, tier allowances, and path resolution rules.

---

## 📌 1. Storage Layout Overview

Agy Account Swarm persists state in the user's roaming AppData directory, with cached media stored in local AppData:

```
%APPDATA%\AgyAccountSwarm\
├── settings.json         <-- Global application settings and UI preferences
├── profiles.json         <-- Array of configured account profiles and sandboxes
└── quota_config.json     <-- Tier allowance thresholds and token budgets

%LOCALAPPDATA%\AgyAccountSwarm\
├── avatars\              <-- Extracted and cached Google profile photos (.png/.jpg)
└── logs\
    └── app.log           <-- Standardized application execution and error logs
```

---

## 🛠️ 2. Global Application Settings (`settings.json`)

The global settings file stores orchestrator preferences:

### Example `settings.json`:
```json
{
  "PreferredTerminal": "WindowsTerminal",
  "SwarmMode": "SplitPanes",
  "CustomAgyExecutablePath": "C:\\Program Files\\Google\\Antigravity\\agy.exe",
  "AutoCheckAuthOnStartup": true,
  "Theme": "Dark",
  "CloseToTray": true,
  "MinimizeToTray": true,
  "SoundEnabled": true,
  "Language": "en",
  "PreferredChartMode": "Bar",
  "AutoSyncInterval": "5 Minutes",
  "AutoSyncAudioEnabled": false
}
```

### Property Definitions:

| Property | Type | Default | Permitted Values / Description |
| :--- | :--- | :--- | :--- |
| **`PreferredTerminal`** | `enum` | `"WindowsTerminal"` | Terminal host to launch CLI workers:<br>• `"WindowsTerminal"` (`wt.exe`)<br>• `"PowerShell"` (`pwsh.exe` or `powershell.exe`)<br>• `"CommandPrompt"` (`cmd.exe`) |
| **`SwarmMode`** | `enum` | `"SplitPanes"` | Swarm window layout mode:<br>• `"SplitPanes"` (tiled panes within single terminal)<br>• `"SeparateTabs"` (individual tabs)<br>• `"SeparateWindows"` (independent detached windows) |
| **`CustomAgyExecutablePath`** | `string?` | `null` | Absolute path to `agy` CLI binary. If `null`, auto-resolved from system `PATH`. |
| **`AutoCheckAuthOnStartup`**| `bool` | `true` | Automatically run `ProfileDoctor` and OAuth JWT validation on boot. |
| **`Theme`** | `string` | `"Dark"` | Application visual theme:<br>• `"Dark"` (Obsidian Dark)<br>• `"Light"` (Daylight Clean)<br>• `"Cyberpunk"` (Cyberpunk Neon)<br>• `"Emerald"` (Matrix Emerald) |
| **`CloseToTray`** | `bool` | `true` | When `true`, clicking the `✕` close button minimizes to the Windows System Tray rather than terminating the process. |
| **`MinimizeToTray`** | `bool` | `true` | Minimizes window to system tray when the minimize button is clicked. |
| **`SoundEnabled`** | `bool` | `true` | Enables click effects, navigation chimes, and quota warning audio synthesizer. |
| **`Language`** | `string` | `"en"` | UI localization: `"en"` (English) or `"id"` (Bahasa Indonesia). |
| **`PreferredChartMode`** | `string` | `"Bar"` | Default analytics visualization mode: `"Bar"`, `"Line"`, or `"Area"`. |
| **`AutoSyncInterval`** | `string` | `"5 Minutes"` | Polling frequency for background telemetry: `"1 Minute"`, `"5 Minutes"`, `"15 Minutes"`, `"30 Minutes"`. |
| **`AutoSyncAudioEnabled`** | `bool` | `false` | Plays a soft synthetic chime upon completing automatic telemetry sync cycles. |

---

## 👥 3. Account Profiles Schema (`profiles.json`)

The profiles configuration defines each isolated account worker:

### Example `profiles.json`:
```json
[
  {
    "Id": "main",
    "Name": "Default (Main Account)",
    "Description": "Host primary Google account",
    "ColorTag": "#3B82F6",
    "CustomProfilePath": null,
    "DefaultWorkspace": "D:\\Works\\Projects\\MainApp",
    "ExtraArguments": "--model gemini-2.5-pro",
    "DangerouslySkipPermissions": false,
    "IsSelectedForSwarm": true,
    "Tier": "Pro",
    "PreferredModel": "gemini-2.5-pro",
    "QuotaLimit": 1000,
    "IsQuotaExhausted": false,
    "CreatedAt": "2026-09-20T10:00:00Z",
    "LastLaunchedAt": "2026-09-30T14:22:15Z"
  },
  {
    "Id": "worker_alpha_7a9c",
    "Name": "Worker Alpha",
    "Description": "Automated code reviewer and refactoring bot",
    "ColorTag": "#10B981",
    "CustomProfilePath": "C:\\Users\\username\\.gemini-profiles\\worker-alpha",
    "DefaultWorkspace": "D:\\Works\\Projects\\BackendApi",
    "ExtraArguments": "",
    "DangerouslySkipPermissions": true,
    "IsSelectedForSwarm": true,
    "Tier": "Plus",
    "PreferredModel": "gemini-2.5-flash",
    "QuotaLimit": 300,
    "IsQuotaExhausted": false,
    "CreatedAt": "2026-09-25T12:00:00Z",
    "LastLaunchedAt": "2026-09-30T15:00:00Z"
  }
]
```

### Profile Property Definitions:

| Property | Type | Description |
| :--- | :--- | :--- |
| **`Id`** | `string` | Unique identifier (e.g. `"main"` for primary host profile, or 32-character GUID). |
| **`Name`** | `string` | Display name of the account profile. |
| **`Description`** | `string` | Optional user description or team assignment note. |
| **`ColorTag`** | `string` | Hex color code for badges and card accents (e.g. `"#3B82F6"`). |
| **`CustomProfilePath`** | `string?` | Custom isolated profile root. If `null`, defaults to `%USERPROFILE%\.gemini-profiles\{SanitizedName}`. |
| **`DefaultWorkspace`** | `string?` | Absolute working directory to launch `agy` in. Path spaces are automatically quoted. |
| **`ExtraArguments`** | `string?` | Custom CLI arguments appended to `agy` executions (e.g. `--model ...`). |
| **`DangerouslySkipPermissions`** | `bool` | When `true`, automatically appends `--dangerously-skip-permissions` to bypass confirmation modals. |
| **`IsSelectedForSwarm`** | `bool` | Whether this profile is active in batch Swarm launches (`Launch Swarm`). |
| **`Tier`** | `string` | Subscription tier badge: `"Basic"`, `"Plus"`, `"Pro"`, `"Ultra"`, or `"Unverified"`. |
| **`PreferredModel`** | `string` | Default model selected in the model picker (e.g. `"gemini-2.5-flash"`, `"gemini-2.5-pro"`). |
| **`QuotaLimit`** | `int` | Daily prompt turns ceiling before warning or exhaustion flags trigger. |
| **`IsQuotaExhausted`** | `bool` | Manually or automatically flagged exhaustion sentinel state. |
| **`CreatedAt`** | `DateTime` | UTC timestamp when profile was created. |
| **`LastLaunchedAt`** | `DateTime?` | UTC timestamp of last terminal launch. |

---

## 📊 4. Tier Allowance Config (`quota_config.json`)

Controls dynamic capacity thresholds per subscription tier:

### Example `quota_config.json`:
```json
{
  "Tiers": {
    "Basic": {
      "DailyPrompts": 100,
      "WeeklyPrompts": 500,
      "DailyTokens": 500000
    },
    "Plus": {
      "DailyPrompts": 300,
      "WeeklyPrompts": 1500,
      "DailyTokens": 1500000
    },
    "Pro": {
      "DailyPrompts": 1000,
      "WeeklyPrompts": 5000,
      "DailyTokens": 5000000
    },
    "Ultra": {
      "DailyPrompts": 2500,
      "WeeklyPrompts": 12500,
      "DailyTokens": 15000000
    }
  }
}
```

---

## 🔒 5. Directory Resolution & Path Security Rules

`AccountProfile.GetEffectiveProfileDirectory()` guarantees security against directory traversal attacks:

1. **Host Profile Safety**: If `IsMainDefaultProfile()` is true, returns the host `%USERPROFILE%`.
2. **System Directory Blacklist**: Prohibits sandbox paths pointing to drive roots (`C:\`), Windows directory (`C:\Windows`), or System directory (`C:\Windows\System32`).
3. **Traversal Prevention**: Automatically strips `..`, `/`, `\`, and invalid path characters via `SanitizeFolderName()`.
4. **Fallback Containment**: Paths that fail resolution fallback safely to `%USERPROFILE%\.gemini-profiles\safe_profile\`.
