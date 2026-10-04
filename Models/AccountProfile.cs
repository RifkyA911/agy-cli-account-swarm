using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace AgyAccountSwarm.Models;

public class AccountProfile : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void NotifyAllPropertiesChanged()
    {
        OnPropertyChanged(string.Empty);
    }

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
    /// Whether to automatically pass --dangerously-skip-permissions to bypass all interactive CLI confirmation prompts.
    /// </summary>
    public bool DangerouslySkipPermissions { get; set; } = false;

    private bool _isSelectedForSwarm = true;

    /// <summary>
    /// Whether this profile is selected for Swarm Launch (batch execution).
    /// </summary>
    public bool IsSelectedForSwarm
    {
        get => _isSelectedForSwarm;
        set
        {
            if (_isSelectedForSwarm != value)
            {
                _isSelectedForSwarm = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Subscription tier: Basic, Plus, Pro, Ultra, or Unverified (default before login)
    /// </summary>
    public string Tier { get; set; } = "Unverified";

    /// <summary>
    /// Swarm execution role / archetype (e.g. Lead Architect, Backend Specialist, Security Auditor, QA Automation, Generalist Worker).
    /// </summary>
    public string AccountRole { get; set; } = "Generalist Worker";

    /// <summary>
    /// Model Provider Family: "Gemini" or "Claude / GPT".
    /// </summary>
    public string ModelFamily { get; set; } = "Gemini";

    /// <summary>
    /// Model Reasoning / Temperature preset: "Low (0.2)", "Medium (0.7)", "High (1.0)".
    /// </summary>
    public string ModelTemperature { get; set; } = "Medium (0.7)";

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

    [JsonIgnore]
    public bool IsDefaultPrimary => IsMainDefaultProfile();

    [JsonIgnore]
    public string AvatarInitial
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Name)) return Name.Substring(0, 1).ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(AuthStatus?.AccountEmail)) return AuthStatus.AccountEmail.Substring(0, 1).ToUpperInvariant();
            return "G";
        }
    }

    [JsonIgnore]
    public string? AvatarUrl
    {
        get
        {
            if (!string.IsNullOrEmpty(AuthStatus?.LocalAvatarPath) && System.IO.File.Exists(AuthStatus.LocalAvatarPath))
                return AuthStatus.LocalAvatarPath;
            if (!string.IsNullOrEmpty(AuthStatus?.AvatarUrl))
            {
                if (System.IO.File.Exists(AuthStatus.AvatarUrl)) return AuthStatus.AvatarUrl;
                if (AuthStatus.AvatarUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return AuthStatus.AvatarUrl;
            }
            return null;
        }
    }

    [JsonIgnore]
    public bool HasAvatarUrl => !string.IsNullOrEmpty(AvatarUrl);

    [JsonIgnore]
    public string? AccountEmail => AuthStatus?.AccountEmail;

    [JsonIgnore]
    public string TierBadgeText => !string.IsNullOrWhiteSpace(Tier) ? Tier : "Basic";

    [JsonIgnore]
    public string TierBadgeBackground => TierBadgeText.ToLowerInvariant() switch
    {
        "ultra" => "#D97706",
        "pro" => "#7C3AED",
        "plus" => "#0284C7",
        _ => "#64748B"
    };

    [JsonIgnore]
    public string StatusBadgeColor => AuthStatus?.Status switch
    {
        AuthStatusType.Authenticated => "#10B981",
        AuthStatusType.QuotaExhausted => "#EF4444",
        AuthStatusType.NeedsLogin => "#F59E0B",
        AuthStatusType.Error => "#EF4444",
        _ => "#6B7280"
    };

    [JsonIgnore]
    public string StatusBadgeText => AuthStatus?.Status switch
    {
        AuthStatusType.Authenticated => string.IsNullOrEmpty(AuthStatus.AccountEmail) ? "Authenticated" : AuthStatus.AccountEmail,
        AuthStatusType.QuotaExhausted => "Quota Exhausted",
        AuthStatusType.NeedsLogin => "Needs Login",
        AuthStatusType.Error => "Auth Error",
        _ => "Ready"
    };

    [JsonIgnore]
    public string TodayQuotaFormatted => $"{AuthStatus?.TodayTurnsCount ?? 0:N0} prompts today";

    [JsonIgnore]
    public string WeeklyRemainingFormatted => AuthStatus != null && AuthStatus.GeminiWeeklyRemainingPercent > 0
        ? $"{AuthStatus.GeminiWeeklyRemainingPercent:F0}% quota remaining"
        : "Authentic Quota Ready";

    public bool IsMainDefaultProfile()
    {
        // Duplicates or copies must NEVER be treated as the main primary profile
        if (Name.Contains("(Copy", StringComparison.OrdinalIgnoreCase) ||
            Name.EndsWith("(Copy)", StringComparison.OrdinalIgnoreCase) ||
            Name.StartsWith("Copy of", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(Id, "main", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Name, "Default (Main Account)", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Name, "Primary (Default)", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\', '/');
        if (string.IsNullOrWhiteSpace(CustomProfilePath)) return false;
        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(CustomProfilePath.Trim()).TrimEnd('\\', '/');
            return expanded.Equals(userHome, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public string GetEffectiveProfileDirectory()
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (IsMainDefaultProfile())
        {
            return userHome;
        }

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

                // Prevent pointing to root drive or system directories
                bool isDangerous = string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase);
                if (!string.IsNullOrEmpty(winDir) && fullPath.StartsWith(winDir, StringComparison.OrdinalIgnoreCase)) isDangerous = true;
                if (!string.IsNullOrEmpty(sysDir) && fullPath.StartsWith(sysDir, StringComparison.OrdinalIgnoreCase)) isDangerous = true;

                if (!OperatingSystem.IsWindows())
                {
                    if (fullPath == "/" || fullPath.StartsWith("/etc") || fullPath.StartsWith("/bin") || fullPath.StartsWith("/sbin") || fullPath.StartsWith("/usr"))
                    {
                        isDangerous = true;
                    }
                }

                if (!isDangerous)
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
