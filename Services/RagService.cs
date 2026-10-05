using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Services;

public interface IRagService
{
    bool IsAragAvailable { get; }
    string AragCliPath { get; }
    Task<List<KnowledgeBaseInfo>> GetKnowledgeBasesAsync(CancellationToken cancellationToken = default);
    Task<RagSearchResult> SearchContextAsync(string query, string kbName = "agy-swarm", string searchMode = "Hybrid", int topK = 3, CancellationToken cancellationToken = default);
    string BuildAugmentedPrompt(string originalPrompt, List<RagChunkItem> retrievedChunks, string? liveTaskContext = null);
}

public class RagService : IRagService
{
    private string? _resolvedAragCliPath;
    private readonly string _workspaceRoot;

    public RagService(string? workspaceRoot = null)
    {
        _workspaceRoot = workspaceRoot ?? Directory.GetCurrentDirectory();
        ResolveAragCli();
    }

    public bool IsAragAvailable => !string.IsNullOrEmpty(_resolvedAragCliPath) && File.Exists(_resolvedAragCliPath);
    public string AragCliPath => _resolvedAragCliPath ?? string.Empty;

    private void ResolveAragCli()
    {
        // 1. Check user .cargo/bin directory (Standard for Rust CLI tools)
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var cargoBin = Path.Combine(userProfile, ".cargo", "bin", OperatingSystem.IsWindows() ? "arag-cli.exe" : "arag-cli");
        if (File.Exists(cargoBin))
        {
            _resolvedAragCliPath = cargoBin;
            return;
        }

        // 2. Check system PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var exeName = OperatingSystem.IsWindows() ? "arag-cli.exe" : "arag-cli";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), exeName);
            if (File.Exists(candidate))
            {
                _resolvedAragCliPath = candidate;
                return;
            }
        }
    }

    public async Task<List<KnowledgeBaseInfo>> GetKnowledgeBasesAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<KnowledgeBaseInfo>();

        if (IsAragAvailable)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _resolvedAragCliPath!,
                    Arguments = "kb list --format json",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
                    await proc.WaitForExitAsync(cancellationToken);

                    if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout))
                    {
                        var jsonNode = JsonNode.Parse(stdout);
                        if (jsonNode is JsonArray arr)
                        {
                            foreach (var item in arr)
                            {
                                if (item is JsonObject obj)
                                {
                                    list.Add(new KnowledgeBaseInfo
                                    {
                                        Name = obj["name"]?.GetValue<string>() ?? "unknown",
                                        Path = obj["path"]?.GetValue<string>() ?? "",
                                        Active = obj["active"]?.GetValue<bool>() ?? false,
                                        TotalChunks = obj["total_chunks"]?.GetValue<int>() ?? 0,
                                        TotalSentences = obj["total_sentences"]?.GetValue<int>() ?? 0,
                                        SizeMb = obj["size_mb"]?.GetValue<double>() ?? 0
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to retrieve arag-cli knowledge bases: {ex.Message}");
            }
        }

        // Fallback: If no KBs parsed or arag-cli not found, check ~/.arag/kb folder or provide workspace KB
        if (list.Count == 0)
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var aragKbDir = Path.Combine(userProfile, ".arag", "kb");
            if (Directory.Exists(aragKbDir))
            {
                foreach (var dir in Directory.GetDirectories(aragKbDir))
                {
                    var name = Path.GetFileName(dir);
                    list.Add(new KnowledgeBaseInfo
                    {
                        Name = name,
                        Path = dir,
                        Active = name.Equals("agy-swarm", StringComparison.OrdinalIgnoreCase),
                        TotalChunks = 122
                    });
                }
            }
        }

        if (list.Count == 0)
        {
            list.Add(new KnowledgeBaseInfo
            {
                Name = "agy-swarm",
                Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".arag", "kb", "agy-swarm"),
                Active = true,
                TotalChunks = 122
            });
        }

        return list;
    }

    public async Task<RagSearchResult> SearchContextAsync(
        string query,
        string kbName = "agy-swarm",
        string searchMode = "Hybrid",
        int topK = 3,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new RagSearchResult { Query = query, TotalResults = 0 };
        }

        if (IsAragAvailable)
        {
            // 1. If mode is Hybrid or Semantic, try hybrid search
            if (searchMode.Contains("Hybrid", StringComparison.OrdinalIgnoreCase) ||
                searchMode.Contains("Semantic", StringComparison.OrdinalIgnoreCase))
            {
                var hybridRes = await ExecuteAragSearchAsync("search", query, kbName, topK, cancellationToken);
                if (hybridRes.IsSuccess && hybridRes.Chunks.Count > 0)
                {
                    return hybridRes;
                }
                // Fallback to keyword-search if hybrid fails (e.g. embedding mismatch)
            }

            // 2. Keyword BM25 search
            var keywordRes = await ExecuteAragKeywordSearchAsync(query, kbName, topK, cancellationToken);
            if (keywordRes.IsSuccess && keywordRes.Chunks.Count > 0)
            {
                return keywordRes;
            }
        }

        // 3. Built-in Local Markdown Fallback Search if arag-cli yields 0 results or is unavailable
        return await ExecuteLocalDocFallbackSearchAsync(query, topK, cancellationToken);
    }

    private async Task<RagSearchResult> ExecuteAragSearchAsync(
        string subcommand,
        string query,
        string kbName,
        int topK,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var sanitizedQuery = query.Replace("\"", "'").Trim();
            var psi = new ProcessStartInfo
            {
                FileName = _resolvedAragCliPath!,
                Arguments = $"{subcommand} \"{sanitizedQuery}\" -k {topK} --kb {kbName} --format json",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                return new RagSearchResult { IsSuccess = false, ErrorMessage = "Failed to launch arag-cli" };
            }

            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);
            sw.Stop();

            if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout))
            {
                return ParseAragJsonOutput(stdout, query, kbName, sw.ElapsedMilliseconds);
            }

            return new RagSearchResult { IsSuccess = false, ErrorMessage = stdout };
        }
        catch (Exception ex)
        {
            return new RagSearchResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<RagSearchResult> ExecuteAragKeywordSearchAsync(
        string query,
        string kbName,
        int topK,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // Split into individual keyword tokens (removing short punctuation words)
            var tokens = query.Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ';', '?', '!', '"', '\'' },
                                    StringSplitOptions.RemoveEmptyEntries)
                              .Where(t => t.Length >= 2)
                              .Distinct(StringComparer.OrdinalIgnoreCase)
                              .Take(5)
                              .ToList();

            if (tokens.Count == 0) tokens.Add(query.Trim());

            var argsBuilder = new StringBuilder();
            argsBuilder.Append("keyword-search ");
            foreach (var tok in tokens)
            {
                argsBuilder.Append($"\"{tok}\" ");
            }
            argsBuilder.Append($"-k {topK} --kb {kbName} --format json");

            var psi = new ProcessStartInfo
            {
                FileName = _resolvedAragCliPath!,
                Arguments = argsBuilder.ToString(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                return new RagSearchResult { IsSuccess = false, ErrorMessage = "Failed to launch arag-cli" };
            }

            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);
            sw.Stop();

            if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout))
            {
                return ParseAragJsonOutput(stdout, query, kbName, sw.ElapsedMilliseconds);
            }

            return new RagSearchResult { IsSuccess = false, ErrorMessage = stdout };
        }
        catch (Exception ex)
        {
            return new RagSearchResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    private RagSearchResult ParseAragJsonOutput(string json, string query, string kbName, long fallbackMs)
    {
        try
        {
            var node = JsonNode.Parse(json);
            if (node == null) return new RagSearchResult { Query = query };

            var result = new RagSearchResult
            {
                Tool = node["tool"]?.GetValue<string>() ?? "keyword_search",
                Query = query,
                IsSuccess = true
            };

            var meta = node["meta"];
            result.SearchTimeMs = meta?["search_time_ms"]?.GetValue<long>() ?? fallbackMs;
            result.TotalResults = meta?["total_results"]?.GetValue<int>() ?? 0;

            var resultsArr = node["results"] as JsonArray;
            if (resultsArr != null)
            {
                foreach (var item in resultsArr)
                {
                    if (item is JsonObject obj)
                    {
                        var chunk = new RagChunkItem
                        {
                            ChunkId = obj["chunk_id"]?.ToString() ?? "",
                            Snippet = obj["snippet"]?.GetValue<string>() ?? "",
                            Score = obj["score"]?.GetValue<double>() ?? 0.0,
                            KbName = kbName
                        };

                        if (obj["matched_keywords"] is JsonArray kwArr)
                        {
                            foreach (var kw in kwArr)
                            {
                                if (kw != null) chunk.MatchedKeywords.Add(kw.ToString());
                            }
                        }

                        result.Chunks.Add(chunk);
                    }
                }
            }

            if (result.TotalResults == 0) result.TotalResults = result.Chunks.Count;
            return result;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to parse arag-cli output JSON: {ex.Message}");
            return new RagSearchResult { Query = query, IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<RagSearchResult> ExecuteLocalDocFallbackSearchAsync(string query, int topK, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var result = new RagSearchResult
        {
            Tool = "local_docs_scan",
            Query = query,
            IsSuccess = true
        };

        try
        {
            var docsDir = Path.Combine(_workspaceRoot, "docs");
            var candidateFiles = new List<string>();

            if (Directory.Exists(docsDir))
            {
                candidateFiles.AddRange(Directory.GetFiles(docsDir, "*.md", SearchOption.AllDirectories));
            }

            var rootMds = Directory.GetFiles(_workspaceRoot, "*.md", SearchOption.TopDirectoryOnly);
            candidateFiles.AddRange(rootMds);

            var queryTokens = query.Split(new[] { ' ', '\t', ',', '.', '?' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Where(t => t.Length >= 3)
                                   .Select(t => t.ToLowerInvariant())
                                   .ToList();

            var scoredChunks = new List<RagChunkItem>();

            foreach (var file in candidateFiles.Distinct())
            {
                if (cancellationToken.IsCancellationRequested) break;
                var fileName = Path.GetFileName(file);
                var content = await File.ReadAllTextAsync(file, cancellationToken);
                var paragraphs = content.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < paragraphs.Length; i++)
                {
                    var p = paragraphs[i].Trim();
                    if (p.Length < 30) continue;

                    int score = 0;
                    var matched = new List<string>();
                    var pLower = p.ToLowerInvariant();

                    foreach (var tok in queryTokens)
                    {
                        if (pLower.Contains(tok))
                        {
                            score += 2;
                            matched.Add(tok);
                        }
                    }

                    if (score > 0)
                    {
                        scoredChunks.Add(new RagChunkItem
                        {
                            ChunkId = $"{fileName}#p{i + 1}",
                            Snippet = p.Length > 350 ? p.Substring(0, 350) + "..." : p,
                            Score = score,
                            KbName = "local-docs",
                            MatchedKeywords = matched
                        });
                    }
                }
            }

            result.Chunks = scoredChunks.OrderByDescending(c => c.Score).Take(topK).ToList();
            result.TotalResults = result.Chunks.Count;
            sw.Stop();
            result.SearchTimeMs = sw.ElapsedMilliseconds;
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    public string BuildAugmentedPrompt(string originalPrompt, List<RagChunkItem> retrievedChunks, string? liveTaskContext = null)
    {
        if ((retrievedChunks == null || retrievedChunks.Count == 0) && string.IsNullOrWhiteSpace(liveTaskContext))
        {
            return originalPrompt;
        }

        var sb = new StringBuilder();

        // 1. Inject Knowledge Base Chunks if available
        if (retrievedChunks != null && retrievedChunks.Count > 0)
        {
            sb.AppendLine("=== KNOWLEDGE BASE RELEVANT CONTEXT (RAG) ===");
            sb.AppendLine("The following factual documentation snippets were retrieved from verified local project knowledge bases:");
            sb.AppendLine();

            foreach (var chunk in retrievedChunks)
            {
                sb.AppendLine($"[Source: {chunk.KbName} | Chunk #{chunk.ChunkId} | Relevance: {chunk.ScoreDisplay}]");
                sb.AppendLine(chunk.Snippet.Trim());
                sb.AppendLine("---");
            }
            sb.AppendLine();
        }

        // 2. Inject Live Swarm Tasks if available
        if (!string.IsNullOrWhiteSpace(liveTaskContext))
        {
            sb.AppendLine("=== REAL-TIME SWARM STATUS & WORKER TELEMETRY ===");
            sb.AppendLine(liveTaskContext.Trim());
            sb.AppendLine();
        }

        // 3. Append User Query
        sb.AppendLine("=== USER QUERY ===");
        sb.AppendLine(originalPrompt.Trim());
        sb.AppendLine();
        sb.AppendLine("Instructions: Use the factual knowledge base context and real-time swarm status above to provide a precise, accurate, and comprehensive response.");

        return sb.ToString();
    }
}
