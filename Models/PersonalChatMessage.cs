using System;
using System.Collections.Generic;

namespace AgyAccountSwarm.Models;

/// <summary>
/// Individual message item in a personal 1-on-1 chat session with agy CLI.
/// </summary>
public class PersonalChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Role { get; set; } = "user"; // "user", "assistant", "system"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Model { get; set; } = "gemini-2.5-pro";
    public string ReasoningEffort { get; set; } = "medium";
    public bool IsStreaming { get; set; }
    public List<RagChunkItem> InjectedRagChunks { get; set; } = new();
    public bool HasRagChunks => InjectedRagChunks != null && InjectedRagChunks.Count > 0;
    public double ExecutionDurationSeconds { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int ThinkingTokens { get; set; }
    public int TotalTokens { get; set; }

    public bool IsUser => string.Equals(Role, "user", StringComparison.OrdinalIgnoreCase);
    public bool IsAssistant => string.Equals(Role, "assistant", StringComparison.OrdinalIgnoreCase);
    public bool IsSystem => string.Equals(Role, "system", StringComparison.OrdinalIgnoreCase);

    public string FormattedTime => Timestamp.ToLocalTime().ToString("HH:mm:ss");

    public string TokenTelemetryDisplay
    {
        get
        {
            if (TotalTokens <= 0 && ExecutionDurationSeconds <= 0) return string.Empty;
            var parts = new List<string>();
            if (TotalTokens > 0) parts.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:N0} tokens", TotalTokens));
            if (ExecutionDurationSeconds > 0) parts.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F1}s", ExecutionDurationSeconds));
            if (!string.IsNullOrEmpty(Model)) parts.Add(Model);
            return string.Join(" • ", parts);
        }
    }
}

/// <summary>
/// Conversation session metadata stored in history for personal chat.
/// </summary>
public class PersonalChatSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString(); // conversation_id
    public string ProfileId { get; set; } = "main";
    public string Title { get; set; } = "New Conversation";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int TurnCount { get; set; }
    public string LastQuerySnippet { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.5-pro";

    public string RelativeTimeDisplay
    {
        get
        {
            var diff = DateTime.UtcNow - UpdatedAt;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            return UpdatedAt.ToLocalTime().ToString("MMM dd");
        }
    }
}
