---
name: arag-cli
description: High-performance Agent RAG search tool for keyword search, semantic search, chunk reading, and more. Use when: searching game guides, querying character info, looking up item data, retrieving world-building lore, etc. Triggers: keyword search, semantic search, RAG search, arag, search guide, character lookup, item lookup
allowed-tools: Bash(arag-cli *)
---

# Arag CLI

High-performance Agent RAG search tool providing keyword search, semantic search, chunk reading, and multi-KB management.

## Agent Usage Guide

**Tools**: keyword_search (exact keywords) | semantic_search (semantic) | search (hybrid) | read_chunk (read content)

**Strategy**: Iterative search → read → evaluate. Break multi-hop questions into sub-questions and solve step by step.

**When to switch**: If keyword_search returns no results twice or returns the same results three times, switch to semantic_search or search (hybrid, with personalized ranking — preferred).

**Answer guidelines**: Base answers on documents, cite sources, avoid speculation.

## Installation

```bash
# Linux/macOS
curl -sSL https://github.com/KuaishouGameMind/arag-cli/releases/latest/download/install.sh | bash

# Windows
# Download and run install.bat
```

## Quick Examples

```bash
# Keyword search
arag-cli keyword-search "warrior" "skill" -k 5

# Semantic search
arag-cli semantic-search "What are the sequences of the Warrior pathway" -k 5

# Hybrid search (recommended)
arag-cli search "What are the sequences of the Warrior pathway" -k 10

# Read chunk
arag-cli read-chunk 1850
arag-cli read-chunk 154 --expand-neighbors 2

# Search a specific KB
arag-cli --kb <name> keyword-search "boss skill"

# Knowledge base health check
arag-cli lint

# Chunk annotations
arag-cli note add 1850 --text "Important reference"
arag-cli note show 1850
arag-cli note list

# Pin high-quality answers
arag-cli pin --query "Warrior pathway full sequences" --answer-md answer.md
arag-cli pin-candidates --top 5

# Multi-KB management
arag-cli kb list
arag-cli kb use <name>

```

## Tool Schemas

### keyword_search

```json
{
  "name": "keyword_search",
  "description": "BM25 keyword search, exact keyword matching, returns relevant evidence snippets",
  "parameters": {
    "type": "object",
    "properties": {
      "keywords": {
        "type": "array",
        "items": {"type": "string"},
        "description": "List of search keywords"
      },
      "top_k": {
        "type": "integer",
        "description": "Number of results to return",
        "default": 5
      }
    },
    "required": ["keywords"]
  }
}
```

### semantic_search

```json
{
  "name": "semantic_search",
  "description": "Semantic vector search, finds related content via embedding similarity",
  "parameters": {
    "type": "object",
    "properties": {
      "query": {
        "type": "string",
        "description": "Search query string"
      },
      "top_k": {
        "type": "integer",
        "description": "Number of results to return",
        "default": 5
      }
    },
    "required": ["query"]
  }
}
```

### read_chunk

```json
{
  "name": "read_chunk",
  "description": "Read the full content of specified chunks",
  "parameters": {
    "type": "object",
    "properties": {
      "chunk_ids": {
        "type": "array",
        "items": {"type": "string"},
        "description": "List of chunk IDs"
      },
      "mode": {
        "type": "string",
        "enum": ["excerpt", "full"],
        "description": "Read mode: excerpt (summary) or full (complete)",
        "default": "excerpt"
      }
    },
    "required": ["chunk_ids"]
  }
}
```

### search

```json
{
  "name": "search",
  "description": "Hybrid search: BM25 + semantic vector RRF fusion + personalized ranking",
  "parameters": {
    "type": "object",
    "properties": {
      "query": {
        "type": "string",
        "description": "Natural language query (used for both keyword and semantic search)"
      },
      "top_k": {
        "type": "integer",
        "description": "Number of results to return (default 10, max 20)",
        "default": 10
      },
      "no_personalize": {
        "type": "boolean",
        "description": "Disable personalized ranking, fall back to pure RRF scores",
        "default": false
      }
    },
    "required": ["query"]
  }
}
```

### note

```json
{
  "name": "note",
  "description": "Chunk annotation management: add/show/list/remove/export",
  "parameters": {
    "type": "object",
    "properties": {
      "action": {
        "type": "string",
        "enum": ["add", "show", "list", "remove", "export"],
        "description": "Action type"
      },
      "chunk_id": {
        "type": "string",
        "description": "Chunk ID (required for add/show/remove)"
      },
      "text": {
        "type": "string",
        "description": "Annotation text content (for add action)"
      },
      "file": {
        "type": "string",
        "description": "Markdown file path (for add action)"
      }
    },
    "required": ["action"]
  }
}
```

### pin

```json
{
  "name": "pin",
  "description": "Pin high-quality answers into the knowledge base",
  "parameters": {
    "type": "object",
    "properties": {
      "query": {
        "type": "string",
        "description": "User query that triggered the pin"
      },
      "answer_md": {
        "type": "string",
        "description": "Path to the Markdown answer file"
      }
    },
    "required": ["query", "answer_md"]
  }
}
```

### lint

```json
{
  "name": "lint",
  "description": "Knowledge base health check (7 diagnostics: orphan chunks / low adoption / duplicates / stale / drift / oversized / blind spots)",
  "parameters": {
    "type": "object",
    "properties": {
      "chunk_size": {
        "type": "integer",
        "description": "Baseline chunk token count (default 500)",
        "default": 500
      },
      "chunk_size_factor": {
        "type": "number",
        "description": "Oversized multiplier threshold (default 1.5)",
        "default": 1.5
      },
      "stale_months": {
        "type": "integer",
        "description": "Stale file threshold in months (default 6)",
        "default": 6
      },
      "duplicate_threshold": {
        "type": "number",
        "description": "Duplicate detection Jaccard threshold (default 0.95)",
        "default": 0.95
      },
      "issues_only": {
        "type": "boolean",
        "description": "Only output checks with issues",
        "default": false
      }
    }
  }
}
```

## Reference Docs

- [Full Command Reference](./references/commands.md) - All command tables and detailed examples
- [Complete Tool Schemas](./references/schemas.md) - Full JSON Schema for 6 tools
