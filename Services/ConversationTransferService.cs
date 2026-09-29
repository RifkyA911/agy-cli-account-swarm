using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Services;

public class TransferableConversation
{
    public string ConversationId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string DisplayPrompt { get; set; } = string.Empty;
    public string Workspace { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}

public class ConversationTransferResult
{
    public bool Success { get; set; }
    public int TransferredCount { get; set; }
    public int SkippedCount { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; } = [];
}

public interface IConversationTransferService
{
    Task<List<TransferableConversation>> GetConversationsAsync(AccountProfile profile);
    Task<ConversationTransferResult> TransferConversationsAsync(
        AccountProfile sourceProfile,
        AccountProfile targetProfile,
        IEnumerable<string> conversationIds,
        bool overwrite = false);
}

public class ConversationTransferService : IConversationTransferService
{
    public async Task<List<TransferableConversation>> GetConversationsAsync(AccountProfile profile)
    {
        return await Task.Run(() =>
        {
            var conversations = new Dictionary<string, TransferableConversation>(StringComparer.OrdinalIgnoreCase);
            var profileDir = profile.GetEffectiveProfileDirectory();
            var geminiDir = Path.Combine(profileDir, ".gemini", "antigravity-cli");
            var historyFile = Path.Combine(geminiDir, "history.jsonl");

            if (!File.Exists(historyFile))
            {
                // Fallback for default profile if empty
                if (profile.CustomProfilePath == null)
                {
                    var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    var fallbackFile = Path.Combine(userHome, ".gemini", "antigravity-cli", "history.jsonl");
                    if (File.Exists(fallbackFile))
                    {
                        historyFile = fallbackFile;
                    }
                    else
                    {
                        return [];
                    }
                }
                else
                {
                    return [];
                }
            }

            try
            {
                using var stream = new FileStream(historyFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;
                        var convId = root.TryGetProperty("conversationId", out var cProp) ? cProp.GetString() ?? "" : "";
                        if (string.IsNullOrWhiteSpace(convId)) continue;

                        long epochMs = 0;
                        if (root.TryGetProperty("timestamp", out var tsProp) && tsProp.TryGetInt64(out var ts))
                        {
                            epochMs = ts;
                        }

                        var dt = epochMs > 0
                            ? DateTimeOffset.FromUnixTimeMilliseconds(epochMs).LocalDateTime
                            : File.GetLastWriteTime(historyFile);

                        var display = root.TryGetProperty("display", out var dispProp) ? dispProp.GetString() ?? "" : "";
                        var ws = root.TryGetProperty("workspace", out var wsProp) ? wsProp.GetString() ?? "" : "";

                        // If already recorded, update with latest timestamp or prompt
                        if (!conversations.TryGetValue(convId, out var existing) || dt > existing.Timestamp)
                        {
                            conversations[convId] = new TransferableConversation
                            {
                                ConversationId = convId,
                                Timestamp = dt,
                                DisplayPrompt = string.IsNullOrWhiteSpace(display) ? (existing?.DisplayPrompt ?? $"Session {convId[..Math.Min(8, convId.Length)]}") : display,
                                Workspace = string.IsNullOrWhiteSpace(ws) ? (existing?.Workspace ?? "") : ws,
                                IsSelected = false
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"[ConversationTransferService] Skipping malformed line in history: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[ConversationTransferService] Failed reading conversations for '{profile.Name}': {ex.Message}");
            }

            // Also check conversations folder for any sessions that might not be in history.jsonl
            var convDir = Path.Combine(geminiDir, "conversations");
            if (Directory.Exists(convDir))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(convDir))
                    {
                        var name = Path.GetFileNameWithoutExtension(file);
                        if (!conversations.ContainsKey(name))
                        {
                            conversations[name] = new TransferableConversation
                            {
                                ConversationId = name,
                                Timestamp = File.GetLastWriteTime(file),
                                DisplayPrompt = $"Conversation {name[..Math.Min(8, name.Length)]}",
                                Workspace = "",
                                IsSelected = false
                            };
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"[ConversationTransferService] Error scanning conversations folder: {ex.Message}");
                }
            }

            return conversations.Values.OrderByDescending(c => c.Timestamp).ToList();
        });
    }

