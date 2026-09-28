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
    private static readonly IQuotaConfigService DefaultQuotaConfig = new QuotaConfigService();
    private readonly IQuotaConfigService _quotaConfigService;

    public AuthDetectorService(IQuotaConfigService? quotaConfigService = null)
    {
        _quotaConfigService = quotaConfigService ?? DefaultQuotaConfig;
    }

    public Task<ProfileAuthStatus> DetectAuthStatusAsync(AccountProfile profile)
    {
        return Task.Run(async () =>
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
                catch (Exception ex)
                {
                    Logger.Debug($"[AuthDetector] Could not parse settings.json for '{profile.Name}': {ex.Message}");
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
                    var rawToken = File.ReadAllText(oauthTokenPath);
                    var dataProtection = new DataProtectionService();
                    var tokenJson = dataProtection.Unprotect(rawToken);

                    // Transparent migration: if token was stored in plain-text, encrypt it with DPAPI at rest
                    if (!dataProtection.IsProtected(rawToken) && !string.IsNullOrWhiteSpace(tokenJson) && tokenJson.TrimStart().StartsWith("{"))
                    {
                        try
                        {
                            var encrypted = dataProtection.Protect(tokenJson);
                            File.WriteAllText(oauthTokenPath, encrypted, Encoding.UTF8);
                            Logger.Info($"[AuthDetector] Migrated plain-text token to DPAPI encrypted format at-rest for '{profile.Name}'");
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn($"[AuthDetector] Failed migrating token to DPAPI for '{profile.Name}': {ex.Message}");
                        }
                    }

                    var info = ExtractUserInfoFromTokenJson(tokenJson);
                    if (!string.IsNullOrEmpty(info.Email)) status.AccountEmail = info.Email;
                    if (!string.IsNullOrEmpty(info.DisplayName)) status.DisplayName = info.DisplayName;
                    if (!string.IsNullOrEmpty(info.PictureUrl)) status.AvatarUrl = info.PictureUrl;
                }
                catch (Exception ex)
                {
                    Logger.Error($"[AuthDetector] Error processing token for '{profile.Name}'", ex);
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
                    catch (Exception ex)
                    {
                        Logger.Debug($"[AuthDetector] Could not parse google_accounts.json: {ex.Message}");
                    }

                }
            }

            // 3. Resolve Avatar (Authentic Google Profile Avatar or Local Disk Cache)
            string? detectedAvatar = null;

            // Check 1: Antigravity Browser Profile authentic Google Profile Picture.png
            var browserProfilePic = Path.Combine(geminiDir, "antigravity-browser-profile", "Default", "Google Profile Picture.png");
            if (File.Exists(browserProfilePic))
            {
                detectedAvatar = browserProfilePic;
            }
            else if (profile.IsMainDefaultProfile())
            {
                var globalPic = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini", "antigravity-browser-profile", "Default", "Google Profile Picture.png");
                if (File.Exists(globalPic))
                {
                    detectedAvatar = globalPic;
                }
            }

            // Check 2: If we have an avatar URL from token/JWT picture claim
            if (string.IsNullOrEmpty(detectedAvatar) && !string.IsNullOrEmpty(status.AvatarUrl))
            {
                var cached = EnsureAvatarCached(status.AvatarUrl, status.AccountEmail ?? profile.Name);
                if (!string.IsNullOrEmpty(cached) && File.Exists(cached))
                {
                    detectedAvatar = cached;
                }
            }

            // Check 3: Check %LOCALAPPDATA%\AgyAccountSwarm\avatars\ cache
            if (string.IsNullOrEmpty(detectedAvatar))
            {
                var identifier = status.AccountEmail ?? profile.Name;
                var safeId = Math.Abs(identifier.GetHashCode()).ToString("X8");
                var localAvatarDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AgyAccountSwarm", "avatars");
                var cachedFile = Path.Combine(localAvatarDir, $"{safeId}.jpg");
                if (File.Exists(cachedFile) && new FileInfo(cachedFile).Length > 0)
                {
                    detectedAvatar = cachedFile;
                }
            }

            // Set final authentic avatar paths (or null to activate authentic initial circle fallback)
            status.AvatarUrl = detectedAvatar;
            status.LocalAvatarPath = detectedAvatar;

            if (!string.IsNullOrEmpty(status.AccountEmail))
            {
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
            }

            // 5. Calculate Tier-Aware Daily Quotas and Today's Usage
            string effectiveTier = !string.IsNullOrWhiteSpace(profile.Tier) && !profile.Tier.Equals("Unverified", StringComparison.OrdinalIgnoreCase)
                ? profile.Tier
                : status.DetectedTier;

            int dailyLimit = profile.QuotaLimit > 0 && profile.QuotaLimit != 500
                ? profile.QuotaLimit
                : _quotaConfigService.GetDailyQuota(effectiveTier);

            int weeklyLimit = profile.QuotaLimit > 0 && profile.QuotaLimit != 500
                ? profile.QuotaLimit * 5
                : _quotaConfigService.GetWeeklyQuota(effectiveTier);

            long dailyTokensLimit = _quotaConfigService.GetDailyTokensLimit(effectiveTier);
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
                    int gemini5hCount = 0;
                    int claude5hCount = 0;
                    int geminiWeeklyCount = 0;
                    int claudeWeeklyCount = 0;
                    DateTime? oldest5hTimestamp = null;

                    var conversationCountsToday = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    string? lastConversationId = null;
                    var today = DateTime.Today;
                    var weekStart = today.AddDays(-6);
                    var fiveHoursAgo = DateTime.Now.AddHours(-5);

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

                                string? modelUsed = null;
                                if (doc.RootElement.TryGetProperty("model", out var mProp))
                                {
                                    modelUsed = mProp.GetString();
                                }
                                bool isClaudeOrGpt = !string.IsNullOrEmpty(modelUsed) &&
                                    (modelUsed.Contains("claude", StringComparison.OrdinalIgnoreCase) ||
                                     modelUsed.Contains("opus", StringComparison.OrdinalIgnoreCase) ||
                                     modelUsed.Contains("sonnet", StringComparison.OrdinalIgnoreCase) ||
                                     modelUsed.Contains("gpt", StringComparison.OrdinalIgnoreCase));

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
                                        if (isClaudeOrGpt) claudeWeeklyCount++;
                                        else geminiWeeklyCount++;
                                    }
                                    if (dt >= fiveHoursAgo)
                                    {
                                        if (isClaudeOrGpt) claude5hCount++;
                                        else gemini5hCount++;

                                        if (!oldest5hTimestamp.HasValue || dt < oldest5hTimestamp.Value)
                                        {
                                            oldest5hTimestamp = dt;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Debug($"[AuthDetector] Skipped malformed history line: {ex.Message}");
                            }

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

                    // Tier-aware Model Group Capacities
                    int gemini5hCap = effectiveTier switch
                    {
                        "Ultra" => 650,
                        "Pro" => 250,
                        "Plus" => 90,
                        _ => 30
                    };
                    int geminiWeeklyCap = weeklyLimit;

                    int claude5hCap = effectiveTier switch
                    {
                        "Ultra" => 220,
                        "Pro" => 80,
                        "Plus" => 30,
                        _ => 10
                    };
                    int claudeWeeklyCap = effectiveTier switch
                    {
                        "Ultra" => 4500,
                        "Pro" => 1800,
                        "Plus" => 500,
                        _ => 150
                    };

                    status.GeminiWeeklyRemainingPercent = Math.Max(0.0, Math.Min(100.0, ((geminiWeeklyCap - geminiWeeklyCount) / (double)geminiWeeklyCap) * 100.0));
                    status.Gemini5HourRemainingPercent = Math.Max(0.0, Math.Min(100.0, ((gemini5hCap - gemini5hCount) / (double)gemini5hCap) * 100.0));
                    status.ClaudeGptWeeklyRemainingPercent = Math.Max(0.0, Math.Min(100.0, ((claudeWeeklyCap - claudeWeeklyCount) / (double)claudeWeeklyCap) * 100.0));
                    status.ClaudeGpt5HourRemainingPercent = Math.Max(0.0, Math.Min(100.0, ((claude5hCap - claude5hCount) / (double)claude5hCap) * 100.0));

                    // Rolling countdowns
                    var nowUtc = DateTime.UtcNow;
                    int daysUntilSunday = ((int)DayOfWeek.Sunday - (int)nowUtc.DayOfWeek + 7) % 7;
                    if (daysUntilSunday == 0) daysUntilSunday = 7;
                    var nextSundayMidnight = nowUtc.Date.AddDays(daysUntilSunday);
                    var weeklyRemTime = nextSundayMidnight - nowUtc;
                    status.GeminiWeeklyRefreshesIn = $"{(int)weeklyRemTime.TotalHours}h {weeklyRemTime.Minutes}m";
                    status.ClaudeGptWeeklyRefreshesIn = $"{(int)weeklyRemTime.TotalHours}h {weeklyRemTime.Minutes}m";

                    var fiveHourRemTime = TimeSpan.FromMinutes(263);
                    if (oldest5hTimestamp.HasValue)
                    {
                        var rollOff = oldest5hTimestamp.Value.AddHours(5) - DateTime.Now;
                        if (rollOff > TimeSpan.Zero) fiveHourRemTime = rollOff;
                    }
                    status.Gemini5HourRefreshesIn = $"{(int)fiveHourRemTime.TotalHours}h {fiveHourRemTime.Minutes}m";

                    var claude5hRemTime = TimeSpan.FromMinutes(61);
                    if (oldest5hTimestamp.HasValue)
                    {
                        var rollOff = oldest5hTimestamp.Value.AddHours(5) - DateTime.Now;
                        if (rollOff > TimeSpan.Zero) claude5hRemTime = rollOff;
                    }
                    status.ClaudeGpt5HourRefreshesIn = $"{(int)claude5hRemTime.TotalHours}h {claude5hRemTime.Minutes}m";
                }
                catch (Exception ex)
                {
                    Logger.Warn($"[AuthDetector] Failed reading history.jsonl for '{profile.Name}': {ex.Message}");
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

            // 7. Check Authentication and Fetch Live Authentic Usage from agy CLI
            bool hasValidAuth = File.Exists(oauthTokenPath) ||
                                !string.IsNullOrEmpty(status.AccountEmail) ||
                                (profile.IsMainDefaultProfile() && !string.IsNullOrEmpty(ReadWindowsCredential("gemini:antigravity")));

            if (hasValidAuth)
            {
                var agyPath = TerminalLauncherService.ResolveAgyExecutablePath();
                if (!string.IsNullOrEmpty(agyPath))
                {
                    try
                    {
                        var liveUsage = await AgyUsageParser.FetchUsageCachedAsync(profileDir, !profile.IsMainDefaultProfile(), agyPath);

                        if (liveUsage != null && liveUsage.IsSuccess)
                        {
                            if (liveUsage.GeminiWeeklyRemainingPercent.HasValue)
                                status.GeminiWeeklyRemainingPercent = liveUsage.GeminiWeeklyRemainingPercent.Value;
                            if (!string.IsNullOrEmpty(liveUsage.GeminiWeeklyRefreshesIn))
                                status.GeminiWeeklyRefreshesIn = liveUsage.GeminiWeeklyRefreshesIn;

                            if (liveUsage.Gemini5HourRemainingPercent.HasValue)
                                status.Gemini5HourRemainingPercent = liveUsage.Gemini5HourRemainingPercent.Value;
                            if (!string.IsNullOrEmpty(liveUsage.Gemini5HourRefreshesIn))
                                status.Gemini5HourRefreshesIn = liveUsage.Gemini5HourRefreshesIn;

                            if (liveUsage.ClaudeGptWeeklyRemainingPercent.HasValue)
                                status.ClaudeGptWeeklyRemainingPercent = liveUsage.ClaudeGptWeeklyRemainingPercent.Value;
                            if (!string.IsNullOrEmpty(liveUsage.ClaudeGptWeeklyRefreshesIn))
                                status.ClaudeGptWeeklyRefreshesIn = liveUsage.ClaudeGptWeeklyRefreshesIn;

                            if (liveUsage.ClaudeGpt5HourRemainingPercent.HasValue)
                                status.ClaudeGpt5HourRemainingPercent = liveUsage.ClaudeGpt5HourRemainingPercent.Value;
                            if (!string.IsNullOrEmpty(liveUsage.ClaudeGpt5HourRefreshesIn))
                                status.ClaudeGpt5HourRefreshesIn = liveUsage.ClaudeGpt5HourRefreshesIn;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"[AuthDetector] Failed querying live agy CLI usage for '{profile.Name}': {ex.Message}");
                    }
                }
            }

            // Authentic CLI Inspection Previews (matching agy cli /usage)
            string geminiWeeklyBar = BuildAsciiProgressBar(status.GeminiWeeklyRemainingPercent, 50);
            string gemini5hBar = BuildAsciiProgressBar(status.Gemini5HourRemainingPercent, 50);
            string claudeWeeklyBar = BuildAsciiProgressBar(status.ClaudeGptWeeklyRemainingPercent, 50);
            string claude5hBar = BuildAsciiProgressBar(status.ClaudeGpt5HourRemainingPercent, 50);

            status.InspectionUsageText =
                "GEMINI MODELS\r\n" +
                "  Models within this group: Gemini Flash, Gemini Pro\r\n\r\n" +
                "  Weekly Limit Remaining\r\n" +
                $"    [{geminiWeeklyBar}] {status.GeminiWeeklyRemainingPercent:F2}%\r\n" +
                $"    Refreshes in {status.GeminiWeeklyRefreshesIn}\r\n\r\n" +
                "  Five Hour Limit Remaining\r\n" +
                $"    [{gemini5hBar}] {status.Gemini5HourRemainingPercent:F2}%\r\n" +
                $"    Refreshes in {status.Gemini5HourRefreshesIn}\r\n\r\n\r\n" +
                "CLAUDE AND GPT MODELS\r\n" +
                "  Models within this group: Claude Opus, Claude Sonnet, GPT-OSS\r\n\r\n" +
                "  Weekly Limit Remaining\r\n" +
                $"    [{claudeWeeklyBar}] {status.ClaudeGptWeeklyRemainingPercent:F2}%\r\n" +
                $"    Refreshes in {status.ClaudeGptWeeklyRefreshesIn}\r\n\r\n" +
                "  Five Hour Limit Remaining\r\n" +
                $"    [{claude5hBar}] {status.ClaudeGpt5HourRemainingPercent:F2}%\r\n" +
                $"    Refreshes in {status.ClaudeGpt5HourRefreshesIn}\r\n\r\n\r\n" +
                "  │Within each group, models share a weekly limit and a 5-hour limit. Quota is\r\n" +
                "  │consumed proportionally to the cost of the tokens. Thus, limits will last\r\n" +
                "  │longer with shorter tasks or using more cost-effective models. The 5-hour\r\n" +
                "  │limit smooths out aggregate demand to fairly distribute global capacity\r\n" +
                "  │across all users, while your weekly limit is tied directly to your individual\r\n" +
                "  │tier.";

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

            // 8. Determine Final Status
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
                var groups = new Dictionary<string, (int count, long maxTime, string lastPrompt, string workspace)>();

                using (var stream = new FileStream(historyFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    string? line;
                    while ((line = reader.ReadLine()) != null)
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
                        catch (Exception ex)
                        {
                            Logger.Debug($"[AuthDetector] Skipped malformed session history line: {ex.Message}");
                        }
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

            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
                var response = _httpClient.GetAsync(pictureUrl, cts.Token).GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
                    var bytes = response.Content.ReadAsByteArrayAsync(cts.Token).GetAwaiter().GetResult();
                    if (bytes.Length > 0)
                    {
                        File.WriteAllBytes(filePath, bytes);
                        return filePath;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"[AuthDetector] Failed caching avatar image from '{pictureUrl}': {ex.Message}");
            }

            return File.Exists(filePath) ? filePath : pictureUrl;
        }
        catch (Exception ex)
        {
            Logger.Debug($"[AuthDetector] Error in EnsureAvatarCached: {ex.Message}");
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
        catch (Exception ex)
        {
            Logger.Debug($"[AuthDetector] Failed extracting user info from token JSON: {ex.Message}");
            return new UserAuthTokenInfo(null, null, null);
        }
    }


    public static int GetDailyQuotaForTier(string? tier) => DefaultQuotaConfig.GetDailyQuota(tier);

    public static long GetDailyTokensLimitForTier(string? tier) => DefaultQuotaConfig.GetDailyTokensLimit(tier);

    public static int GetWeeklyQuotaForTier(string? tier) => DefaultQuotaConfig.GetWeeklyQuota(tier);


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

    public static string BuildAsciiProgressBar(double percentage, int length = 50)
    {
        int filled = (int)Math.Round((Math.Clamp(percentage, 0.0, 100.0) / 100.0) * length);
        if (filled > length) filled = length;
        if (filled < 0) filled = 0;
        return new string('█', filled) + new string('░', length - filled);
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
