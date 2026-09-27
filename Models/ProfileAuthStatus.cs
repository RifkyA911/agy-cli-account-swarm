using System;

namespace AgyAccountSwarm.Models;

public enum TerminalType
{
    WindowsTerminal,
    PowerShell,
    CommandPrompt
}

public enum SwarmLaunchMode
{
    SplitPanes,
    SeparateTabs,
    SeparateWindows
}

public enum AuthStatusType
{
    NotInitialized,
    NeedsLogin,
    Authenticated,
    Error,
    QuotaExhausted
}

public class ProfileAuthStatus
{
    public AuthStatusType Status { get; set; } = AuthStatusType.NotInitialized;
    public string? AccountEmail { get; set; }
    public string? AvatarUrl { get; set; }
    public string StatusMessage { get; set; } = "Not initialized";
    public string? CurrentModel { get; set; } = null;
    public int TotalTurnsCount { get; set; } = 0;
    public int TodayTurnsCount { get; set; } = 0;
    public int DailyQuotaLimit { get; set; } = 1000;
    public long TodayTokensEstimated { get; set; } = 0;
    public long DailyTokensLimit { get; set; } = 5000000;
    public double UsagePercentage { get; set; } = 0; // 0 to 100
    public string UsageLabel { get; set; } = "0 prompts today";
    public string QuotaResetCountdown { get; set; } = "Resets at 00:00 UTC";
    public string DetectedTier { get; set; } = "Pro";
    public DateTime? TokenModifiedAt { get; set; }
    public bool IsExhausted => Status == AuthStatusType.QuotaExhausted || UsagePercentage >= 100.0;
}
