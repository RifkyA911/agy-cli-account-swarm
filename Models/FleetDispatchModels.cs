using System;
using System.Collections.Generic;

namespace AgyAccountSwarm.Models;

public enum DispatchMode
{
    RoleTailored,
    Broadcast,
    Consensus
}

public class FleetDispatchConfig
{
    public string TaskObjective { get; set; } = string.Empty;
    public string TargetWorkspace { get; set; } = string.Empty;
    public DispatchMode Mode { get; set; } = DispatchMode.RoleTailored;
    public bool UseGitWorktrees { get; set; } = true;
    public List<string> SelectedWorkerNames { get; set; } = new();
}

public class GitWorktreeInfo
{
    public string Path { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string CommitHash { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public string? LockReason { get; set; }
}

public class ResourceCheckResult
{
    public bool IsSafe { get; set; } = true;
    public double AvailableDiskGb { get; set; }
    public double AvailableMemoryGb { get; set; }
    public bool IsGitRepository { get; set; }
    public string? CurrentBranch { get; set; }
    public string? WarningMessage { get; set; }
    public string? ErrorMessage { get; set; }
}

public class DispatchedWorkerTask
{
    public string ProfileName { get; set; } = string.Empty;
    public string AssignedRole { get; set; } = string.Empty;
    public string TailoredPrompt { get; set; } = string.Empty;
    public string WorktreePath { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string Status { get; set; } = "Ready";
    public int? ProcessId { get; set; }
    public DateTime? StartedAt { get; set; }
}

public class WorktreeTreeNode
{
    public string BranchName { get; set; } = string.Empty;
    public string WorkerName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = "Ready";
    public string StatusColor { get; set; } = "#3B82F6";
    public bool IsActive { get; set; }
    public bool IsLast { get; set; }
}

