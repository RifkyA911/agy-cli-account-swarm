# Database & Configuration Architecture 🗄️

This document details the underlying persistence layer, SQLite database structure (`conversation_summaries.db`), JSON configurations, and data flow in **Agy Account Swarm**.

---

## 1. SQLite Database Architecture

Antigravity CLI persists conversation summaries, execution metadata, and telemetry inside SQLite:
- **Default Database Path**: `~/.gemini/antigravity-cli/conversation_summaries.db`
- **Sandboxed Profiles Path**: `~/.gemini-profiles/{id}/.gemini/antigravity-cli/conversation_summaries.db`

### Data Definition Language (DDL)

```sql
CREATE TABLE IF NOT EXISTS summaries (
    conversation_id TEXT PRIMARY KEY,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    title TEXT,
    summary TEXT,
    total_tokens INTEGER DEFAULT 0,
    model TEXT
);

CREATE TABLE IF NOT EXISTS conversation_turns (
    turn_id TEXT PRIMARY KEY,
    conversation_id TEXT NOT NULL,
    turn_number INTEGER NOT NULL,
    role TEXT NOT NULL, -- 'user', 'assistant', 'system'
    content TEXT,
    tokens INTEGER DEFAULT 0,
    timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY(conversation_id) REFERENCES summaries(conversation_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS annotations (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    conversation_id TEXT NOT NULL,
    label TEXT,
    notes TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY(conversation_id) REFERENCES summaries(conversation_id)
);

CREATE INDEX IF NOT EXISTS idx_summaries_updated ON summaries(updated_at DESC);
CREATE INDEX IF NOT EXISTS idx_turns_conv ON conversation_turns(conversation_id, turn_number ASC);
```

### Data Manipulation Language (DML) Queries

#### 1. Retrieve Recent Conversations
```sql
SELECT conversation_id, title, model, total_tokens, updated_at
FROM summaries
ORDER BY updated_at DESC
LIMIT 50;
```

#### 2. Model Usage Breakdown
```sql
SELECT model, COUNT(*) AS session_count, SUM(total_tokens) AS total_tokens_used
FROM summaries
WHERE model IS NOT NULL
GROUP BY model
ORDER BY total_tokens_used DESC;
```

#### 3. Fetch Full Dialogue for Conversation
```sql
SELECT turn_number, role, content, tokens, timestamp
FROM conversation_turns
WHERE conversation_id = :conv_id
ORDER BY turn_number ASC;
```

---

## 2. Configuration Schemas

### Application Profiles (`%APPDATA%/AgyAccountSwarm/profiles.json`)
```json
[
  {
    "Id": "main",
    "Name": "Primary (Default)",
    "SandboxPath": "C:\\Users\\rifky",
    "PreferredModel": "gemini-3.8-flash",
    "Tier": "Pro",
    "QuotaLimit": 1000,
    "CreatedAt": "2026-09-26T18:00:00Z"
  },
  {
    "Id": "worker-alpha",
    "Name": "Worker Alpha (Account 2)",
    "SandboxPath": "C:\\Users\\rifky\\.gemini-profiles\\worker-alpha",
    "PreferredModel": "gemini-2.5-pro",
    "Tier": "Basic",
    "QuotaLimit": 500,
    "CreatedAt": "2026-09-26T18:30:00Z"
  }
]
```

### Application Global Settings (`%APPDATA%/AgyAccountSwarm/settings.json`)
```json
{
  "Theme": "Dark",
  "LaunchMode": "SplitPanes",
  "CloseToTray": false,
  "MinimizeToTray": true,
  "DefaultModel": "gemini-3.8-flash",
  "Language": "en",
  "AutoSyncInterval": "5 Minutes"
}
```

---

## 3. Data Integrity & Syncing

- **Real-time File Observers**: Monitors `~/.gemini/antigravity-cli/history.jsonl` and profile sandboxes using `FileSystemWatcher`.
- **Periodic Dispatcher Polling**: Configurable automatic refresh interval (1m, 5m, 15m, 30m) ensuring metrics match live CLI execution without UI locks.
- **Zero-Synthetic Baseline**: Unused models display clean zero counts without synthetic interpolation.
