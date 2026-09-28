using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Services;

public class RealHistoryEntry
{
    public DateTime Timestamp { get; set; }
    public string Display { get; set; } = string.Empty;
    public string Workspace { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;
    public string ProfileName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
}

public interface ITelemetryService
{
    Task<List<RealHistoryEntry>> LoadAllProfileHistoryAsync(IEnumerable<AccountProfile> profiles);
}

public class TelemetryService : ITelemetryService
{
    public Task<List<RealHistoryEntry>> LoadAllProfileHistoryAsync(IEnumerable<AccountProfile> profiles)
    {
        return Task.Run(() =>
        {
            var results = new List<RealHistoryEntry>();

            foreach (var profile in profiles)
            {
                var profileDir = profile.GetEffectiveProfileDirectory();
                var geminiDir = Path.Combine(profileDir, ".gemini", "antigravity-cli");
                var historyFile = Path.Combine(geminiDir, "history.jsonl");

                // Determine active model for this profile
                string detectedModel = !string.IsNullOrWhiteSpace(profile.PreferredModel) ? profile.PreferredModel : "gemini-3.8-flash";
                var settingsFile = Path.Combine(geminiDir, "settings.json");
                if (File.Exists(settingsFile))
                {
                    try
                    {
                        var sJson = File.ReadAllText(settingsFile);
                        using var sDoc = JsonDocument.Parse(sJson);
                        if (sDoc.RootElement.TryGetProperty("model", out var mProp) && mProp.GetString() is { Length: > 0 } mStr)
                        {
                            detectedModel = mStr;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"[TelemetryService] Could not parse settings.json for '{profile.Name}': {ex.Message}");
                    }
                }

                if (!File.Exists(historyFile))
                {
                    // Fallback to default user dir if profile dir hasn't created it yet
                    var fallbackUser = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    var fallbackFile = Path.Combine(fallbackUser, ".gemini", "antigravity-cli", "history.jsonl");
                    if (profile.CustomProfilePath == null && File.Exists(fallbackFile))
                    {
                        historyFile = fallbackFile;
                    }
                    else
                    {
                        continue;
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
                            var conv = root.TryGetProperty("conversationId", out var cProp) ? cProp.GetString() ?? "" : "";

                            results.Add(new RealHistoryEntry
                            {
                                Timestamp = dt,
                                Display = display,
                                Workspace = ws,
                                ConversationId = conv,
                                ProfileId = profile.Id,
                                ProfileName = profile.Name,
                                ModelName = detectedModel
                            });
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug($"[TelemetryService] Skipped malformed JSON line in history.jsonl: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"[TelemetryService] Could not read history.jsonl for profile '{profile.Name}': {ex.Message}");
                }

            }

            return results;
        });
    }
}
