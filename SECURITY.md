# Security Policy

## Supported Versions
| Version     | Supported          |
| ----------- | ------------------ |
| v0.9.x-beta | :white_check_mark: |
| < v0.9.0    | :x:                |

---

## Threat Model & Security Architecture

Agy CLI Account Swarm operates as a local Windows desktop orchestrator for Antigravity CLI sessions. Its security architecture relies on the following boundaries:

### 1. Token & Credential Protection At-Rest
- **Windows DPAPI**: Cached OAuth tokens (`antigravity-oauth-token`) are encrypted at-rest using Windows Data Protection API (`crypt32.dll` with `CurrentUser` scope).
- **Decoupled Keyring**: Sandboxed workers run with virtualized environment variables (`SSH_CONNECTION=1`, `SSH_CLIENT=1`) to prevent secondary profiles from writing to or reading from the host Windows Credential Manager.
- **Log & Diagnostic Redaction**: Sensitive patterns (JWT tokens, OAuth access/refresh tokens, Bearer headers) are automatically masked by `Logger.RedactSensitive` before reaching memory ring buffers or disk log files.

### 2. Command & Argument Injection Defense
- **Windows Terminal (`wt.exe`)**: Profile names, session arguments, and workspace paths are sanitized against command-chaining semicolons (`;`) and special delimiter characters.
- **PowerShell Execution**: Invocations avoid raw interpolated script blocks and instead use Base64-encoded UTF-16 commands (`-EncodedCommand`) to eliminate parsing ambiguity and quote escaping vulnerabilities.
- **Command Prompt (`cmd.exe`)**: All parameters are wrapped with strict character sanitization (`SanitizeBatchString`) removing `&`, `|`, `<`, `>`, `%`, `^`, and backticks.

### 3. Path Traversal & Sandbox Isolation
- **Sanitized Directory Names**: Profile folder names are stripped of traversal sequences (`..`, `/`, `\`) and invalid filesystem characters.
- **Canonicalization**: Paths are canonicalized via `Path.GetFullPath()`. Profiles cannot be configured to point to the Windows root, system directory, or system drive root.
- **Safe Sandboxing**: Each isolated profile is confined to `%USERPROFILE%\.gemini-profiles\{safe_name}`.

### 4. Telemetry Provenance
- Telemetry data is parsed strictly from local filesystem logs (`history.jsonl`) and local CLI execution (`agy -p "/usage" --output-format json`) with a defensive parser. No untrusted remote telemetry sources or synthetic injections are accepted.

---

## Reporting a Vulnerability

If you discover a security vulnerability or credential leakage risk:
1. Please do **NOT** open a public issue.
2. Contact the maintainer directly through GitHub private security advisories or via `https://github.com/RifkyA911`.
3. Provide reproduction steps and environment details.
4. We will evaluate and patch the issue promptly.
