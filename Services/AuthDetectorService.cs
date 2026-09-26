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

            // 2. Read Usage / Activity from history.jsonl
            var historyFile = Path.Combine(cliDir, "history.jsonl");
            if (File.Exists(historyFile))
            {
                try
                {
                    int lineCount = 0;
                    using var reader = new StreamReader(historyFile);
                    while (reader.ReadLine() != null)
                    {
                        lineCount++;
                    }
                    status.TotalTurnsCount = lineCount;
                    int maxQuota = profile.QuotaLimit > 0 ? profile.QuotaLimit : 500;
                    status.UsagePercentage = Math.Min(100.0, ((double)lineCount / maxQuota) * 100.0);
                    status.UsageLabel = $"{lineCount} / {maxQuota} prompts";
                }
                catch
                {
                    status.UsageLabel = "0 prompts run";
                }
            }

            // 3. Check ~/.gemini/google_accounts.json
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
                    // Ignore parse errors, continue
                }
            }

            // 4. Check ~/.gemini/antigravity-cli/antigravity-oauth-token
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

                    if (string.IsNullOrEmpty(status.CurrentModel))
                    {
                        status.CurrentModel = !string.IsNullOrWhiteSpace(profile.PreferredModel)
                            ? profile.PreferredModel
                            : "gemini-2.5-flash";
                    }

                    return status;
                }
                catch
                {
                    status.Status = AuthStatusType.Error;
                    status.StatusMessage = "Corrupt OAuth token file";
                    status.CurrentModel = null;
                    return status;
                }
            }

            if (!string.IsNullOrEmpty(status.AccountEmail))
            {
                if (profile.IsQuotaExhausted || status.UsagePercentage >= 100.0)
                {
                    status.Status = AuthStatusType.QuotaExhausted;
                    status.StatusMessage = "Quota Exhausted (Limit Reached)";
                }
                else
                {
                    status.Status = AuthStatusType.Authenticated;
                    status.StatusMessage = $"Logged in ({status.AccountEmail})";
                }

                if (string.IsNullOrEmpty(status.CurrentModel))
                {
                    status.CurrentModel = !string.IsNullOrWhiteSpace(profile.PreferredModel)
                        ? profile.PreferredModel
                        : "gemini-2.5-flash";
                }

                return status;
            }

            // If not authenticated, do not show active model!
            status.CurrentModel = null;

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
}
