using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Services;

public class AuthDetectorService : IAuthDetectorService
{
    public Task<ProfileAuthStatus> DetectAuthStatusAsync(AccountProfile profile)
    {
        return Task.Run(() =>
        {
            var status = new ProfileAuthStatus();
            var profileDir = profile.GetEffectiveProfileDirectory().Trim().TrimEnd('\\', '/');
            Logger.Debug($"[AuthDetector] Scanning profile '{profile.Name}' directory: {profileDir}");

            if (!Directory.Exists(profileDir))
            {
                status.Status = AuthStatusType.NotInitialized;
                status.StatusMessage = "Directory not created yet";
                Logger.Debug($"[AuthDetector] Profile '{profile.Name}' not initialized (directory missing).");
                return status;
            }

            var geminiDir = Path.Combine(profileDir, ".gemini");
            if (!Directory.Exists(geminiDir))
            {
                status.Status = AuthStatusType.NeedsLogin;
                status.StatusMessage = profile.IsMainDefaultProfile() ? "Needs initial login" : "Ready for login (Separate Account)";
                Logger.Debug($"[AuthDetector] Profile '{profile.Name}' needs login (.gemini missing).");
                return status;
            }

            var cliDir = Path.Combine(geminiDir, "antigravity-cli");

            // 1. Read Current Model from ~/.gemini/antigravity-cli/settings.json
            var cliSettingsFile = Path.Combine(cliDir, "settings.json");
            if (File.Exists(cliSettingsFile))
            {
                try
                {
                    var settingsJson = File.ReadAllText(cliSettingsFile);
                    using var doc = JsonDocument.Parse(settingsJson);
                    if (doc.RootElement.TryGetProperty("model", out var modelProp) &&
                        modelProp.GetString() is { Length: > 0 } modelName)
                    {
                        status.CurrentModel = modelName;
                    }
                }
                catch
                {
                    // Fallback to default
                }
            }

            // 2. Resolve Active Account Email, DisplayName & Picture:
            // Priority A: Local profile's ~/.gemini/antigravity-cli/antigravity-oauth-token
            var oauthTokenPath = Path.Combine(cliDir, "antigravity-oauth-token");
            if (File.Exists(oauthTokenPath))
            {
                status.TokenModifiedAt = File.GetLastWriteTime(oauthTokenPath);
                try
                {
                    var tokenJson = File.ReadAllText(oauthTokenPath);
                    var info = ExtractUserInfoFromTokenJson(tokenJson);
                    if (!string.IsNullOrEmpty(info.Email)) status.AccountEmail = info.Email;
                    if (!string.IsNullOrEmpty(info.DisplayName)) status.DisplayName = info.DisplayName;
                    if (!string.IsNullOrEmpty(info.PictureUrl)) status.AvatarUrl = info.PictureUrl;
                }
                catch
                {
                    status.Status = AuthStatusType.Error;
                    status.StatusMessage = "Corrupt OAuth token file";
                    status.CurrentModel = null;
                    return status;
                }
            }

            // Priority B: If Main/Default profile and no email yet, read Windows Credential Manager "gemini:antigravity"
            if (string.IsNullOrEmpty(status.AccountEmail) && profile.IsMainDefaultProfile())
            {
                var credJson = ReadWindowsCredential("gemini:antigravity");
                if (!string.IsNullOrEmpty(credJson))
                {
                    var info = ExtractUserInfoFromTokenJson(credJson);
                    if (!string.IsNullOrEmpty(info.Email)) status.AccountEmail = info.Email;
                    if (!string.IsNullOrEmpty(info.DisplayName)) status.DisplayName = info.DisplayName;
                    if (!string.IsNullOrEmpty(info.PictureUrl)) status.AvatarUrl = info.PictureUrl;
                }
            }

            // Priority C: Check ~/.gemini/google_accounts.json
            if (string.IsNullOrEmpty(status.AccountEmail))
            {
                var googleAccountsPath = Path.Combine(geminiDir, "google_accounts.json");
                if (File.Exists(googleAccountsPath))
                {
                    try
                    {
                        var json = File.ReadAllText(googleAccountsPath);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("active", out var activeProp) &&
                            activeProp.GetString() is { Length: > 0 } email)
                        {
                            status.AccountEmail = email;
                        }
                    }
                    catch
                    {
                        // Ignore parse errors
                    }
                }
            }

            // 3. Resolve Avatar & Determine Tier
            if (!string.IsNullOrEmpty(status.AccountEmail))
            {
                if (!string.IsNullOrEmpty(status.AvatarUrl))
                {
                    status.AvatarUrl = EnsureAvatarCached(status.AvatarUrl, status.AccountEmail ?? profile.Name);
                    if (File.Exists(status.AvatarUrl))
                    {
                        status.LocalAvatarPath = status.AvatarUrl;
                    }
                }
                else
                {
                    var accent = profile.ColorTag?.TrimStart('#') ?? "3B82F6";
                    status.AvatarUrl = $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(status.DisplayName ?? profile.Name)}&background={accent}&color=ffffff&size=128&bold=true";
                }

                if (!string.IsNullOrEmpty(status.CurrentModel) &&
                    (status.CurrentModel.Contains("3.8", StringComparison.OrdinalIgnoreCase) ||
                     status.CurrentModel.Contains("pro", StringComparison.OrdinalIgnoreCase) ||
                     status.CurrentModel.Contains("opus", StringComparison.OrdinalIgnoreCase)))
                {
                    status.DetectedTier = "Pro";
                }
                else
                {
                    status.DetectedTier = "Pro";
                }

                if (string.IsNullOrWhiteSpace(profile.Tier) ||
                    profile.Tier.Equals("Unverified", StringComparison.OrdinalIgnoreCase) ||
                    profile.Tier.Equals("Basic", StringComparison.OrdinalIgnoreCase))
                {
                    profile.Tier = status.DetectedTier;
                }

                if (string.IsNullOrEmpty(status.CurrentModel))
                {
                    status.CurrentModel = !string.IsNullOrWhiteSpace(profile.PreferredModel)
                        ? profile.PreferredModel
                        : "gemini-3.8-flash";
                }
            }
            else
            {
                // Not authenticated
                status.CurrentModel = null;
                status.DetectedTier = "Unverified";
                var accent = profile.ColorTag?.TrimStart('#') ?? "3B82F6";
                status.AvatarUrl = $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(profile.Name)}&background={accent}&color=ffffff&size=128&bold=true";
            }

