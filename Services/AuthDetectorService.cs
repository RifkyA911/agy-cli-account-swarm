using System;
using System.IO;
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

            if (!Directory.Exists(profileDir))
            {
                status.Status = AuthStatusType.NotInitialized;
                status.StatusMessage = "Directory not created yet";
                return status;
            }

            var geminiDir = Path.Combine(profileDir, ".gemini");
            if (!Directory.Exists(geminiDir))
            {
                status.Status = AuthStatusType.NeedsLogin;
                status.StatusMessage = "Needs initial login";
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

            // 2. Check ~/.gemini/google_accounts.json
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

            // 3. Check ~/.gemini/antigravity-cli/antigravity-oauth-token
            var oauthTokenPath = Path.Combine(cliDir, "antigravity-oauth-token");
            if (File.Exists(oauthTokenPath))
            {
                status.TokenModifiedAt = File.GetLastWriteTime(oauthTokenPath);
                try
                {
                    var tokenJson = File.ReadAllText(oauthTokenPath);
                    using var doc = JsonDocument.Parse(tokenJson);
                    
                    if (string.IsNullOrEmpty(status.AccountEmail))
                    {
                        if (doc.RootElement.TryGetProperty("user_email", out var emailProp))
                        {
                            status.AccountEmail = emailProp.GetString();
                        }
                        else if (doc.RootElement.TryGetProperty("email", out var emailProp2))
                        {
                            status.AccountEmail = emailProp2.GetString();
                        }
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

            // 4. Resolve Avatar & Determine Tier
            if (!string.IsNullOrEmpty(status.AccountEmail))
            {
                status.AvatarUrl = $"https://profiles.google.com/s2/photos/profile/{status.AccountEmail}?sz=96";

                if (status.AccountEmail.Contains("rifkyakhmad911@gmail.com", StringComparison.OrdinalIgnoreCase))
                {
                    status.DetectedTier = "Pro";
                }
                else if (!string.IsNullOrEmpty(status.CurrentModel) &&
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

            long dailyTokensLimit = GetDailyTokensLimitForTier(effectiveTier);
            status.DailyQuotaLimit = dailyLimit;
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
                    var today = DateTime.Today;

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
                                if (doc.RootElement.TryGetProperty("timestamp", out var tsProp) && tsProp.TryGetInt64(out var ts) && ts > 0)
                                {
                                    var dt = DateTimeOffset.FromUnixTimeMilliseconds(ts).LocalDateTime;
                                    if (dt.Date == today)
                                    {
                                        todayCount++;
                                    }
                                }
                            }
                            catch { }
                        }
                    }

                    status.TotalTurnsCount = totalCount;
                    status.TodayTurnsCount = todayCount;
                    status.TodayTokensEstimated = (long)todayCount * 1950L;

                    status.UsagePercentage = Math.Min(100.0, ((double)todayCount / dailyLimit) * 100.0);
                    status.UsageLabel = $"{todayCount:N0} / {dailyLimit:N0} prompts today ({status.UsagePercentage:F1}%)";
                }
                catch
                {
                    status.UsageLabel = $"0 / {dailyLimit:N0} prompts today";
                }
            }
            else
            {
                status.UsageLabel = $"0 / {dailyLimit:N0} prompts today";
            }

            // 7. Determine Final Status
            if (File.Exists(oauthTokenPath) || !string.IsNullOrEmpty(status.AccountEmail))
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
                return status;
            }

            if (Directory.Exists(cliDir))
            {
                status.Status = AuthStatusType.NeedsLogin;
                status.StatusMessage = "Session initialized, awaiting auth";
                return status;
            }

            status.Status = AuthStatusType.NeedsLogin;
            status.StatusMessage = "Ready for login";
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
}
