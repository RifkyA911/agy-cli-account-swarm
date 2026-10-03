using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AgyAccountSwarm.Models;

public partial class SwarmProject : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _techStack = "Rust";

    [ObservableProperty]
    private string _rootDirectory = string.Empty;

    [ObservableProperty]
    private bool _useGitWorktrees = true;

    [ObservableProperty]
    private string _defaultBranch = "main";

    [ObservableProperty]
    private List<string> _assignedWorkerIds = new();

    [ObservableProperty]
    private DateTime _createdAt = DateTime.UtcNow;

    [ObservableProperty]
    private DateTime? _lastActiveAt;

    [ObservableProperty]
    private string _defaultObjective = string.Empty;

    public static string SanitizeProjectFolderName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "default-project";
        var safe = name.ToLowerInvariant().Replace(' ', '-');
        var invalid = new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|', '^', '~' };
        var chars = safe.Where(c => !invalid.Contains(c)).ToArray();
        var result = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(result) ? "project" : result;
    }
}

public partial class ProjectWorkerSelectionItem : ObservableObject
{
    [ObservableProperty]
    private string _profileId = string.Empty;

    [ObservableProperty]
    private string _profileName = string.Empty;

    [ObservableProperty]
    private string _accountEmail = string.Empty;

    [ObservableProperty]
    private string _colorTag = "#4285F4";

    [ObservableProperty]
    private string? _avatarUrl;

    [ObservableProperty]
    private string _avatarInitial = "W";

    [ObservableProperty]
    private string _tier = "Pro";

    [ObservableProperty]
    private bool _isSelected;
}
