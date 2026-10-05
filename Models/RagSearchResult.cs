using System;
using System.Collections.Generic;

namespace AgyAccountSwarm.Models;

/// <summary>
/// Individual retrieved text chunk from a knowledge base via arag-cli or local search.
/// </summary>
public class RagChunkItem
{
    public string ChunkId { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public double Score { get; set; }
    public string KbName { get; set; } = string.Empty;
    public List<string> MatchedKeywords { get; set; } = new();

    public string ScoreDisplay => $"{Score:F2}";
}

/// <summary>
/// Result container returned by RAG search execution.
/// </summary>
public class RagSearchResult
{
    public string Tool { get; set; } = "keyword_search";
    public string Query { get; set; } = string.Empty;
    public int TotalResults { get; set; }
    public long SearchTimeMs { get; set; }
    public List<RagChunkItem> Chunks { get; set; } = new();
    public bool IsSuccess { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Knowledge Base metadata discovered from arag-cli kb list.
/// </summary>
public class KnowledgeBaseInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool Active { get; set; }
    public int TotalChunks { get; set; }
    public int TotalSentences { get; set; }
    public double SizeMb { get; set; }

    public string DisplayTitle => $"{Name} ({TotalChunks} chunks)";
}