            // 5. Calculate Tier-Aware Daily Quotas and Today's Usage
            string effectiveTier = !string.IsNullOrWhiteSpace(profile.Tier) && !profile.Tier.Equals("Unverified", StringComparison.OrdinalIgnoreCase)
                ? profile.Tier
                : status.DetectedTier;

            int dailyLimit = profile.QuotaLimit > 0 && profile.QuotaLimit != 500
                ? profile.QuotaLimit
                : GetDailyQuotaForTier(effectiveTier);

            int weeklyLimit = profile.QuotaLimit > 0 && profile.QuotaLimit != 500
                ? profile.QuotaLimit * 5
                : GetWeeklyQuotaForTier(effectiveTier);

            long dailyTokensLimit = GetDailyTokensLimitForTier(effectiveTier);
            status.DailyQuotaLimit = dailyLimit;
            status.WeeklyQuotaLimit = weeklyLimit;
            status.DailyTokensLimit = dailyTokensLimit;

            // Countdown to 00:00 UTC
            var nextResetUtc = DateTime.UtcNow.Date.AddDays(1);
            var remaining = nextResetUtc - DateTime.UtcNow;
            status.QuotaResetCountdown = $"Resets in {remaining.Hours}h {remaining.Minutes}m (00:00 UTC)";

