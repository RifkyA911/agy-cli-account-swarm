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

            // 2. Resolve Active Account Email:
            // Priority A: Local profile's ~/.gemini/antigravity-cli/antigravity-oauth-token
            var oauthTokenPath = Path.Combine(cliDir, "antigravity-oauth-token");
            if (File.Exists(oauthTokenPath))
            {
                status.TokenModifiedAt = File.GetLastWriteTime(oauthTokenPath);
                try
                {
                    var tokenJson = File.ReadAllText(oauthTokenPath);
                    var extracted = ExtractEmailFromTokenJson(tokenJson);
                    if (!string.IsNullOrEmpty(extracted))
                    {
                        status.AccountEmail = extracted;
                    }
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
                    var extracted = ExtractEmailFromTokenJson(credJson);
                    if (!string.IsNullOrEmpty(extracted))
                    {
                        status.AccountEmail = extracted;
                    }
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
                status.AvatarUrl = $"https://profiles.google.com/s2/photos/profile/{status.AccountEmail}?sz=96";

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
        if (string.IsNullOrWhiteSpace(tokenJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(tokenJson);
            if (doc.RootElement.TryGetProperty("id_token", out var idTokenProp) && idTokenProp.GetString() is { Length: > 0 } idToken)
            {
                var email = ExtractEmailFromIdToken(idToken);
                if (!string.IsNullOrEmpty(email)) return email;
            }
            if (doc.RootElement.TryGetProperty("email", out var emailProp) && emailProp.GetString() is { Length: > 0 } email2)
            {
                return email2;
            }
            if (doc.RootElement.TryGetProperty("user_email", out var uEmailProp) && uEmailProp.GetString() is { Length: > 0 } uEmail2)
            {
                return uEmail2;
            }
        }
        catch
        {
        }
        return null;
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
