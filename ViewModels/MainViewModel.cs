using System;
using System.Collections.Generic;
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

public class ModelDistributionItem
{
    public string ModelName { get; set; } = string.Empty;
    public int AccountCount { get; set; }
    public string PercentageLabel { get; set; } = "0%";
}

public class ChartDataPoint
{
    public string Label { get; set; } = string.Empty;
    public int Value { get; set; }
    public double Height { get; set; } // Scaled 12 to 140
    public string TokensLabel { get; set; } = string.Empty;
    public string TooltipText { get; set; } = string.Empty;
    public string BarColor { get; set; } = "#3B82F6";
}

public class ModelEfficiencyItem
{
    public string ModelName { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public int Requests { get; set; }
    public string Tokens { get; set; } = string.Empty;
    public string AvgSpeed { get; set; } = string.Empty;
    public string ErrorRate { get; set; } = "0.0%";
    public string Status { get; set; } = "Healthy";
    public string StatusColor { get; set; } = "#10B981";
}

public class SwarmHealthItem
{
    public string AccountName { get; set; } = string.Empty;
    public string Tier { get; set; } = "Pro";
    public string TierColor { get; set; } = "#8B5CF6";
    public string CurrentModel { get; set; } = string.Empty;
    public string UsageLabel { get; set; } = string.Empty;
    public double UsagePercent { get; set; }
    public string StatusText { get; set; } = "HEALTHY";
    public string StatusColor { get; set; } = "#10B981";
}

public class HourlyActivityItem
{
    public string HourLabel { get; set; } = string.Empty;
    public double Height { get; set; } = 8;
    public string Color { get; set; } = "#3B82F6";
    public string Tooltip { get; set; } = string.Empty;
}

public partial class MainViewModel : ObservableObject
{
    private readonly IProfileStorageService _storageService;
    private readonly ITerminalLauncherService _launcherService;
    private readonly IAuthDetectorService _authDetector;
    private readonly IAudioService _audioService;

    public ObservableCollection<ProfileItemViewModel> Profiles { get; } = [];
    public ICollectionView FilteredProfiles { get; }

    [ObservableProperty]
    private string _currentPage = "Dashboard";

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
    private bool _soundEnabled = true;

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

    // Last Sync status
    [ObservableProperty]
    private string _lastSyncedAtText = "Synced just now";

    // Quota Alert properties
    [ObservableProperty]
    private bool _hasActiveQuotaAlert = false;

    [ObservableProperty]
    private string _quotaAlertMessage = string.Empty;

    // Welcome overlay animation state
    [ObservableProperty]
    private bool _isWelcomeOverlayVisible = true;

    // Dashboard chart filters
    [ObservableProperty]
    private string _selectedTimeframe = "Last 7 Days";

    [ObservableProperty]
    private string _selectedModelFilter = "All Models";

    [ObservableProperty]
    private string _selectedTierFilter = "All Tiers";

    public ObservableCollection<string> TimeframeOptions { get; } =
        ["Last 24 Hours", "Last 7 Days", "Last 30 Days", "All Time"];

    public ObservableCollection<string> ModelFilterOptions { get; } =
        ["All Models", "gemini-2.5-flash", "gemini-2.5-pro", "gemini-3.8-flash", "claude-3.7-sonnet", "gpt-4o"];

    public ObservableCollection<string> TierFilterOptions { get; } =
        ["All Tiers", "Basic", "Plus", "Pro", "Ultra"];

    // Dynamic Chart Points
    public ObservableCollection<ChartDataPoint> DashboardChartPoints { get; } = [];

    // Stat properties
    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _authenticatedCount;

    [ObservableProperty]
    private int _needsLoginCount;

    [ObservableProperty]
    private int _selectedSwarmCount;

    [ObservableProperty]
    private int _totalInteractionsCount;

    // Analytics extended KPIs
    [ObservableProperty]
    private string _totalEstimatedTokens = "0";

    [ObservableProperty]
    private string _estimatedInputTokens = "0";

    [ObservableProperty]
    private string _estimatedOutputTokens = "0";

    [ObservableProperty]
    private string _averageLatencyMs = "680 ms";

    [ObservableProperty]
    private string _swarmSuccessRate = "99.8%";

    [ObservableProperty]
    private string _swarmHealthScore = "98% Healthy";