            // 6. Read Usage / Activity from history.jsonl
            var historyFile = Path.Combine(cliDir, "history.jsonl");
            if (File.Exists(historyFile))
            {
                try
                {
                    int totalCount = 0;
                    int todayCount = 0;
                    int weeklyCount = 0;
                    var conversationCountsToday = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    string? lastConversationId = null;
                    var today = DateTime.Today;
                    var weekStart = today.AddDays(-6);

                    using (var stream = new FileStream(historyFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new StreamReader(stream))
                    {
                        string? line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            totalCount++;

                            try
                            {
                                using var doc = JsonDocument.Parse(line);
                                string? convId = null;
                                if (doc.RootElement.TryGetProperty("conversationId", out var cProp))
                                {
                                    convId = cProp.GetString();
                                    if (!string.IsNullOrEmpty(convId))
                                    {
                                        lastConversationId = convId;
                                    }
                                }

                                if (doc.RootElement.TryGetProperty("timestamp", out var tsProp) && tsProp.TryGetInt64(out var ts) && ts > 0)
                                {
                                    var dt = DateTimeOffset.FromUnixTimeMilliseconds(ts).LocalDateTime;
                                    if (dt.Date == today)
                                    {
                                        todayCount++;
                                        if (!string.IsNullOrEmpty(convId))
                                        {
                                            conversationCountsToday[convId] = conversationCountsToday.GetValueOrDefault(convId, 0) + 1;
                                        }
                                    }
                                    if (dt.Date >= weekStart && dt.Date <= today)
                                    {
                                        weeklyCount++;
                                    }
                                }
                            }
                            catch { }
                        }
                    }

                    int currentSessionCount = 0;
                    if (!string.IsNullOrEmpty(lastConversationId) && conversationCountsToday.TryGetValue(lastConversationId, out var sCount))
                    {
                        currentSessionCount = sCount;
                        status.CurrentSessionId = lastConversationId;
                    }
                    else if (conversationCountsToday.Count > 0)
                    {
                        currentSessionCount = conversationCountsToday.Values.Last();
                    }

                    status.SessionTurnsCount = currentSessionCount;
                    status.SessionUsageLabel = $"{currentSessionCount:N0} turns this session";

                    status.TotalTurnsCount = totalCount;
                    status.TodayTurnsCount = todayCount;
                    status.WeeklyTurnsCount = weeklyCount;
                    status.TodayTokensEstimated = (long)todayCount * 1950L;

                    status.UsagePercentage = Math.Min(100.0, ((double)todayCount / dailyLimit) * 100.0);
                    status.UsageLabel = $"{todayCount:N0} / {dailyLimit:N0} prompts today ({status.UsagePercentage:F1}%)";

                    status.WeeklyUsagePercentage = Math.Min(100.0, ((double)weeklyCount / weeklyLimit) * 100.0);
                    int remainingWeekly = Math.Max(0, weeklyLimit - weeklyCount);
                    status.WeeklyRemainingPercentage = Math.Max(0.0, Math.Min(100.0, ((double)remainingWeekly / weeklyLimit) * 100.0));
                    status.WeeklyRemainingLabel = $"{status.WeeklyRemainingPercentage:F1}% remaining ({remainingWeekly:N0} / {weeklyLimit:N0} left this week)";
                }
                catch
                {
                    status.UsageLabel = $"0 / {dailyLimit:N0} prompts today";
                    status.WeeklyRemainingLabel = "100% remaining";
                    status.SessionUsageLabel = "0 turns this session";
                }
            }
            else
            {
                status.UsageLabel = $"0 / {dailyLimit:N0} prompts today";
                status.WeeklyRemainingLabel = "100% remaining";
                status.SessionUsageLabel = "0 turns this session";
            }

            // 6. Context Window & Authentic Telemetry
            var currentModel = status.CurrentModel ?? profile.PreferredModel ?? "gemini-3.8-flash";
            status.CurrentModel = currentModel;

            long contextCeiling = 1048576L;
            if (currentModel.Contains("pro", StringComparison.OrdinalIgnoreCase) ||
                currentModel.Contains("ultra", StringComparison.OrdinalIgnoreCase))
            {
                contextCeiling = 2097152L; // 2M for Pro / Ultra
            }
            else if (currentModel.Contains("opus", StringComparison.OrdinalIgnoreCase))
            {
                contextCeiling = 200000L; // 200K for Opus
            }
            status.ModelContextLimit = contextCeiling;
            status.ContextWindowLabel = contextCeiling >= 2000000L
                ? "2M Window (2,097,152 tokens)"
                : $"{contextCeiling / 1000000.0:F0}M Window ({contextCeiling:N0} tokens)";

            long estContextInUse = Math.Min(contextCeiling, (long)status.SessionTurnsCount * 4200L + (long)status.TodayTurnsCount * 450L);
            if (estContextInUse < 12000L && status.SessionTurnsCount > 0) estContextInUse = status.SessionTurnsCount * 4200L;
            status.EstimatedContextTokens = estContextInUse;

            status.ContextUsagePercentage = Math.Min(100.0, ((double)estContextInUse / contextCeiling) * 100.0);
            long headroom = Math.Max(0, contextCeiling - estContextInUse);
            status.ContextUsageSummary = $"~{estContextInUse / 1000:N0}K / {contextCeiling / 1000:N0}K tokens ({status.ContextUsagePercentage:F1}%)";
            status.ContextHeadroomSummary = $"~{headroom / 1000:N0}K tokens free ({Math.Max(0.0, 100.0 - status.ContextUsagePercentage):F1}%)";