    public async Task<ConversationTransferResult> TransferConversationsAsync(
        AccountProfile sourceProfile,
        AccountProfile targetProfile,
        IEnumerable<string> conversationIds,
        bool overwrite = false)
    {
        return await Task.Run(() =>
        {
            var result = new ConversationTransferResult();
            var targetList = conversationIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (targetList.Count == 0)
            {
                result.Success = true;
                result.Message = "No conversations selected to transfer.";
                return result;
            }

            var sourceProfileDir = sourceProfile.GetEffectiveProfileDirectory();
            var targetProfileDir = targetProfile.GetEffectiveProfileDirectory();

            var sourceCliDir = Path.Combine(sourceProfileDir, ".gemini", "antigravity-cli");
            var targetCliDir = Path.Combine(targetProfileDir, ".gemini", "antigravity-cli");

            if (!Directory.Exists(sourceCliDir))
            {
                // Fallback for default profile
                if (sourceProfile.CustomProfilePath == null)
                {
                    var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    sourceCliDir = Path.Combine(userHome, ".gemini", "antigravity-cli");
                }
            }

            if (!Directory.Exists(targetCliDir))
            {
                Directory.CreateDirectory(targetCliDir);
            }

            var targetConversationsDir = Path.Combine(targetCliDir, "conversations");
            if (!Directory.Exists(targetConversationsDir))
            {
                Directory.CreateDirectory(targetConversationsDir);
            }

            var targetBrainDir = Path.Combine(targetCliDir, "brain");
            if (!Directory.Exists(targetBrainDir))
            {
                Directory.CreateDirectory(targetBrainDir);
            }

            // 1. Collect target existing conversation IDs in history.jsonl
            var targetHistoryFile = Path.Combine(targetCliDir, "history.jsonl");
            var targetExistingConvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(targetHistoryFile))
            {
                try
                {
                    foreach (var line in File.ReadAllLines(targetHistoryFile))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        try
                        {
                            using var doc = JsonDocument.Parse(line);
                            if (doc.RootElement.TryGetProperty("conversationId", out var cProp) && cProp.GetString() is { Length: > 0 } id)
                            {
                                targetExistingConvIds.Add(id);
                            }
                        }
                        catch { /* ignore */ }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"[ConversationTransferService] Could not read target history.jsonl: {ex.Message}");
                }
            }

            // 2. Read matching history.jsonl lines from source
            var sourceHistoryFile = Path.Combine(sourceCliDir, "history.jsonl");
            var linesToAppend = new List<string>();
            var setOfTargetIds = new HashSet<string>(targetList, StringComparer.OrdinalIgnoreCase);

            if (File.Exists(sourceHistoryFile))
            {
                try
                {
                    foreach (var line in File.ReadAllLines(sourceHistoryFile))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        try
                        {
                            using var doc = JsonDocument.Parse(line);
                            if (doc.RootElement.TryGetProperty("conversationId", out var cProp) &&
                                cProp.GetString() is { Length: > 0 } id &&
                                setOfTargetIds.Contains(id))
                            {
                                if (!targetExistingConvIds.Contains(id) || overwrite)
                                {
                                    linesToAppend.Add(line);
                                    targetExistingConvIds.Add(id);
                                }
                            }
                        }
                        catch { /* ignore */ }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"[ConversationTransferService] Could not read source history.jsonl: {ex.Message}");
                }
            }

            // Append collected history lines
            if (linesToAppend.Count > 0)
            {
                try
                {
                    File.AppendAllLines(targetHistoryFile, linesToAppend);
                }
                catch (Exception ex)
                {
                    var msg = $"Failed appending history lines: {ex.Message}";
                    Logger.Error(msg, ex);
                    result.Errors.Add(msg);
                }
            }

            // 3. Copy conversation files and brain directory for each conversation ID
            var sourceConversationsDir = Path.Combine(sourceCliDir, "conversations");
            var sourceBrainDir = Path.Combine(sourceCliDir, "brain");

            foreach (var convId in targetList)
            {
                bool transferredSomething = false;

                // Copy conversations files: e.g. {convId} or {convId}.*
                if (Directory.Exists(sourceConversationsDir))
                {
                    try
                    {
                        var matchingFiles = Directory.GetFiles(sourceConversationsDir, $"{convId}*");
                        foreach (var srcFile in matchingFiles)
                        {
                            var destFile = Path.Combine(targetConversationsDir, Path.GetFileName(srcFile));
                            if (!File.Exists(destFile) || overwrite)
                            {
                                File.Copy(srcFile, destFile, true);
                                transferredSomething = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Could not copy conversation state for {convId}: {ex.Message}");
                    }
                }

                // Copy brain folder: e.g. brain/{convId}/
                if (Directory.Exists(sourceBrainDir))
                {
                    var srcBrainFolder = Path.Combine(sourceBrainDir, convId);
                    if (Directory.Exists(srcBrainFolder))
                    {
                        var destBrainFolder = Path.Combine(targetBrainDir, convId);
                        try
                        {
                            CopyDirectoryRecursively(srcBrainFolder, destBrainFolder, overwrite);
                            transferredSomething = true;
                        }
                        catch (Exception ex)
                        {
                            result.Errors.Add($"Could not copy brain folder for {convId}: {ex.Message}");
                        }
                    }
                }

                if (transferredSomething || linesToAppend.Any(l => l.Contains(convId)))
                {
                    result.TransferredCount++;
                }
                else
                {
                    result.SkippedCount++;
                }
            }

            result.Success = result.TransferredCount > 0 || result.SkippedCount > 0;
            result.Message = $"Transferred {result.TransferredCount} conversation(s) to '{targetProfile.Name}' (Skipped {result.SkippedCount}).";
            Logger.Info($"[ConversationTransferService] {result.Message}");

            return result;
        });
    }

    private static void CopyDirectoryRecursively(string sourceDir, string destDir, bool overwrite)
    {
        if (!Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            if (!File.Exists(destFile) || overwrite)
            {
                File.Copy(file, destFile, true);
            }
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
            CopyDirectoryRecursively(subDir, destSubDir, overwrite);
        }
    }
}
