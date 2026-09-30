# Security, Credential Isolation & Sandboxing Architecture 🛡️

> **Document Version**: 2.0  
> **Status**: Verified Production Standard  
> **Scope**: Credential Segregation, Keyring Decoupling, Secret Redaction, and Filesystem Sandboxing in Agy CLI Account Swarm.

---

## 📌 1. Threat Model & Sandboxing Objectives

The Antigravity CLI (`agy`) was natively engineered for a single developer operating under one host user account. Running multiple accounts simultaneously presents severe security and isolation risks:

| Security Threat | Impact Without Sandboxing | Swarm Mitigation Standard |
| :--- | :--- | :--- |
| **Credential Manager Overwrite** | Secondary account login overwrites primary Google OAuth token in Windows Credential Manager. | **Keyring Decoupling**: Virtualized SSH environment variables force file-based token isolation per sandbox. |
| **Token Corruption via BOM** | Windows UTF-8 text writers inject `\ufeff` Byte Order Mark, causing Go JSON unmarshaler crashes. | **BOM-Free UTF-8 Pipeline**: Tokens are written as pure UTF-8 without BOM; auto-healing detects and strips BOMs. |
| **Secret Leakage in Logs** | Raw OAuth JWTs, API keys, or access tokens leak into desktop logs or crash dumps. | **Memory & Log Redaction**: `DataProtectionService` automatically scrubs tokens and Authorization headers. |
| **Path Traversal / Escape** | Malicious or accidental profile paths write into `C:\Windows` or drive root. | **Filesystem Boundaries**: Strict path validation blacklists system folders and forces fallback sandboxes. |
| **Host Account Pollution** | Cloned profile accidentally claims "Default Profile" status and edits host config. | **Default Profile Protection**: Strict heuristics prevent any clone from inheriting host root access. |

---

## 🔑 2. Operating System Keyring Decoupling

Under normal execution on Windows, `agy` delegates OAuth token storage to the **Windows Credential Manager** (`gemini:antigravity`). Because Windows Credential Manager is scoped to the entire Windows user account, every terminal instance shares the exact same keyring target.

### The Decoupling Mechanism:
The Antigravity CLI binary contains a remote fallback check: when it detects that it is executing over an SSH session, it bypasses the OS GUI keyring and falls back to a discrete file-based token stored in:
```
%USERPROFILE%\.gemini\antigravity-cli\antigravity-oauth-token
```

Agy Account Swarm exploits this behavior non-invasively by injecting virtual network indicators into worker process environments:
```cmd
set "SSH_CONNECTION=1"
set "SSH_CLIENT=1"
```
Because `%USERPROFILE%` is redirected to `~/.gemini-profiles/{id}`, the CLI:
1. Skips the Windows Credential Manager entirely.
2. Writes and reads its OAuth token exclusively from `~/.gemini-profiles/{id}/.gemini/antigravity-cli/antigravity-oauth-token`.
3. Leaves the developer's primary Google account in Windows Credential Manager completely intact.

### Default Main Account Exception:
For the primary **Default (Main Account)** profile, `TerminalLauncherService` explicitly unsets these variables:
```cmd
set "SSH_CONNECTION="
set "SSH_CLIENT="
```
This ensures the host developer account functions naturally with Windows Credential Manager without prompting for re-login.

---

## 📄 3. Pure UTF-8 Token Preservation (Zero-BOM Standard)

The Antigravity CLI is compiled from Go source code. Go's standard `encoding/json` library strictly adheres to RFC 8259, which mandates that JSON text must NOT begin with a Byte Order Mark (BOM).

### The Bug & Self-Healing Fix:
When Windows utilities or older versions of the desktop orchestrator wrote or re-encrypted `antigravity-oauth-token`, a UTF-8 BOM (`\ufeff`) was introduced. When `agy` attempted to start, it crashed with:
```
keyringAuth: failed to load stored token: failed to unmarshal token: invalid character '\ufeff' looking for beginning of value
```

### Modern Architecture:
1. `AuthDetectorService` and `ProfileDoctorService` read and write tokens using `new UTF8Encoding(false)` (explicitly disabling BOM generation).
2. A self-healing migration interceptor inspects token files on startup: if `\ufeff` is detected, it strips the 3-byte prefix and re-saves the raw JSON immediately:
```csharp
if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
{
    var cleanJson = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
    File.WriteAllText(tokenPath, cleanJson, new UTF8Encoding(false));
}
```

---

## 🔒 4. Data Protection & Secret Redaction (`DataProtectionService`)

All logging and internal diagnostics pass through `DataProtectionService` to guarantee that secrets are never exposed in plaintext:

### A. Real-Time Regex Redaction
Before any log line or terminal buffer is written to `app.log` or rendered in the `/logs` GUI view, regex patterns sanitize:
- **Bearer Tokens**: `Bearer [A-Za-z0-9_\-\.]{20,}` ➔ `Bearer [REDACTED]`
- **Google OAuth JWTs**: `eyJ[A-Za-z0-9_\-]{10,}\.eyJ[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]+` ➔ `[REDACTED_JWT]`
- **Google API Keys**: `AIza[0-9A-Za-z-_]{35}` ➔ `AIza[REDACTED_API_KEY]`
- **OAuth Refresh Tokens**: `1//[0-9A-Za-z-_]{30,}` ➔ `1//[REDACTED_REFRESH_TOKEN]`

### B. DPAPI Protected Auxiliary Storage
For internal credential persistence (such as custom API keys or encrypted profile metadata), `DataProtectionService` uses the Windows **Data Protection API (DPAPI)**:
```csharp
byte[] encrypted = ProtectedData.Protect(
    plaintextBytes, 
    optionalEntropy, 
    DataProtectionScope.CurrentUser
);
```
Data encrypted with `CurrentUser` scope can only be decrypted by the exact same Windows user account on that specific workstation.

---

## 🛡️ 5. Default Profile Protection & Clone Safeguards

Cloning an account (`📋 Duplicate`) creates a separate sandbox with duplicate preferences. To prevent catastrophic overrides:
- `IsMainDefaultProfile()` verifies that cloned profiles with names like `Default (Main Account) (Copy)` or `Copy of Primary` are **strictly barred** from inheriting host profile paths.
- Clones are automatically redirected to `~/.gemini-profiles/{id}`.
- Host delete actions are disabled in the GUI for any profile identified as primary default.

---

## 📁 6. Filesystem Traversal Guard

When an account specifies a `CustomProfilePath`:
1. It is sanitized via `Path.GetFullPath(Environment.ExpandEnvironmentVariables(path))`.
2. Paths matching system roots or critical operating system directories are rejected:
   - Root drive: `C:\`
   - Windows root: `C:\Windows`
   - System directory: `C:\Windows\System32`
3. If an invalid or dangerous path is detected, the runtime falls back automatically to the secure sandbox directory: `%USERPROFILE%\.gemini-profiles\safe_profile\`.