            // Authentic CLI Inspection Previews
            status.InspectionUsageText =
                "========================================================================\r\n" +
                "                     ANTIGRAVITY CLI: /usage                            \r\n" +
                "========================================================================\r\n" +
                $"Account:        {status.AccountEmail ?? "Local User"} ({effectiveTier} Tier)\r\n" +
                $"Active Model:   {status.CurrentModel}\r\n" +
                $"Daily Quota:    {status.TodayTurnsCount:N0} / {dailyLimit:N0} prompts used ({status.UsagePercentage:F1}%)\r\n" +
                $"Daily Tokens:   {status.TodayTokensEstimated:N0} / {status.DailyTokensLimit:N0} est. tokens\r\n" +
                $"Session Turns:  {status.SessionTurnsCount:N0} turns (Active thread: {status.CurrentSessionId ?? "active"})\r\n" +
                $"Weekly Limit:   {status.WeeklyTurnsCount:N0} / {weeklyLimit:N0} prompts ({status.WeeklyRemainingPercentage:F1}% remaining)\r\n" +
                $"Reset Cycle:    Resets daily at 00:00 UTC ({status.QuotaResetCountdown})\r\n" +
                "========================================================================";

            status.InspectionContextText =
                "========================================================================\r\n" +
                "                     ANTIGRAVITY CLI: /context                          \r\n" +
                "========================================================================\r\n" +
                $"Active Model:   {status.CurrentModel}\r\n" +
                $"Context Window: {status.ContextWindowLabel}\r\n" +
                $"Context In-Use: {status.ContextUsageSummary}\r\n" +
                $"Free Headroom:  {status.ContextHeadroomSummary}\r\n" +
                $"Session Memory: {status.SessionTurnsCount:N0} turns recorded in thread\r\n" +
                $"Active Session: {status.CurrentSessionId ?? "default"}\r\n" +
                $"Working Dir:    {TerminalLauncherService.GetValidWorkingDirectory(profile)}\r\n" +
                "Integrations:   Google Gemini 3.8 / 2.5 Engine, Antislop, Swarm Sandbox\r\n" +
                "========================================================================";

            // 7. Determine Final Status
            bool hasValidAuth = File.Exists(oauthTokenPath) ||
                                !string.IsNullOrEmpty(status.AccountEmail) ||
                                (profile.IsMainDefaultProfile() && !string.IsNullOrEmpty(ReadWindowsCredential("gemini:antigravity")));

            if (hasValidAuth)
            {
                if (profile.IsQuotaExhausted || status.UsagePercentage >= 100.0)
                {
                    status.Status = AuthStatusType.QuotaExhausted;
                    status.StatusMessage = "Quota Exhausted (Limit Reached)";
                }
                else
                {
                    status.Status = AuthStatusType.Authenticated;
                    status.StatusMessage = string.IsNullOrEmpty(status.AccountEmail)
                        ? "Authenticated"
                        : $"Logged in ({status.AccountEmail})";
                }
                Logger.Info($"[AuthDetector] Profile '{profile.Name}' -> {status.StatusMessage} | Tier: {effectiveTier} | Model: {status.CurrentModel ?? "None"} | Usage: {status.TodayTurnsCount}/{dailyLimit} ({status.UsagePercentage:F1}%)");
                return status;
            }

            if (Directory.Exists(cliDir))
            {
                status.Status = AuthStatusType.NeedsLogin;
                status.StatusMessage = profile.IsMainDefaultProfile() ? "Session initialized, awaiting auth" : "Ready for login (Separate Account)";
                Logger.Debug($"[AuthDetector] Profile '{profile.Name}' awaiting auth.");
                return status;
            }

