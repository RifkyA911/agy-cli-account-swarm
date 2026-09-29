# Terms of Service, Google Policies & Risk Advisory

> **Document Version**: 1.0  
> **Effective Date**: September 2026  
> **Scope**: Agy CLI Account Swarm (GUI Desktop Orchestrator)

---

## 1. Executive Summary & Purpose

**Agy CLI Account Swarm** is an open-source, local developer workstation utility designed to manage, sandbox, and launch multiple authentic instances of Google's **Antigravity CLI (`agy`)**.

This document transparently outlines the operational architecture of Agy CLI Account Swarm, its strict adherence to local file boundaries, how it interacts with Google's cloud services, and the risks and responsibilities assumed by developers utilizing multi-account orchestration.

---

## 2. Disclaimer of Affiliation

> [!IMPORTANT]
> **Agy CLI Account Swarm** is an independent third-party open-source project created by community contributors.  
> It is **NOT** sponsored, affiliated with, maintained, or endorsed by **Google LLC**, **Alphabet Inc.**, or the **Google DeepMind** team.  
> *"Google"*, *"Google Antigravity"*, *"agy"*, *"Gemini"*, and related marks are registered trademarks of Google LLC.

---

## 3. Strict GUI-Only Desktop Scope & Upcoming 'agy-swarm' CLI

> [!WARNING]
> **GUI Desktop Architecture Boundary**:  
> This repository (`agy-cli-account-swarm`) is strictly designed and supported as a **GUI Desktop Application** (Windows Presentation Foundation on .NET 9). It does not provide a headless CLI command runner or daemon inside this codebase.  
> **Upcoming Project**: A standalone, headless terminal-based orchestration interface will be published separately under the dedicated project name **`agy-swarm`**.

---

## 4. Architecture & Policy Compliance

### 4.1. Zero Synthetic Data & Authentic Local Telemetry
- All account telemetry (prompt counts, weekly model quotas, 5-hour limit refresh timers, active models, and context capacities) is parsed strictly from **local user data**:
  - `~/.gemini/antigravity-cli/history.jsonl`
  - `~/.gemini/antigravity-cli/settings.json`
  - Authentic output from `agy -p "/usage" --output-format json` (which consumes **0 turns** and **0 tokens**).
- No synthetic or mock telemetry is generated.
- No telemetry or telemetry logs are ever transmitted to any third-party analytics servers.

### 4.2. Local Sandboxing & Credential Isolation
- Antigravity CLI credentials (`oauth_credentials.json`) are stored within isolated per-profile sandboxes (`~/.gemini-profiles/{id}/.gemini/antigravity-cli/`).
- Sandboxing uses native operating system environment variables (`USERPROFILE`, `HOME`, `SSH_CONNECTION`).
- Tokens are encrypted using the user's local operating system capabilities and are never shared across profiles.

### 4.3. Chat History & Conversation Import Safety
- The conversation import feature (`Import Chat`) performs **purely local disk operations**:
  - It duplicates local JSONL prompt lines from `history.jsonl`, local transcript artifacts from `brain/{convId}`, and session databases from `conversations/`.
  - **No Google OAuth tokens, refresh tokens, or account credentials are copied or merged**.
  - No remote network requests or API modifications are made during conversation transfers.
  - This ensures users can preserve developer context across profiles without violating Google authentication mechanisms.

---

## 5. Google Terms of Service & Risk Considerations

When using Agy CLI Account Swarm in combination with Google Antigravity services, developers must adhere to Google's official Terms of Service and Generative AI Prohibited Use Policies.

### 5.1. Rate Limits & Multi-Account Usage
- **Fair Capacity Distribution**: Google implements both a Weekly Limit and a rolling 5-Hour Limit to fairly distribute global compute capacity across users.
- **Automated Rapid-Fire Scripting Risk**: Running dozens of accounts simultaneously in an aggressive parallel swarm may trigger rate-limiting, temporary IP throttling, or CAPTCHA challenges from Google's anti-abuse sentinels.
- **Account Policy Compliance**: Users are responsible for ensuring their usage of multiple Google accounts complies with Google's Terms of Service regarding account management and quota policies. Avoid using automated swarms for abusive generation, spam, or service disruption.

### 5.2. Workspace Security & File Access
- The Antigravity CLI executes agent tools locally on your workstation (reading files, executing commands, running build scripts).
- Running untrusted agent scripts or workspaces with elevated permissions poses local security risks. Always review agent actions and use workspace anchoring responsibly.

---

## 6. Developer Checklist for Safe Operation

To maximize safety and maintain policy compliance:

1. ✅ **Respect Rate Limits**: Observe the model quota gauges (Gemini and Claude/GPT 5-hour limits) displayed on your Account Cards.
2. ✅ **Maintain Account Separation**: Never manually swap OAuth credentials between sandboxes.
3. ✅ **Inspect Transcripts Locally**: Use the built-in Logs page (`/logs`) and local `brain/` transcripts to audit agent interactions.
4. ✅ **Keep agy Updated**: Regularly update the official Google Antigravity CLI via `agy --version` / installer to ensure schema compatibility.
