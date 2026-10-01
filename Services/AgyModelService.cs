using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AgyAccountSwarm.Services;

public record AgyModelInfo(string Id, string DisplayName);

/// <summary>
/// Service responsible for discovering supported LLM models from the Antigravity CLI
/// and matching/normalizing model identifiers for analytics and filtering.
/// </summary>
public interface IAgyModelService
{
    /// <summary>
    /// Discovers supported LLMs by querying 'agy models' and inspecting profile configurations,
    /// falling back to standard Gemini and third-party models if the CLI is offline.
    /// </summary>
    /// <param name="additionalProfilePaths">Optional profile sandbox paths to inspect for custom model definitions.</param>
    /// <returns>A list of discovered <see cref="AgyModelInfo"/> records.</returns>
    Task<List<AgyModelInfo>> DiscoverModelsAsync(IEnumerable<string>? additionalProfilePaths = null);

    /// <summary>
    /// Evaluates whether a session model matches a filter target (handling prefixes and aliases).
    /// </summary>
    /// <param name="entryModel">The model recorded in session history.</param>
    /// <param name="filterModel">The filter model identifier.</param>
    /// <returns><c>true</c> if matched; otherwise, <c>false</c>.</returns>
    bool IsModelMatch(string? entryModel, string? filterModel);

    /// <summary>
    /// Normalizes model identifiers into standardized display names or canonical slugs.
    /// </summary>
    /// <param name="model">The raw model string.</param>
    /// <returns>Normalized model string.</returns>
    string Normalize(string? model);
}

public class AgyModelService : IAgyModelService
{
    private static readonly List<AgyModelInfo> DefaultModels =
    [
        new("gemini-3.8-flash-medium", "Gemini 3.8 Flash (Medium)"),
        new("gemini-3.8-flash-high", "Gemini 3.8 Flash (High)"),
        new("gemini-3.8-flash-low", "Gemini 3.8 Flash (Low)"),
        new("gemini-3.7-flash-medium", "Gemini 3.7 Flash (Medium)"),
        new("gemini-3.7-flash-high", "Gemini 3.7 Flash (High)"),
        new("gemini-3.6-flash-medium", "Gemini 3.6 Flash (Medium)"),
        new("gemini-3.1-pro-high", "Gemini 3.1 Pro (High)"),
        new("claude-sonnet-4-6", "Claude Sonnet 4.6 (Thinking)"),
        new("claude-opus-4-6-thinking", "Claude Opus 4.6 (Thinking)"),
        new("gpt-oss-120b-medium", "GPT-OSS 120B (Medium)"),
        new("gemini-2.5-pro", "Gemini 2.5 Pro"),
        new("gemini-2.5-flash", "Gemini 2.5 Flash"),
        new("gemini-1.5-pro", "Gemini 1.5 Pro")
    ];

    private List<AgyModelInfo>? _cachedModels;

    public async Task<List<AgyModelInfo>> DiscoverModelsAsync(IEnumerable<string>? additionalProfilePaths = null)
    {
        if (_cachedModels != null && _cachedModels.Count > 0)
        {
            return _cachedModels;
        }

        var modelsDict = new Dictionary<string, AgyModelInfo>(StringComparer.OrdinalIgnoreCase);

        // 1. Populate default fallback models
        foreach (var m in DefaultModels)
        {
            modelsDict[m.Id] = m;
        }

        // 2. Discover live models from 'agy models' CLI
        try
        {
            var agyPath = TerminalLauncherService.ResolveAgyExecutablePath() ?? FindAgyBinary();
            if (!string.IsNullOrEmpty(agyPath) && File.Exists(agyPath))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = agyPath,
                    Arguments = "models",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                psi.Environment["TERM"] = "dumb";
                psi.Environment["CI"] = "1";
                psi.Environment["WT_SESSION"] = "";

                using var process = new Process { StartInfo = psi };
                process.Start();
                process.StandardInput.Close();


                var readTask = process.StandardOutput.ReadToEndAsync();
                var completed = await Task.WhenAny(readTask, Task.Delay(3000));

                if (completed == readTask)
                {
                    var output = await readTask;
                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("Fetching", StringComparison.OrdinalIgnoreCase)) continue;

                        var parts = trimmed.Split(['\t'], 2, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 2)
                        {
                            var id = parts[0].Trim();
                            var name = parts[1].Trim();
                            modelsDict[id] = new AgyModelInfo(id, name);
                        }
                        else if (parts.Length == 1 && !string.IsNullOrWhiteSpace(parts[0]))
                        {
                            var id = parts[0].Trim();
                            if (!modelsDict.ContainsKey(id))
                            {
                                modelsDict[id] = new AgyModelInfo(id, id);
                            }
                        }
                    }
                }
                else
                {
                    try { process.Kill(); } catch { }
                }
            }
        }
        catch
        {
            // Silently fall back to cached/default models
        }

        // 3. Scan settings.json across user profile and custom sandboxes
        var pathsToScan = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini", "antigravity-cli", "settings.json")
        };

        if (additionalProfilePaths != null)
        {
            foreach (var p in additionalProfilePaths)
            {
                pathsToScan.Add(Path.Combine(p, ".gemini", "antigravity-cli", "settings.json"));
            }
        }

        foreach (var sPath in pathsToScan)
        {
            if (File.Exists(sPath))
            {
                try
                {
                    var text = await File.ReadAllTextAsync(sPath);
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.TryGetProperty("model", out var mProp) && mProp.GetString() is { Length: > 0 } modelStr)
                    {
                        // Check if model exists
                        var existing = modelsDict.Values.FirstOrDefault(m => 
                            m.DisplayName.Equals(modelStr, StringComparison.OrdinalIgnoreCase) || 
                            m.Id.Equals(modelStr, StringComparison.OrdinalIgnoreCase));

                        if (existing == null)
                        {
                            modelsDict[modelStr] = new AgyModelInfo(modelStr, modelStr);
                        }
                    }
                }
                catch
                {
                    // Ignore JSON read error
                }
            }
        }

        _cachedModels = modelsDict.Values.OrderBy(m => m.DisplayName).ToList();
        Logger.Info($"[AgyModelService] Discovered {_cachedModels.Count} available models ({modelsDict.Count(m => !DefaultModels.Any(d => d.Id == m.Key))} dynamic)");
        return _cachedModels;
    }

    public bool IsModelMatch(string? entryModel, string? filterModel)
    {
        if (string.IsNullOrWhiteSpace(filterModel) || filterModel.Equals("All Models", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.IsNullOrWhiteSpace(entryModel))
            return false;

        string normEntry = Normalize(entryModel);
        string normFilter = Normalize(filterModel);

        if (string.IsNullOrEmpty(normFilter)) return true;
        if (string.IsNullOrEmpty(normEntry)) return false;

        return normEntry.Equals(normFilter, StringComparison.OrdinalIgnoreCase)
            || normEntry.Contains(normFilter, StringComparison.OrdinalIgnoreCase)
            || normFilter.Contains(normEntry, StringComparison.OrdinalIgnoreCase);
    }

    public string Normalize(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return string.Empty;
        return new string(model.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static string? FindAgyBinary()
    {
        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var direct = Path.Combine(localApp, "agy", "bin", "agy.exe");
        if (File.Exists(direct)) return direct;

        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        foreach (var p in paths)
        {
            var candidate = Path.Combine(p, "agy.exe");
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }
}