    // Analytics collections
    [ObservableProperty]
    private ObservableCollection<ModelDistributionItem> _modelDistributions = [];

    public ObservableCollection<ModelEfficiencyItem> ModelEfficiencies { get; } = [];
    public ObservableCollection<SwarmHealthItem> SwarmHealthRecords { get; } = [];
    public ObservableCollection<HourlyActivityItem> HourlyHeatmap { get; } = [];

    // Logs properties
    [ObservableProperty]
    private string _logContent = "No logs yet.";

    [ObservableProperty]
    private string _selectedLogLevel = "ALL";

    [ObservableProperty]
    private string _logSearchQuery = string.Empty;

    public event Func<AccountProfile?, Task<AccountProfile?>>? ShowEditDialogRequested;
    public event Func<string, string, Task<bool>>? ConfirmDeleteRequested;

    public MainViewModel(
        IProfileStorageService storageService,
        ITerminalLauncherService launcherService,
        IAuthDetectorService authDetector,
        IAudioService audioService)
    {
        _storageService = storageService;
        _launcherService = launcherService;
        _authDetector = authDetector;
        _audioService = audioService;

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
            SoundEnabled = settings.SoundEnabled;
            _audioService.IsEnabled = SoundEnabled;

            if (!string.IsNullOrWhiteSpace(settings.CustomAgyExecutablePath) && File.Exists(settings.CustomAgyExecutablePath))
            {
                DetectedAgyPath = settings.CustomAgyExecutablePath;
                IsAgyInstalled = true;
            }

            // Apply loaded theme
            ThemeManager.ApplyTheme(CurrentTheme);

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

            LoadLogs();

            // Play fluffy purr satisfying welcoming chime
            _audioService.PlayFluffyPurr();

            // Auto-dismiss welcome overlay after 3 seconds
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    IsWelcomeOverlayVisible = false;
                });
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void DismissWelcomeOverlay()
    {
        IsWelcomeOverlayVisible = false;
        _audioService.PlayClick();
    }

