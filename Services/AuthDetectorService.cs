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
            var profileDir = profile.GetEffectiveProfileDirectory();

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

            // Check 1: ~/.gemini/google_accounts.json
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
                    // Ignore parse errors, continue to other checks
                }
            }

            // Check 2: ~/.gemini/antigravity-cli/antigravity-oauth-token
            var oauthTokenPath = Path.Combine(geminiDir, "antigravity-cli", "antigravity-oauth-token");
            if (File.Exists(oauthTokenPath))
            {
                status.TokenModifiedAt = File.GetLastWriteTime(oauthTokenPath);
                try
                {
                    var tokenJson = File.ReadAllText(oauthTokenPath);
                    using var doc = JsonDocument.Parse(tokenJson);
                    
                    // Try to extract email or identity if present in token payload
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

                    status.Status = AuthStatusType.Authenticated;
                    status.StatusMessage = string.IsNullOrEmpty(status.AccountEmail)
                        ? "Authenticated"
                        : $"Logged in ({status.AccountEmail})";
                    return status;
                }
                catch
                {
                    status.Status = AuthStatusType.Error;
                    status.StatusMessage = "Corrupt OAuth token file";
                    return status;
                }
            }

            // Check 3: If google_accounts.json had an active email or antigravity-cli directory exists
            var cliDir = Path.Combine(geminiDir, "antigravity-cli");
            if (!string.IsNullOrEmpty(status.AccountEmail))
            {
                status.Status = AuthStatusType.Authenticated;
                status.StatusMessage = $"Logged in ({status.AccountEmail})";
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
}
