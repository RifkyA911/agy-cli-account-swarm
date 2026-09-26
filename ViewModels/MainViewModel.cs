using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgyAccountSwarm.Models;
using AgyAccountSwarm.Services;

namespace AgyAccountSwarm.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IProfileStorageService _storageService;
    private readonly ITerminalLauncherService _launcherService;
    private readonly IAuthDetectorService _authDetector;

    public ObservableCollection<ProfileItemViewModel> Profiles { get; } = [];
    public ICollectionView FilteredProfiles { get; }

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private TerminalType _selectedTerminal = TerminalType.WindowsTerminal;

    [ObservableProperty]
    private SwarmLaunchMode _selectedSwarmMode = SwarmLaunchMode.SplitPanes;

    [ObservableProperty]
    private string _currentTheme = "Dark";

    [ObservableProperty]
    private bool _closeToTray = true;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private bool _isWindowsTerminalAvailable;

    [ObservableProperty]
    private string? _detectedAgyPath;

    [ObservableProperty]
    private bool _isAgyInstalled;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _notificationMessage;

    // Stat properties
    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _authenticatedCount;

    [ObservableProperty]
    private int _needsLoginCount;

    [ObservableProperty]
    private int _selectedSwarmCount;

    public event Func<AccountProfile?, Task<AccountProfile?>>? ShowEditDialogRequested;
    public event Func<string, string, Task<bool>>? ConfirmDeleteRequested;

    public MainViewModel(
        IProfileStorageService storageService,
        ITerminalLauncherService launcherService,
        IAuthDetectorService authDetector)
    {
        _storageService = storageService;
        _launcherService = launcherService;
        _authDetector = authDetector;

        FilteredProfiles = CollectionViewSource.GetDefaultView(Profiles);
        FilteredProfiles.Filter = FilterProfile;

        Profiles.CollectionChanged += OnProfilesCollectionChanged;
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            // Detect agy & Windows Terminal
            DetectedAgyPath = _launcherService.FindAgyExecutablePath();
            IsAgyInstalled = !string.IsNullOrEmpty(DetectedAgyPath);
            IsWindowsTerminalAvailable = _launcherService.IsWindowsTerminalAvailable();

            // Load settings
            var settings = await _storageService.LoadSettingsAsync();
            SelectedTerminal = settings.PreferredTerminal;
            SelectedSwarmMode = settings.SwarmMode;
            CurrentTheme = settings.Theme;
            CloseToTray = settings.CloseToTray;
            MinimizeToTray = settings.MinimizeToTray;

            // Apply loaded theme
            ThemeManager.ApplyTheme(CurrentTheme);

            // If WT not available, fallback to PowerShell or CMD
            if (SelectedTerminal == TerminalType.WindowsTerminal && !IsWindowsTerminalAvailable)
            {
                SelectedTerminal = TerminalType.PowerShell;
            }

            // Load profiles
            var savedProfiles = await _storageService.LoadProfilesAsync();
            Profiles.Clear();
            foreach (var p in savedProfiles)
            {
                var itemVm = CreateItemViewModel(p);
                Profiles.Add(itemVm);
            }

            UpdateStats();

            // Trigger auth check for all profiles in background
            _ = RefreshAllAuthAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private ProfileItemViewModel CreateItemViewModel(AccountProfile profile)
    {
        var vm = new ProfileItemViewModel(profile, _launcherService, _authDetector);
        vm.OnEditRequested += async item => await EditProfileAsync(item);
        vm.OnDeleteRequested += async item => await DeleteProfileAsync(item);
        vm.OnNotificationRequested += ShowNotification;
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(ProfileItemViewModel.IsSelectedForSwarm) or
                                  nameof(ProfileItemViewModel.AuthStatus))
            {
                UpdateStats();
            }
        };
        return vm;
    }

    private bool FilterProfile(object obj)
    {
        if (obj is not ProfileItemViewModel item) return false;
        if (string.IsNullOrWhiteSpace(SearchQuery)) return true;

        var q = SearchQuery.Trim().ToLowerInvariant();
        return item.Name.ToLowerInvariant().Contains(q) ||
               item.Description.ToLowerInvariant().Contains(q) ||
               item.CurrentModel.ToLowerInvariant().Contains(q) ||
               (item.AuthStatus.AccountEmail?.ToLowerInvariant().Contains(q) ?? false);
    }

    partial void OnSearchQueryChanged(string value)
    {
        FilteredProfiles.Refresh();
    }

    partial void OnSelectedTerminalChanged(TerminalType value)
    {
        _ = SaveSettingsAsync();
    }

    partial void OnSelectedSwarmModeChanged(SwarmLaunchMode value)
    {
        _ = SaveSettingsAsync();
    }

    partial void OnCloseToTrayChanged(bool value)
    {
        _ = SaveSettingsAsync();
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        _ = SaveSettingsAsync();
    }

    [RelayCommand]
    public void ToggleTheme()
    {
        CurrentTheme = CurrentTheme == "Dark" ? "Light" : "Dark";
        ThemeManager.ApplyTheme(CurrentTheme);
        _ = SaveSettingsAsync();
        ShowNotification($"Switched to {CurrentTheme} theme");
    }

    [RelayCommand]
    public async Task RefreshAllAuthAsync()
    {
        IsLoading = true;
        try
        {
            var tasks = Profiles.Select(p => p.RefreshAuthStatusAsync());
            await Task.WhenAll(tasks);
            UpdateStats();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task AddProfileAsync()
    {
        if (ShowEditDialogRequested == null) return;

        var result = await ShowEditDialogRequested(null);
        if (result != null)
        {
            var itemVm = CreateItemViewModel(result);
            Profiles.Add(itemVm);
            await SaveProfilesAsync();
            await itemVm.RefreshAuthStatusAsync();
            ShowNotification($"Added new profile '{result.Name}'");
        }
    }

    public async Task EditProfileAsync(ProfileItemViewModel item)
    {
        if (ShowEditDialogRequested == null) return;

        var result = await ShowEditDialogRequested(item.Profile);
        if (result != null)
        {
            item.Name = result.Name;
            item.Description = result.Description;
            item.ColorTag = result.ColorTag;
            item.CustomProfilePath = result.CustomProfilePath;
            item.DefaultWorkspace = result.DefaultWorkspace;
            item.ExtraArguments = result.ExtraArguments;
            item.IsSelectedForSwarm = result.IsSelectedForSwarm;
            item.SyncBackToModel();

            await SaveProfilesAsync();
            await item.RefreshAuthStatusAsync();
            FilteredProfiles.Refresh();
            ShowNotification($"Updated profile '{item.Name}'");
        }
    }

    public async Task DeleteProfileAsync(ProfileItemViewModel item)
    {
        if (ConfirmDeleteRequested != null)
        {
            var confirmed = await ConfirmDeleteRequested(
                "Delete Profile",
                $"Are you sure you want to remove the profile '{item.Name}' from manager?\n(Physical files in the profile directory won't be deleted)");
            if (!confirmed) return;
        }

        Profiles.Remove(item);
        await SaveProfilesAsync();
        UpdateStats();
        ShowNotification($"Removed profile '{item.Name}'");
    }

    [RelayCommand]
    public async Task LaunchSwarmAsync()
    {
        var targets = Profiles.Where(p => p.IsSelectedForSwarm).ToList();
        if (targets.Count == 0)
        {
            ShowNotification("No profiles selected for Swarm Launch.");
            return;
        }

        try
        {
            var procs = await _launcherService.LaunchSwarmAsync(
                targets.Select(t => t.Profile),
                SelectedTerminal,
                SelectedSwarmMode);

            ShowNotification($"Swarm launched: {targets.Count} account sessions started ({SelectedSwarmMode})!");
        }
        catch (Exception ex)
        {
            ShowNotification($"Failed to launch swarm: {ex.Message}");
        }
    }

    [RelayCommand]
    public void ToggleSelectAllSwarm()
    {
        var allSelected = Profiles.All(p => p.IsSelectedForSwarm);
        foreach (var p in Profiles)
        {
            p.IsSelectedForSwarm = !allSelected;
        }
        UpdateStats();
    }

    [RelayCommand]
    public void OpenAppDataFolder()
    {
        var path = _storageService.GetAppDataPath();
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{path}\"",
            UseShellExecute = true
        });
    }

    public void ShowNotification(string message)
    {
        NotificationMessage = message;
        _ = Task.Run(async () =>
        {
            await Task.Delay(3500);
            if (NotificationMessage == message)
            {
                NotificationMessage = null;
            }
        });
    }

    private void OnProfilesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateStats();
    }

    private void UpdateStats()
    {
        TotalCount = Profiles.Count;
        AuthenticatedCount = Profiles.Count(p => p.AuthStatus.Status == AuthStatusType.Authenticated);
        NeedsLoginCount = Profiles.Count(p => p.AuthStatus.Status is AuthStatusType.NeedsLogin or AuthStatusType.NotInitialized);
        SelectedSwarmCount = Profiles.Count(p => p.IsSelectedForSwarm);
    }

    private async Task SaveProfilesAsync()
    {
        foreach (var p in Profiles) p.SyncBackToModel();
        await _storageService.SaveProfilesAsync(Profiles.Select(p => p.Profile));
        UpdateStats();
    }

    private async Task SaveSettingsAsync()
    {
        var settings = new AppSettings
        {
            PreferredTerminal = SelectedTerminal,
            SwarmMode = SelectedSwarmMode,
            Theme = CurrentTheme,
            CloseToTray = CloseToTray,
            MinimizeToTray = MinimizeToTray
        };
        await _storageService.SaveSettingsAsync(settings);
    }
}