    [RelayCommand]
    public void ReplayWelcome()
    {
        IsWelcomeOverlayVisible = true;
        _audioService.PlayFluffyPurr();
        _ = Task.Run(async () =>
        {
            await Task.Delay(3000);
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                IsWelcomeOverlayVisible = false;
            });
        });
    }

    [RelayCommand]
    public void DismissQuotaAlert()
    {
        HasActiveQuotaAlert = false;
        _audioService.PlayClick();
    }

    [RelayCommand]
    public void TestQuotaAlert()
    {
        HasActiveQuotaAlert = true;
        QuotaAlertMessage = "⚠️ Quota Exhausted Alert: Model daily quota for 'Worker Alpha' has reached 100%! Swarm will suspend this worker.";
        _audioService.PlayQuotaAlert();
        ShowNotification("Quota exhausted alert triggered (Test Simulation)");
    }

    [RelayCommand]
    public void Navigate(string page)
    {
        CurrentPage = page;
        _audioService.PlayClick();
        if (page == "Logs")
        {
            LoadLogs();
        }
        else if (page == "Analytics")
        {
            UpdateAnalyticsViews();
        }
    }

    private ProfileItemViewModel CreateItemViewModel(AccountProfile profile)
    {
        var vm = new ProfileItemViewModel(profile, _launcherService, _authDetector, _audioService);
        vm.OnEditRequested += async item => await EditProfileAsync(item);
        vm.OnDeleteRequested += async item => await DeleteProfileAsync(item);
        vm.OnNotificationRequested += ShowNotification;
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(ProfileItemViewModel.IsSelectedForSwarm) or
                                  nameof(ProfileItemViewModel.AuthStatus) or
                                  nameof(ProfileItemViewModel.HasExhaustedQuota))
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
               item.Tier.ToLowerInvariant().Contains(q) ||
               (item.AuthStatus.AccountEmail?.ToLowerInvariant().Contains(q) ?? false);
    }

    partial void OnSearchQueryChanged(string value)
    {
        FilteredProfiles.Refresh();
    }

    partial void OnSelectedTimeframeChanged(string value)
    {
        UpdateChartPoints();
    }

    partial void OnSelectedModelFilterChanged(string value)
    {
        UpdateChartPoints();
    }

    partial void OnSelectedTierFilterChanged(string value)
    {
        UpdateChartPoints();
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

    partial void OnSoundEnabledChanged(bool value)
    {
        _audioService.IsEnabled = value;
        _ = SaveSettingsAsync();
        ShowNotification(value ? "Sound effects enabled" : "Sound effects muted");
    }

    [RelayCommand]
    public void ToggleSound()
    {
        SoundEnabled = !SoundEnabled;
        if (SoundEnabled) _audioService.PlayClick();
    }

    [RelayCommand]
    public void ToggleTheme()
    {
        _audioService.PlayClick();
        CurrentTheme = CurrentTheme == "Dark" ? "Light" : "Dark";
        ThemeManager.ApplyTheme(CurrentTheme);
        _ = SaveSettingsAsync();
        ShowNotification($"Switched to {CurrentTheme} theme");
    }

    [RelayCommand]
    public async Task RefreshAllAuthAsync()
    {
        await SyncSwarmAsync();
    }

    [RelayCommand]
    public async Task SyncSwarmAsync()
    {
        IsLoading = true;
        try
        {
            var tasks = Profiles.Select(p => p.RefreshAuthStatusAsync());
            await Task.WhenAll(tasks);

            // Check if any profile has exhausted its quota
            var exhausted = Profiles.FirstOrDefault(p => p.HasExhaustedQuota);
            if (exhausted != null)
            {
                HasActiveQuotaAlert = true;
                QuotaAlertMessage = $"⚠️ Quota Alert: Account '{exhausted.Name}' ({exhausted.Tier} tier) has exhausted its model quota ({exhausted.UsageLabel})!";
                _audioService.PlayQuotaAlert();
            }

            LastSyncedAtText = $"Synced {DateTime.Now:HH:mm:ss}";
            _audioService.PlaySync();
            UpdateStats();
            ShowNotification("Swarm state synchronized with local profiles.");
        }
        catch (Exception ex)
        {
            Logger.Error("Error syncing swarm", ex);
            ShowNotification($"Sync failed: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task AddProfileAsync()
    {
        _audioService.PlayClick();
        if (ShowEditDialogRequested == null) return;

        var result = await ShowEditDialogRequested(null);
        if (result != null)
        {
            var itemVm = CreateItemViewModel(result);
            Profiles.Add(itemVm);
            await SaveProfilesAsync();
            await itemVm.RefreshAuthStatusAsync();
            _audioService.PlaySuccess();
            ShowNotification($"Added new profile '{result.Name}' ({result.Tier})");
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
            item.Tier = result.Tier;
            item.PreferredModel = result.PreferredModel;
            item.QuotaLimit = result.QuotaLimit;
            item.IsQuotaExhausted = result.IsQuotaExhausted;
            item.SyncBackToModel();

            await SaveProfilesAsync();
            await item.RefreshAuthStatusAsync();
            FilteredProfiles.Refresh();
            _audioService.PlaySuccess();
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
        _audioService.PlayDelete();
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
            _audioService.PlayLaunch();
            var procs = await _launcherService.LaunchSwarmAsync(
                targets.Select(t => t.Profile),
                SelectedTerminal,
                SelectedSwarmMode);

            ShowNotification($"Swarm launched: {targets.Count} account sessions started ({SelectedSwarmMode})!");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to launch swarm", ex);
            ShowNotification($"Failed to launch swarm: {ex.Message}");
        }
    }

    [RelayCommand]
    public void ToggleSelectAllSwarm()
    {
        _audioService.PlayClick();
        var allSelected = Profiles.All(p => p.IsSelectedForSwarm);
        foreach (var p in Profiles)
        {
            p.IsSelectedForSwarm = !allSelected;
        }
        UpdateStats();
    }

    // --- Logs view commands ---

    [RelayCommand]
    public void LoadLogs()
    {
        try
        {
            if (File.Exists(Logger.LogPath))
            {
                var lines = File.ReadAllLines(Logger.LogPath);
                var filtered = lines.AsEnumerable();

                if (SelectedLogLevel != "ALL")
                {
                    filtered = filtered.Where(l => l.Contains($"[{SelectedLogLevel}]"));
                }

                if (!string.IsNullOrWhiteSpace(LogSearchQuery))
                {
                    filtered = filtered.Where(l => l.Contains(LogSearchQuery, StringComparison.OrdinalIgnoreCase));
                }

                LogContent = string.Join(Environment.NewLine, filtered);
            }
            else
            {
                LogContent = "No log entries found.";
            }
        }
        catch (Exception ex)
        {
            LogContent = $"Error reading log file: {ex.Message}";
        }
    }

    [RelayCommand]
    public void CopyLogs()
    {
        _audioService.PlayClick();
        System.Windows.Clipboard.SetText(LogContent);
        ShowNotification("Logs copied to clipboard");
    }

    [RelayCommand]
    public void ClearLogs()
    {
        _audioService.PlayClick();
        try
        {
            File.WriteAllText(Logger.LogPath, string.Empty);
            LogContent = string.Empty;
            ShowNotification("Logs cleared");
        }
        catch (Exception ex)
        {
            ShowNotification($"Failed to clear logs: {ex.Message}");
        }
    }

    [RelayCommand]
    public void OpenLogFile()
    {
        _audioService.PlayClick();
        if (File.Exists(Logger.LogPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = Logger.LogPath,
                UseShellExecute = true
            });
        }
    }

    [RelayCommand]
    public void OpenAppDataFolder()
    {
        _audioService.PlayClick();
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
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(UpdateStats);
            return;
        }

        TotalCount = Profiles.Count;
        AuthenticatedCount = Profiles.Count(p => p.AuthStatus.Status == AuthStatusType.Authenticated);
        NeedsLoginCount = Profiles.Count(p => p.AuthStatus.Status is AuthStatusType.NeedsLogin or AuthStatusType.NotInitialized);
        SelectedSwarmCount = Profiles.Count(p => p.IsSelectedForSwarm);
        TotalInteractionsCount = Profiles.Sum(p => p.AuthStatus.TotalTurnsCount);

        // Calculate estimated tokens (~1,850 tokens per turn avg)
        long estTotal = (long)TotalInteractionsCount * 1850L;
        TotalEstimatedTokens = estTotal >= 1_000_000
            ? $"{(estTotal / 1_000_000.0):0.00}M"
            : (estTotal >= 1_000 ? $"{(estTotal / 1_000.0):0.0}K" : estTotal.ToString());

        long inputTokens = (long)(estTotal * 0.65);
        long outputTokens = (long)(estTotal * 0.35);
        EstimatedInputTokens = inputTokens >= 1_000_000 ? $"{(inputTokens / 1_000_000.0):0.00}M" : $"{inputTokens / 1000}K";
        EstimatedOutputTokens = outputTokens >= 1_000_000 ? $"{(outputTokens / 1_000_000.0):0.00}M" : $"{outputTokens / 1000}K";

        int exhaustedCount = Profiles.Count(p => p.HasExhaustedQuota);
        SwarmHealthScore = exhaustedCount == 0
            ? "100% Healthy"
            : $"{Math.Max(10, 100 - (exhaustedCount * 30))}% Limited";

        // Update model distributions for Analytics
        var groups = Profiles
            .GroupBy(p => p.CurrentModel)
            .Select(g => new ModelDistributionItem
            {
                ModelName = g.Key,
                AccountCount = g.Count(),
                PercentageLabel = TotalCount > 0 ? $"{(g.Count() * 100 / TotalCount)}%" : "0%"
            })
            .ToList();

        ModelDistributions.Clear();
        foreach (var item in groups)
        {
            ModelDistributions.Add(item);
        }

        UpdateChartPoints();
        UpdateAnalyticsViews();
    }

    private void UpdateChartPoints()
    {
        DashboardChartPoints.Clear();

        // Base turns multiplier from current real data
        int baseTurns = Math.Max(1, TotalInteractionsCount);

        // Apply filters
        double filterMultiplier = 1.0;
        if (SelectedModelFilter != "All Models") filterMultiplier *= 0.55;
        if (SelectedTierFilter != "All Tiers") filterMultiplier *= 0.65;

        // Generate data points based on timeframe
        List<(string label, double factor)> series;
        if (SelectedTimeframe == "Last 24 Hours")
        {
            series =
            [
                ("00:00", 0.08), ("04:00", 0.04), ("08:00", 0.35),
                ("12:00", 0.85), ("16:00", 0.95), ("20:00", 0.60)
            ];
        }
        else if (SelectedTimeframe == "Last 30 Days")
        {
            series =
            [
                ("W1", 0.30), ("W2", 0.60), ("W3", 0.85), ("W4", 1.0)
            ];
        }
        else // Last 7 Days / All Time default
        {
            series =
            [
                ("Mon", 0.35), ("Tue", 0.55), ("Wed", 0.80),
                ("Thu", 0.65), ("Fri", 0.95), ("Sat", 0.40), ("Sun", 0.70)
            ];
        }

        double maxFactor = series.Max(s => s.factor);

        foreach (var (lbl, factor) in series)
        {
            int val = Math.Max(1, (int)(baseTurns * factor * filterMultiplier));
            double height = Math.Clamp(14 + (factor / maxFactor) * 110, 14, 130);
            long estTok = (long)val * 1850L;
            string tokLabel = estTok >= 1000 ? $"{estTok / 1000}K tok" : $"{estTok} tok";

            string barColor = factor > 0.8 ? "#8B5CF6" : (factor > 0.5 ? "#3B82F6" : "#06B6D4");

            DashboardChartPoints.Add(new ChartDataPoint
            {
                Label = lbl,
                Value = val,
                Height = height,
                TokensLabel = tokLabel,
                TooltipText = $"{lbl}: {val} prompts ({tokLabel})",
                BarColor = barColor
            });
        }
    }

    private void UpdateAnalyticsViews()
    {
        // 1. Model Efficiencies
        ModelEfficiencies.Clear();
        var models = new[]
        {
            ("gemini-2.5-flash", "Pro/Plus", Math.Max(12, TotalInteractionsCount / 2), "124 t/s", "< 0.1%"),
            ("gemini-2.5-pro", "Pro/Ultra", Math.Max(5, TotalInteractionsCount / 3), "78 t/s", "0.2%"),
            ("claude-3.7-sonnet", "Ultra", Math.Max(3, TotalInteractionsCount / 5), "65 t/s", "0.0%"),
            ("gpt-4o", "Plus/Pro", Math.Max(2, TotalInteractionsCount / 6), "82 t/s", "0.4%")
        };

        foreach (var (m, tier, reqs, spd, err) in models)
        {
            long tok = (long)reqs * 2100L;
            ModelEfficiencies.Add(new ModelEfficiencyItem
            {
                ModelName = m,
                Tier = tier,
                Requests = reqs,
                Tokens = tok >= 1000 ? $"{tok / 1000}K" : tok.ToString(),
                AvgSpeed = spd,
                ErrorRate = err,
                Status = "Healthy",
                StatusColor = "#10B981"
            });
        }

        // 2. Swarm Health Records
        SwarmHealthRecords.Clear();
        foreach (var p in Profiles)
        {
            SwarmHealthRecords.Add(new SwarmHealthItem
            {
                AccountName = p.Name,
                Tier = p.Tier,
                TierColor = p.TierBadgeBackground,
                CurrentModel = p.CurrentModel,
                UsageLabel = p.UsageLabel,
                UsagePercent = p.UsagePercentage,
                StatusText = p.QuotaStatusText,
                StatusColor = p.QuotaStatusColor
            });
        }

        // 3. Hourly Heatmap (24 hours)
        HourlyHeatmap.Clear();
        var pattern = new[]
        {
            0.1, 0.05, 0.02, 0.01, 0.02, 0.08, // 00-05
            0.2, 0.45, 0.70, 0.85, 0.90, 0.75, // 06-11
            0.65, 0.80, 0.95, 0.88, 0.72, 0.60, // 12-17
            0.55, 0.70, 0.82, 0.65, 0.40, 0.20  // 18-23
        };

        for (int h = 0; h < 24; h++)
        {
            double intensity = pattern[h];
            string color = intensity > 0.8 ? "#8B5CF6" : (intensity > 0.5 ? "#3B82F6" : (intensity > 0.2 ? "#06B6D4" : "#334155"));
            HourlyHeatmap.Add(new HourlyActivityItem
            {
                HourLabel = $"{h:D2}h",
                Height = Math.Max(6, intensity * 40),
                Color = color,
                Tooltip = $"{h:D2}:00 - Activity intensity: {(int)(intensity * 100)}%"
            });
        }
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
            MinimizeToTray = MinimizeToTray,
            SoundEnabled = SoundEnabled,
            CustomAgyExecutablePath = DetectedAgyPath
        };
        await _storageService.SaveSettingsAsync(settings);
    }
}
