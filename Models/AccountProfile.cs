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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLaunchedAt { get; set; }

    [JsonIgnore]
    public ProfileAuthStatus AuthStatus { get; set; } = new();

    public string GetEffectiveProfileDirectory()
    {
        if (!string.IsNullOrWhiteSpace(CustomProfilePath))
        {
            return Environment.ExpandEnvironmentVariables(CustomProfilePath);
        }

        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var safeName = SanitizeFolderName(Name);
        return System.IO.Path.Combine(userHome, ".gemini-profiles", safeName);
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var cleaned = string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();
        return string.IsNullOrEmpty(cleaned) ? "unnamed_profile" : cleaned;
    }
}
