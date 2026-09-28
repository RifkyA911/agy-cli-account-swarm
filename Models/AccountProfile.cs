using System;
using System.Text.Json.Serialization;

namespace AgyAccountSwarm.Models;

public class AccountProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ColorTag { get; set; } = "#3B82F6"; // Default blue
    
    /// <summary>
    /// Custom isolated profile directory. If empty, defaults to %USERPROFILE%\.gemini-profiles\{Name}
    /// </summary>
    public string? CustomProfilePath { get; set; }

    /// <summary>
    /// Default project directory / workspace to launch agy in.
    /// </summary>
    public string? DefaultWorkspace { get; set; }

    /// <summary>
    /// Additional arguments passed to agy (e.g. --dangerously-skip-permissions, --model ...)
    /// </summary>
    public string? ExtraArguments { get; set; }

    /// <summary>
    /// Whether this profile is selected for Swarm Launch (batch execution).
    /// </summary>
    public bool IsSelectedForSwarm { get; set; } = true;

    /// <summary>
    /// Subscription tier: Basic, Plus, Pro, Ultra, or Unverified (default before login)
    /// </summary>
    public string Tier { get; set; } = "Unverified";

    /// <summary>
    /// Target / preferred model (e.g. gemini-2.5-flash, gemini-2.5-pro, claude-3.7-sonnet)
    /// </summary>
    public string PreferredModel { get; set; } = "gemini-2.5-flash";

    /// <summary>
    /// Quota limit in prompt turns before warning/exhaustion.
    /// </summary>
    public int QuotaLimit { get; set; } = 500;

    /// <summary>
    /// Manually or automatically flagged as quota exhausted.
    /// </summary>
    public bool IsQuotaExhausted { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLaunchedAt { get; set; }

    [JsonIgnore]
    public ProfileAuthStatus AuthStatus { get; set; } = new();

    public string GetEffectiveProfileDirectory()
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var profilesBase = System.IO.Path.Combine(userHome, ".gemini-profiles");

        if (!string.IsNullOrWhiteSpace(CustomProfilePath))
        {
            var expanded = Environment.ExpandEnvironmentVariables(CustomProfilePath.Trim());
            try
            {
                var fullPath = System.IO.Path.GetFullPath(expanded);
                var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                var sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                var root = System.IO.Path.GetPathRoot(fullPath);

                // Prevent pointing to root drive or Windows system directories
                if (!string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase) &&
                    !fullPath.StartsWith(winDir, StringComparison.OrdinalIgnoreCase) &&
                    !fullPath.StartsWith(sysDir, StringComparison.OrdinalIgnoreCase))
                {
                    return fullPath;
                }
            }
            catch
            {
                // Fall back to safe sandbox directory if invalid path
            }
        }

        var safeName = SanitizeFolderName(Name);
        var target = System.IO.Path.Combine(profilesBase, safeName);
        try
        {
            var fullTarget = System.IO.Path.GetFullPath(target);
            var fullBase = System.IO.Path.GetFullPath(profilesBase);
            if (fullTarget.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase))
            {
                return fullTarget;
            }
        }
        catch
        {
            // Fall back
        }

        return System.IO.Path.Combine(profilesBase, "safe_profile");
    }

    public bool IsMainDefaultProfile()
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\', '/');
        var effectiveDir = GetEffectiveProfileDirectory().TrimEnd('\\', '/');
        return effectiveDir.Equals(userHome, StringComparison.OrdinalIgnoreCase);
    }

    public static string SanitizeFolderName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "unnamed_profile";

        // Remove path traversal sequences and separators
        var cleaned = name.Replace('/', '_').Replace('\\', '_').Replace("..", "_");
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        cleaned = string.Join("_", cleaned.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();
        cleaned = cleaned.Trim('.', ' ');

        return string.IsNullOrEmpty(cleaned) ? "unnamed_profile" : cleaned;
    }
}
