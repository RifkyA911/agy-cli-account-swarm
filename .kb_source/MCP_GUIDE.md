# Model Context Protocol (MCP) Guide for Antigravity AI

## 1. What is MCP?

The **Model Context Protocol (MCP)** is an open standard that enables AI models to discover and invoke tools, search documentation, query databases, and read file repositories safely over standard IO (`stdio`).

Google Antigravity CLI natively supports MCP servers configured under:
- `~/.gemini/antigravity-cli/mcp/<server-name>/`
- `~/.gemini/config/mcp_config.json`

---

## 2. Supported Server Types

### Eagerly Loaded Servers
Registered as native tools and available immediately in every session turn.

### Lazy-Loaded Servers
Schemas and tools are resolved on-demand when the agent determines they are relevant (e.g. `context7` for documentation queries).

---

## 3. Discovered MCP Servers in Swarm

The **MCP Tools** view (`/mcp`) in `Agy Account Swarm` automatically scans your profile directories and displays:
- Discovered tool names (e.g. `resolve-library-id`, `query-docs`)
- Invocation command and arguments
- Operational status (Active, Lazy, or Configured)

To add community MCP tools, you can configure `mcp_config.json`:
```json
{
  "mcpServers": {
    "filesystem": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem", "D:\\Works"]
    }
  }
}
```
