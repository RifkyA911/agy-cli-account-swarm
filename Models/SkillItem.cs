using System;
using System.Collections.Generic;

namespace AgyAccountSwarm.Models;

public class SkillItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SourceType { get; set; } = "Workspace"; // "Workspace", "Built-in", "Plugin"
    public string DirectoryPath { get; set; } = string.Empty;
    public string SkillFilePath { get; set; } = string.Empty;
    public bool HasInstructions { get; set; } = false;
    public string InstructionsPreview { get; set; } = string.Empty;
    public string InstructionsContent { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();

    public string SourceBadgeColor => SourceType switch
    {
        "Workspace" => "#3B82F6",
        "Built-in" => "#10B981",
        "Plugin" => "#8B5CF6",
        _ => "#64748B"
    };

    public string SourceBadgeBg => SourceType switch
    {
        "Workspace" => "#153B82F6",
        "Built-in" => "#1510B981",
        "Plugin" => "#158B5CF6",
        _ => "#1564748B"
    };

    public string Icon => SourceType switch
    {
        "Workspace" => "🛠️",
        "Built-in" => "⚡",
        "Plugin" => "🧩",
        _ => "📦"
    };
}
