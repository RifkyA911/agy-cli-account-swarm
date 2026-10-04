using System;

namespace AgyAccountSwarm.Models;

public enum TerminalType
{
    WindowsTerminal,
    PowerShellCore,
    PowerShell,
    CommandPrompt,
    GitBash
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
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? LocalAvatarPath { get; set; }
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

    // Weekly Quota Metrics
    public int WeeklyTurnsCount { get; set; } = 0;
    public int WeeklyQuotaLimit { get; set; } = 5000;
    public double WeeklyUsagePercentage { get; set; } = 0; // 0 to 100
    public double WeeklyRemainingPercentage { get; set; } = 100.0;
    public string WeeklyRemainingLabel { get; set; } = "100% remaining";

    // Per-Session Quota Metrics (from agy cli session /usage)
    public int SessionTurnsCount { get; set; } = 0;
    public string? CurrentSessionId { get; set; }
    public string SessionUsageLabel { get; set; } = "0 turns this session";

    // Context Metrics (from agy cli /context)
    public long ModelContextLimit { get; set; } = 1048576;
    public long EstimatedContextTokens { get; set; } = 0;
    public double ContextUsagePercentage { get; set; } = 0.0;
    public string ContextWindowLabel { get; set; } = "1M Window (1,048,576 tokens)";
    public string ContextUsageSummary { get; set; } = "0 / 1,048K tokens (0.0%)";
    public string ContextHeadroomSummary { get; set; } = "1,048K tokens free (100.0%)";

    // Antigravity Model Group Quotas (from agy cli /usage breakdown)
    // 1. GEMINI MODELS (Gemini Flash, Gemini Pro)
    public double GeminiWeeklyRemainingPercent { get; set; } = 100.0;
    public string GeminiWeeklyRefreshesIn { get; set; } = "106h 56m";
    public double Gemini5HourRemainingPercent { get; set; } = 100.0;
    public string Gemini5HourRefreshesIn { get; set; } = "4h 23m";

    // 2. CLAUDE AND GPT MODELS (Claude Opus, Claude Sonnet, GPT-OSS)
    public double ClaudeGptWeeklyRemainingPercent { get; set; } = 100.0;
    public string ClaudeGptWeeklyRefreshesIn { get; set; } = "106h 2m";
    public double ClaudeGpt5HourRemainingPercent { get; set; } = 100.0;
    public string ClaudeGpt5HourRefreshesIn { get; set; } = "1h 1m";

    // Authentic CLI Inspection Previews
    public string InspectionUsageText { get; set; } = string.Empty;
    public string InspectionContextText { get; set; } = string.Empty;

    public string DetectedTier { get; set; } = "Pro";
    public DateTime? TokenModifiedAt { get; set; }
    public bool IsExhausted => Status == AuthStatusType.QuotaExhausted || UsagePercentage >= 100.0;
}
