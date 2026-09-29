using System;

namespace AgyAccountSwarm.Models;

public class ConversationSessionItem
{
    public string Id { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
    public string? Snippet { get; set; }
    public string? Workspace { get; set; }
    public DateTime? LastTimestamp { get; set; }
    public int TurnsCount { get; set; }

    public bool IsNewChat => string.IsNullOrEmpty(Id);
    public bool IsContinueRecent => Id == "__recent__";
    public bool IsCliOnly => Id == "__cli_only__";
    public bool IsSpecificConversation => !IsNewChat && !IsContinueRecent && !IsCliOnly;

    public override string ToString() => DisplayText;
}