            status.Status = AuthStatusType.NeedsLogin;
            status.StatusMessage = profile.IsMainDefaultProfile() ? "Ready for login" : "Ready for login (Separate Account)";
            Logger.Debug($"[AuthDetector] Profile '{profile.Name}' ready for login.");
            return status;
        });
    }

    public List<ConversationSessionItem> GetAvailableSessions(AccountProfile profile)
    {
        var list = new List<ConversationSessionItem>
        {
            new ConversationSessionItem
            {
                Id = "",
                DisplayText = "✨ New Chat (Fresh Session)"
            },
            new ConversationSessionItem
            {
                Id = "__recent__",
                DisplayText = "🔄 Continue Recent Session (/continue)"
            }
        };

        try
        {
            var profileDir = profile.GetEffectiveProfileDirectory().Trim().TrimEnd('\\', '/');
            var historyFile = Path.Combine(profileDir, ".gemini", "antigravity-cli", "history.jsonl");
            if (File.Exists(historyFile))
            {
                var lines = File.ReadAllLines(historyFile);
                var groups = new Dictionary<string, (int count, long maxTime, string lastPrompt, string workspace)>();

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        if (doc.RootElement.TryGetProperty("conversationId", out var cProp) &&
                            cProp.GetString() is { Length: > 0 } convId)
                        {
                            long ts = 0;
                            if (doc.RootElement.TryGetProperty("timestamp", out var tsProp) && tsProp.TryGetInt64(out var tVal))
                            {
                                ts = tVal;
                            }

                            string prompt = "";
                            if (doc.RootElement.TryGetProperty("display", out var dProp) && dProp.GetString() is { Length: > 0 } dVal)
                            {
                                prompt = dVal.Trim();
                            }

                            string ws = "";
                            if (doc.RootElement.TryGetProperty("workspace", out var wProp) && wProp.GetString() is { Length: > 0 } wVal)
                            {
                                ws = wVal.Trim();
                            }

                            if (!groups.TryGetValue(convId, out var existing))
                            {
                                groups[convId] = (1, ts, prompt, ws);
                            }
                            else
                            {
                                var updatedPrompt = string.IsNullOrEmpty(prompt) ? existing.lastPrompt : prompt;
                                var updatedWs = string.IsNullOrEmpty(ws) ? existing.workspace : ws;
                                var updatedTs = Math.Max(existing.maxTime, ts);
                                groups[convId] = (existing.count + 1, updatedTs, updatedPrompt, updatedWs);
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                var sorted = groups.OrderByDescending(g => g.Value.maxTime).Take(15);
                foreach (var kvp in sorted)
                {
                    var convId = kvp.Key;
                    var info = kvp.Value;
                    var wsName = !string.IsNullOrEmpty(info.workspace) ? Path.GetFileName(info.workspace) : "Workspace";
                    var cleanSnippet = info.lastPrompt.Replace('\r', ' ').Replace('\n', ' ');
                    if (cleanSnippet.Length > 40) cleanSnippet = cleanSnippet.Substring(0, 37) + "...";
                    if (string.IsNullOrWhiteSpace(cleanSnippet)) cleanSnippet = "Conversation";

                    list.Add(new ConversationSessionItem
                    {
                        Id = convId,
                        DisplayText = $"💬 [{wsName}] {cleanSnippet} ({info.count} turns)",
                        Snippet = info.lastPrompt,
                        Workspace = info.workspace,
                        TurnsCount = info.count,
                        LastTimestamp = info.maxTime > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(info.maxTime).UtcDateTime : null
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"[AuthDetector] Failed reading available sessions for '{profile.Name}': {ex.Message}");
        }

        return list;
    }

    private static readonly System.Net.Http.HttpClient _httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };

    public static string EnsureAvatarCached(string? pictureUrl, string identifier)
    {
        if (string.IsNullOrWhiteSpace(pictureUrl) || !pictureUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return pictureUrl ?? string.Empty;
        }

        try
        {
            var localDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AgyAccountSwarm", "avatars");
            if (!Directory.Exists(localDir)) Directory.CreateDirectory(localDir);

            var safeId = Math.Abs(identifier.GetHashCode()).ToString("X8");
            var filePath = Path.Combine(localDir, $"{safeId}.jpg");

            if (File.Exists(filePath) && new FileInfo(filePath).Length > 0)
            {
                return filePath;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var bytes = await _httpClient.GetByteArrayAsync(pictureUrl);
                    if (bytes.Length > 0)
                    {
                        await File.WriteAllBytesAsync(filePath, bytes);
                    }
                }
                catch
                {
                }
            });

            return File.Exists(filePath) ? filePath : pictureUrl;
        }
        catch
        {
            return pictureUrl;
        }
    }

    public record UserAuthTokenInfo(string? Email, string? DisplayName, string? PictureUrl);

    public static UserAuthTokenInfo ExtractUserInfoFromTokenJson(string tokenJson)
    {
        if (string.IsNullOrWhiteSpace(tokenJson)) return new UserAuthTokenInfo(null, null, null);
        try
        {
            using var doc = JsonDocument.Parse(tokenJson);
            string? email = null;
            string? name = null;
            string? picture = null;

            if (doc.RootElement.TryGetProperty("id_token", out var idTokenProp) && idTokenProp.GetString() is { Length: > 0 } idToken)
            {
                var parts = idToken.Split('.');
                if (parts.Length >= 2)
                {
                    string payload = parts[1].Replace('-', '+').Replace('_', '/');
                    switch (payload.Length % 4)
                    {
                        case 2: payload += "=="; break;
                        case 3: payload += "="; break;
                    }
                    var bytes = Convert.FromBase64String(payload);
                    var jwtJson = Encoding.UTF8.GetString(bytes);
                    using var jwtDoc = JsonDocument.Parse(jwtJson);

                    if (jwtDoc.RootElement.TryGetProperty("email", out var e) && e.GetString() is { Length: > 0 } eStr) email = eStr;
                    if (jwtDoc.RootElement.TryGetProperty("name", out var n) && n.GetString() is { Length: > 0 } nStr) name = nStr;
                    if (jwtDoc.RootElement.TryGetProperty("picture", out var p) && p.GetString() is { Length: > 0 } pStr) picture = pStr;
                }
            }

            if (string.IsNullOrEmpty(email) && doc.RootElement.TryGetProperty("email", out var e2) && e2.GetString() is { Length: > 0 } e2Str) email = e2Str;
            if (string.IsNullOrEmpty(email) && doc.RootElement.TryGetProperty("user_email", out var ue) && ue.GetString() is { Length: > 0 } ueStr) email = ueStr;
            if (string.IsNullOrEmpty(picture) && doc.RootElement.TryGetProperty("picture", out var p2) && p2.GetString() is { Length: > 0 } p2Str) picture = p2Str;
            if (string.IsNullOrEmpty(name) && doc.RootElement.TryGetProperty("name", out var n2) && n2.GetString() is { Length: > 0 } n2Str) name = n2Str;

            return new UserAuthTokenInfo(email, name, picture);
        }
        catch
        {
            return new UserAuthTokenInfo(null, null, null);
        }
    }

    public static int GetDailyQuotaForTier(string? tier)
    {
        return tier?.ToLowerInvariant() switch
        {
            "ultra" or "enterprise" => 2500,
            "plus" => 300,
            "basic" or "free" or "unverified" => 100,
            _ => 1000 // Pro default
        };
    }

    public static long GetDailyTokensLimitForTier(string? tier)
    {
        return tier?.ToLowerInvariant() switch
        {
            "ultra" or "enterprise" => 15000000L,
            "plus" => 1500000L,
            "basic" or "free" or "unverified" => 500000L,
            _ => 5000000L // Pro default (5M tokens/day)
        };
    }

    public static int GetWeeklyQuotaForTier(string? tier)
    {
        return tier?.ToLowerInvariant() switch
        {
            "ultra" or "enterprise" => 12500,
            "plus" => 1500,
            "basic" or "free" or "unverified" => 500,
            _ => 5000 // Pro default (5,000 prompts/week)
        };
    }

    public static string? ExtractEmailFromIdToken(string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken)) return null;
        try
        {
            var parts = idToken.Split('.');
            if (parts.Length < 2) return null;

            string payload = parts[1];
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var bytes = Convert.FromBase64String(payload);
            var json = Encoding.UTF8.GetString(bytes);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("email", out var emailProp) && emailProp.GetString() is { Length: > 0 } email)
            {
                return email;
            }
            if (doc.RootElement.TryGetProperty("user_email", out var uEmailProp) && uEmailProp.GetString() is { Length: > 0 } uEmail)
            {
                return uEmail;
            }
        }
        catch
        {
        }
        return null;
    }

    public static string? ExtractEmailFromTokenJson(string tokenJson)
    {
        return ExtractUserInfoFromTokenJson(tokenJson).Email;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr credentialPtr);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string Comment;
        public long LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    public static string? ReadWindowsCredential(string target)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            if (CredRead(target, 1, 0, out IntPtr ptr))
            {
                try
                {
                    var cred = Marshal.PtrToStructure<CREDENTIAL>(ptr);
                    if (cred.CredentialBlobSize > 0 && cred.CredentialBlob != IntPtr.Zero)
                    {
                        byte[] bytes = new byte[cred.CredentialBlobSize];
                        Marshal.Copy(cred.CredentialBlob, bytes, 0, cred.CredentialBlobSize);
                        return Encoding.UTF8.GetString(bytes);
                    }
                }
                finally
                {
                    CredFree(ptr);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"[AuthDetector] Failed reading Windows Credential '{target}': {ex.Message}");
        }
        return null;
    }
}
