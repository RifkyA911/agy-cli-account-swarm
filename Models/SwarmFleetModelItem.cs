using System;
using System.Globalization;

namespace AgyAccountSwarm.Models;

public class SwarmFleetModelItem
{
    public string ModelId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string FamilyColor { get; set; } = "#3B82F6";
    public string ReasoningTier { get; set; } = string.Empty;
    public string ContextLimitLabel { get; set; } = string.Empty;
    public int AssignedAccountsCount { get; set; }
    public string AssignedAccountsList { get; set; } = "Standby (0 accounts)";
    public int TotalPoolCapacity { get; set; }
    public string PoolCapacityLabel => TotalPoolCapacity > 0 ? $"{TotalPoolCapacity.ToString("N0", CultureInfo.InvariantCulture)} prompts/day" : "Standby";
    public double AggregateBurnRate { get; set; }
    public string BurnRateLabel => AggregateBurnRate > 0 ? $"{AggregateBurnRate.ToString("F1", CultureInfo.InvariantCulture)} p/hr" : "0.0 p/hr";
    public bool IsActiveInSwarm => AssignedAccountsCount > 0;
    public string StatusBadgeText => IsActiveInSwarm ? "Active in Swarm" : "Ready in CLI";
    public string StatusBadgeColor => IsActiveInSwarm ? "#10B981" : "#475569";
    public string StatusBadgeBg => IsActiveInSwarm ? "#1510B981" : "#15475569";
}

public class SwarmCliCapabilityItem
{
    public string Command { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Verified";
    public string StatusColor { get; set; } = "#10B981";
    public string BadgeText { get; set; } = "CLI v1.2.12";
}
