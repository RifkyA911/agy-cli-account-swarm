using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgyAccountSwarm.Models;

public enum DispatchMode
{
    RoleTailored,
    Broadcast,
    Consensus
}

public enum FleetExecutionMode
{
    VisibleTerminal,
    HeadlessSilent
}

public class FleetDispatchConfig
{
    public string TaskObjective { get; set; } = string.Empty;
    public string TargetWorkspace { get; set; } = string.Empty;
    public DispatchMode Mode { get; set; } = DispatchMode.RoleTailored;
    public FleetExecutionMode ExecutionMode { get; set; } = FleetExecutionMode.VisibleTerminal;
    public bool DangerouslySkipPermissions { get; set; } = true;
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

public partial class DispatchedWorkerTask : ObservableObject
{
    [ObservableProperty]
    private string _profileName = string.Empty;

    [ObservableProperty]
    private string _assignedRole = string.Empty;

    [ObservableProperty]
    private string _tailoredPrompt = string.Empty;

    [ObservableProperty]
    private string _worktreePath = string.Empty;

    [ObservableProperty]
    private string _branchName = string.Empty;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private string _statusColor = "#10B981";

    [ObservableProperty]
    private int? _processId;

    [ObservableProperty]
    private DateTime? _startedAt;

    [ObservableProperty]
    private DateTime? _completedAt;

    [ObservableProperty]
    private string _currentActivity = "Initializing...";

    [ObservableProperty]
    private string _lastOutputLine = string.Empty;

    [ObservableProperty]
    private string _fullOutputLog = string.Empty;

    [ObservableProperty]
    private string _elapsedTimeFormatted = "00:00";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExecutionModeText))]
    private FleetExecutionMode _executionMode = FleetExecutionMode.VisibleTerminal;

    [ObservableProperty]
    private bool _isOutputExpanded;

    [ObservableProperty]
    private string _colorTag = "#4285F4";

    [ObservableProperty]
    private string? _avatarUrl;

    [ObservableProperty]
    private string _avatarInitial = "W";

    public string ExecutionModeText => ExecutionMode == FleetExecutionMode.VisibleTerminal ? "🖥️ Terminal" : "⚡ Silent";

    [RelayCommand]
    public void ToggleOutput()
    {
        IsOutputExpanded = !IsOutputExpanded;
    }
}

public partial class WorktreeTreeNode : ObservableObject
{
    [ObservableProperty]
    private string _branchName = string.Empty;

    [ObservableProperty]
    private string _workerName = string.Empty;

    [ObservableProperty]
    private string _role = string.Empty;

    [ObservableProperty]
    private string _path = string.Empty;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private string _statusColor = "#3B82F6";

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isLast;
}

public class FleetProgressReport
{
    public int Percent { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}
