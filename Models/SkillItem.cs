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

    public string OwnerTitle { get; set; } = "Semua Akun (Global)";
    public string AccessibleBy { get; set; } = "Dapat diakses oleh seluruh akun Google dan worker swarm.";
    public string ScopeCategory { get; set; } = "Global"; // "Global", "Workspace", "Plugin", "Profile"

    public string SourceBadgeColor => SourceType switch
    {
        "Workspace" => "#3B82F6",
        "Built-in" => "#10B981",
        "Plugin" => "#8B5CF6",
        "Profile" => "#EC4899",
        _ => "#64748B"
    };

    public string SourceBadgeBg => SourceType switch
    {
        "Workspace" => "#153B82F6",
        "Built-in" => "#1510B981",
        "Plugin" => "#158B5CF6",
        "Profile" => "#15EC4899",
        _ => "#1564748B"
    };

    public string OwnerBadgeColor => ScopeCategory switch
    {
        "Workspace" => "#38BDF8",
        "Built-in" => "#34D399",
        "Plugin" => "#A78BFA",
        "Profile" => "#F472B6",
        _ => "#94A3B8"
    };

    public string OwnerBadgeBg => ScopeCategory switch
    {
        "Workspace" => "#1538BDF8",
        "Built-in" => "#1534D399",
        "Plugin" => "#15A78BFA",
        "Profile" => "#15F472B6",
        _ => "#1594A3B8"
    };

    public string Icon => SourceType switch
    {
        "Workspace" => "🛠️",
        "Built-in" => "⚡",
        "Plugin" => "🧩",
        "Profile" => "👤",
        _ => "📦"
    };

    public string OwnerIcon => ScopeCategory switch
    {
        "Workspace" => "📁",
        "Built-in" => "🌐",
        "Plugin" => "🔌",
        "Profile" => "👤",
        _ => "🏢"
    };
}
