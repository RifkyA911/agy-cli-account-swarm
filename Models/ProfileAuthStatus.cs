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
    public string StatusMessage { get; set; } = "Not initialized";
    public string? CurrentModel { get; set; } = null;
    public int TotalTurnsCount { get; set; } = 0;
    public double UsagePercentage { get; set; } = 0; // 0 to 100
    public string UsageLabel { get; set; } = "0 turns";
    public DateTime? TokenModifiedAt { get; set; }
    public bool IsExhausted => Status == AuthStatusType.QuotaExhausted || UsagePercentage >= 100.0;
}
