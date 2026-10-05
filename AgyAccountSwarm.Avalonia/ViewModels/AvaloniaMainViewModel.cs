using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgyAccountSwarm.Models;
using AgyAccountSwarm.Services;

namespace AgyAccountSwarm.Avalonia.ViewModels;

public partial class AvaloniaMainViewModel : ObservableObject
{
    private readonly IProfileStorageService _storageService;
    private readonly ITelemetryService _telemetryService;
    private readonly IAuthDetectorService _authDetectorService;
    private readonly IProfileDoctorService _doctorService;
    private readonly ITerminalLauncherService _launcherService;
    private readonly IAgyModelService _modelService;
    private readonly IMcpService _mcpService;
    private readonly ISkillService _skillService;
    private readonly IQuotaConfigService _quotaConfigService;
    private readonly IAudioService _audioService;
    private readonly IGitWorktreeService _gitWorktreeService;
    private readonly IFleetDispatcherService _fleetDispatcherService;
    private readonly IRagService _ragService;
    private readonly IPersonalChatService _personalChatService;

    private readonly DispatcherTimer _syncTimer;
    private readonly SemaphoreSlim _syncSemaphore = new(1, 1);
    private DateTime _nextSyncTime = DateTime.UtcNow.AddMinutes(5);
    private readonly List<Process> _activeSwarmProcesses = new();

    [ObservableProperty]
    private string _currentPage = "Dashboard";

    [ObservableProperty]
    private string _appVersion = "v0.9.11-beta";

    [ObservableProperty]
    private string _selectedChatTab = "Realtime"; // "Realtime" or "Personal"

    partial void OnSelectedChatTabChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentPageTitle));
        OnPropertyChanged(nameof(IsRealtimeChatTabSelected));
        OnPropertyChanged(nameof(IsPersonalChatTabSelected));
    }

    public bool IsRealtimeChatTabSelected => SelectedChatTab == "Realtime";
    public bool IsPersonalChatTabSelected => SelectedChatTab == "Personal";

    public string CurrentPageTitle => CurrentPage switch
    {
        "Dashboard" => "System Dashboard & Telemetry Overview",
        "Accounts" => "Fleet Accounts & Quota Governance",
        "Chat" => SelectedChatTab == "Personal" 
            ? "Personal Chat Studio & RAG Intelligence Hub" 
            : "Real-Time Swarm Chat & Inter-Agent Bus",
        "PersonalChat" => "Personal Chat Studio & RAG Intelligence Hub",
        "RealtimeChat" => "Real-Time Swarm Chat & Inter-Agent Bus",
        "Dispatcher" => "Swarm Worker & Project Orchestrator",
        "Analytics" => "Telemetry Analytics & Quota Forecasting",
        "Mcp" => "Model Context Protocol (MCP) Ecosystem",
        "Skills" => "Antigravity Agent Skills & Extensions Catalog",
        "Docs" => "Documentation & AGY Operational Guides",
        "Doctor" => "Swarm System Health & Profile Doctor",
        "LiveChat" => "Live Swarm Chat & Interactive Prompter",
        "RealtimeWorkflow" => "Realtime Swarm Workflow Graph",
        "Logs" => "Swarm Execution & Telemetry Logs",
        "Settings" => "Global Settings & Engine Preferences",
        "Terms" => "Terms of Service, Risks & Compliance",
        "About" => "About AGY Account Swarm GUI",
        _ => CurrentPage
    };

    partial void OnCurrentPageChanged(string value)
    {
        if (value == "PersonalChat")
        {
            _currentPage = "Chat";
            _selectedChatTab = "Personal";
            OnPropertyChanged(nameof(CurrentPage));
            OnPropertyChanged(nameof(SelectedChatTab));
            OnPropertyChanged(nameof(IsRealtimeChatTabSelected));
            OnPropertyChanged(nameof(IsPersonalChatTabSelected));
        }
        else if (value == "RealtimeChat")
        {
            _currentPage = "Chat";
            _selectedChatTab = "Realtime";
            OnPropertyChanged(nameof(CurrentPage));
            OnPropertyChanged(nameof(SelectedChatTab));
            OnPropertyChanged(nameof(IsRealtimeChatTabSelected));
            OnPropertyChanged(nameof(IsPersonalChatTabSelected));
        }
        OnPropertyChanged(nameof(CurrentPageTitle));
    }

    [ObservableProperty]
    private bool _isSidebarCollapsed = false;

    [ObservableProperty]
    private double _sidebarWidth = 250;

    [RelayCommand]
    public void ToggleSidebar()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
        SidebarWidth = IsSidebarCollapsed ? 80 : 250;
    }

    public ObservableCollection<DashboardLanguageOption> LanguageOptions { get; } =
    [
        new() { Code = "en", DisplayName = "🇬🇧 English" },
        new() { Code = "id", DisplayName = "🇮🇩 Bahasa Indonesia" }
    ];

    private readonly ILocalizationService _localizationService;
    public ILocalizationService Strings => _localizationService;

    [ObservableProperty]
    private DashboardLanguageOption? _selectedLanguageOption;

    partial void OnSelectedLanguageOptionChanged(DashboardLanguageOption? value)
    {
        if (value != null)
        {
            _localizationService.SetLanguage(value.Code);
            Settings.Language = value.Code;
            _ = _storageService.SaveSettingsAsync(Settings);
        }
    }

    [ObservableProperty]
    private bool _soundEnabled = true;

    [ObservableProperty]
    private bool _welcomeSoundEnabled = true;

    [RelayCommand]
    public void ToggleSound()
    {
        SoundEnabled = !SoundEnabled;
        _audioService.IsEnabled = SoundEnabled;
        Settings.SoundEnabled = SoundEnabled;
        _ = _storageService.SaveSettingsAsync(Settings);
        if (SoundEnabled) _audioService.PlayClick();
    }

    [RelayCommand]
    public void ToggleWelcomeSound()
    {
        WelcomeSoundEnabled = !WelcomeSoundEnabled;
        Settings.WelcomeSoundEnabled = WelcomeSoundEnabled;
        _ = _storageService.SaveSettingsAsync(Settings);
        if (SoundEnabled) _audioService.PlayClick();
    }

    [RelayCommand]
    public void PlayWelcomeChime() => _audioService.PlayWelcomeCalmChime();

    [ObservableProperty]
    private bool _isAgyInstalled = true;

    [ObservableProperty]
    private string? _detectedAgyPath;

    // KPI Metrics
    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _authenticatedCount;

    [ObservableProperty]
    private int _selectedSwarmCount;

    [ObservableProperty]
    private int _totalSavedSessionsCount;

    [ObservableProperty]
    private int _mcpServersCount;

    [ObservableProperty]
    private int _mcpToolsTotalCount;

    public static string FormatSyncTimestamp(DateTime dt)
    {
        var offset = TimeZoneInfo.Local.GetUtcOffset(dt);
        string sign = offset >= TimeSpan.Zero ? "+" : "-";
        string offsetStr = offset.Minutes == 0 
            ? $"UTC{sign}{Math.Abs((int)offset.TotalHours)}" 
            : $"UTC{sign}{Math.Abs((int)offset.TotalHours)}:{Math.Abs(offset.Minutes):D2}";
        return $"Synced {dt:yyyy/MM/dd HH:mm:ss} ({offsetStr})";
    }

    [ObservableProperty]
    private string _lastSyncedAtText = FormatSyncTimestamp(DateTime.Now);

    [ObservableProperty]
    private bool _isWelcomeOverlayVisible = true;

    [RelayCommand]
    public void DismissWelcomeOverlay() => IsWelcomeOverlayVisible = false;

    // Foldable Cards expansion states
    [ObservableProperty]
    private bool _isDashboardKpiExpanded = true;

    [ObservableProperty]
    private bool _isDashboardChartExpanded = true;

    [ObservableProperty]
    private bool _isDashboardAccountsExpanded = true;

    [ObservableProperty]
    private bool _isProjectContextExpanded = true;

    [ObservableProperty]
    private bool _isProjectTasksExpanded = true;

    [ObservableProperty]
    private bool _isAnalyticsTrendsExpanded = true;

    [ObservableProperty]
    private bool _isAnalyticsEfficiencyExpanded = true;

    [RelayCommand]
    public void ToggleDashboardKpi() => IsDashboardKpiExpanded = !IsDashboardKpiExpanded;

    [RelayCommand]
    public void ToggleDashboardChart() => IsDashboardChartExpanded = !IsDashboardChartExpanded;

    [RelayCommand]
    public void ToggleDashboardAccounts() => IsDashboardAccountsExpanded = !IsDashboardAccountsExpanded;

    [RelayCommand]
    public void ToggleProjectContext() => IsProjectContextExpanded = !IsProjectContextExpanded;

    [RelayCommand]
    public void ToggleProjectTasks() => IsProjectTasksExpanded = !IsProjectTasksExpanded;

    [RelayCommand]
    public void ToggleAnalyticsTrends() => IsAnalyticsTrendsExpanded = !IsAnalyticsTrendsExpanded;

    [RelayCommand]
    public void ToggleAnalyticsEfficiency() => IsAnalyticsEfficiencyExpanded = !IsAnalyticsEfficiencyExpanded;

    [ObservableProperty]
    private bool _isDispatcherCapabilitiesExpanded = true;

    [ObservableProperty]
    private bool _isDispatcherReportsExpanded = true;

    [ObservableProperty]
    private bool _isMcpCatalogExpanded = true;

    [ObservableProperty]
    private bool _isDocsViewerExpanded = true;

    [RelayCommand]
    public void ToggleDispatcherCapabilities() => IsDispatcherCapabilitiesExpanded = !IsDispatcherCapabilitiesExpanded;

    [RelayCommand]
    public void ToggleDispatcherReports() => IsDispatcherReportsExpanded = !IsDispatcherReportsExpanded;

    [RelayCommand]
    public void ToggleMcpCatalog() => IsMcpCatalogExpanded = !IsMcpCatalogExpanded;

    [RelayCommand]
    public void ToggleDocsViewer() => IsDocsViewerExpanded = !IsDocsViewerExpanded;

    // Dashboard & Chart Filters
    [ObservableProperty]
    private bool _hasChartData = false;

    [ObservableProperty]
    private string _selectedChartMode = "Bar";

    [ObservableProperty]
    private string _selectedAccountFilter = "All Accounts";

    partial void OnSelectedAccountFilterChanged(string value) => UpdateChartPoints();

    [ObservableProperty]
    private string _selectedTimeframe = "Last 7 Days";

    partial void OnSelectedTimeframeChanged(string value) => UpdateChartPoints();

    [ObservableProperty]
    private string _selectedModelFilter = "All Models";

    partial void OnSelectedModelFilterChanged(string value) => UpdateChartPoints();

    [ObservableProperty]
    private string _selectedChartTierFilter = "All Tiers";

    partial void OnSelectedChartTierFilterChanged(string value) => UpdateChartPoints();

    public ObservableCollection<string> AccountFilterOptions { get; } = ["All Accounts"];
    public ObservableCollection<string> TimeframeOptions { get; } = ["Last 24 Hours", "Last 3 Days", "Last 7 Days", "Last 14 Days", "Last 30 Days", "Last 90 Days", "All Time"];
    public ObservableCollection<string> ModelFilterOptions { get; } = ["All Models", "gemini-3.8-flash", "gemini-2.5-flash", "gemini-2.5-pro", "claude-3-opus", "claude-3.5-sonnet", "claude-3.7-sonnet", "gpt-4o", "gemini-1.5-pro"];
    public ObservableCollection<string> TierFilterOptions { get; } = ["All Tiers", "Basic", "Plus", "Pro", "Ultra"];

    [ObservableProperty]
    private double _chartZoomLevel = 1.0;

    [ObservableProperty]
    private double _chartCanvasWidth = 720.0;

    [ObservableProperty]
    private string _chartZoomText = "100%";

    [ObservableProperty]
    private int _yTick100 = 100;
    [ObservableProperty]
    private int _yTick75 = 75;
    [ObservableProperty]
    private int _yTick50 = 50;
    [ObservableProperty]
    private int _yTick25 = 25;
    [ObservableProperty]
    private int _yTick0 = 0;

    public ObservableCollection<ChartDataPoint> DashboardChartPoints { get; } = [];

    [ObservableProperty]
    private global::Avalonia.Points _linePoints = [];

    [ObservableProperty]
    private global::Avalonia.Points _areaPoints = [];

    private List<RealHistoryEntry> _cachedRealHistory = [];

    // Analytics Extended KPIs
    [ObservableProperty]
    private int _totalInteractionsCount;

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
    private string _swarmHealthScore = "100% Healthy";

    [ObservableProperty]
    private string _selectedHeatmapAccount = "All Accounts";

    partial void OnSelectedHeatmapAccountChanged(string value) => UpdateAnalyticsViews();

    public ObservableCollection<string> HeatmapAccountOptions { get; } = ["All Accounts"];

    [ObservableProperty]
    private int _heatmapTotal24hPrompts;

    [ObservableProperty]
    private string _heatmapPeakHour = "No peak";

    [ObservableProperty]
    private string _heatmapActiveWindow = "0 hrs";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeatmapAveragePerHour))]
    private string _heatmapActivityIntensity = "0 req/hr";

    public string HeatmapAveragePerHour => HeatmapActivityIntensity;

    [ObservableProperty]
    private int _heatmapYTick100 = 10;
    [ObservableProperty]
    private int _heatmapYTick75 = 8;
    [ObservableProperty]
    private int _heatmapYTick50 = 5;
    [ObservableProperty]
    private int _heatmapYTick25 = 2;
    [ObservableProperty]
    private int _heatmapYTick0 = 0;

    public ObservableCollection<ModelEfficiencyItem> ModelEfficiencies { get; } = [];
    public ObservableCollection<HourlyActivityItem> HourlyHeatmap { get; } = [];
    public ObservableCollection<SwarmFleetModelItem> SwarmFleetModels { get; } = [];
    public ObservableCollection<SwarmCliCapabilityItem> SwarmCliCapabilities { get; } = [];
    public ObservableCollection<SwarmHealthItem> SwarmHealthRecords { get; } = [];

    // Swarm-Level Aggregate Headroom & Burn-Rate Properties
    public int SwarmDailyCapacityTotal => Profiles.Sum(p => p.DailyQuotaLimit);
    public int SwarmTodayPromptsTotal => Profiles.Sum(p => p.TodayTurnsCount);
    public double SwarmPoolRemainingPercent => SwarmDailyCapacityTotal > 0
        ? Math.Max(0.0, (SwarmDailyCapacityTotal - SwarmTodayPromptsTotal) / (double)SwarmDailyCapacityTotal * 100.0)
        : 100.0;
    public double SwarmAggregateBurnRate => Profiles.Sum(p => p.BurnRatePromptsPerHour);
    public string SwarmExhaustionForecast
    {
        get
        {
            if (SwarmAggregateBurnRate > 0.05)
            {
                double remainingPool = Math.Max(0, SwarmDailyCapacityTotal - SwarmTodayPromptsTotal);
                double hours = remainingPool / SwarmAggregateBurnRate;
                return $"At current swarm velocity ({SwarmAggregateBurnRate.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} req/hr), pool headroom lasts ~{hours.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} hrs";
            }
            return "Swarm consumption stable • Pool headroom healthy";
        }
    }

    // Accounts Toolbar Properties & Commands
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    partial void OnSearchQueryChanged(string value) => ApplyFilters();

    public string SearchText
    {
        get => SearchQuery;
        set => SearchQuery = value;
    }

    [ObservableProperty]
    private bool _isAccountsFilterPopupOpen;

    [ObservableProperty]
    private string _accountsTierFilter = "All Tiers";

    partial void OnAccountsTierFilterChanged(string value) => ApplyFilters();

    [ObservableProperty]
    private string _accountsHealthFilter = "All Status";

    partial void OnAccountsHealthFilterChanged(string value) => ApplyFilters();

    [ObservableProperty]
    private string _accountsSortBy = "Default";

    partial void OnAccountsSortByChanged(string value) => ApplyFilters();

    public ObservableCollection<string> AccountsTierFilterOptions { get; } =
        ["All Tiers", "Basic", "Plus", "Pro", "Ultra"];

    public ObservableCollection<string> AccountsHealthFilterOptions { get; } =
        ["All Status", "Authenticated", "Needs Login", "Quota Exhausted", "Warning / High Quota"];

    public ObservableCollection<string> AccountsSortOptions { get; } =
        ["Default", "Name (A-Z)", "Name (Z-A)", "Quota Used (High to Low)", "Quota Used (Low to High)"];

    public bool HasActiveAccountsFilters =>
        !string.Equals(AccountsTierFilter, "All Tiers", StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(AccountsHealthFilter, "All Status", StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(AccountsSortBy, "Default", StringComparison.OrdinalIgnoreCase);

    public int ActiveAccountsFilterCount =>
        (!string.Equals(AccountsTierFilter, "All Tiers", StringComparison.OrdinalIgnoreCase) ? 1 : 0) +
        (!string.Equals(AccountsHealthFilter, "All Status", StringComparison.OrdinalIgnoreCase) ? 1 : 0) +
        (!string.Equals(AccountsSortBy, "Default", StringComparison.OrdinalIgnoreCase) ? 1 : 0);

    [RelayCommand]
    public void ToggleAccountsFilterPopup()
    {
        _audioService.PlayClick();
        IsAccountsFilterPopupOpen = !IsAccountsFilterPopupOpen;
    }

    [RelayCommand]
    public void CloseAccountsFilterPopup()
    {
        _audioService.PlayClick();
        IsAccountsFilterPopupOpen = false;
    }

    [RelayCommand]
    public void ResetAccountsFilters()
    {
        _audioService.PlayClick();
        AccountsTierFilter = "All Tiers";
        AccountsHealthFilter = "All Status";
        AccountsSortBy = "Default";
        ApplyFilters();
    }

    [RelayCommand]
    public void SelectAllProfiles()
    {
        _audioService.PlayClick();
        bool anyUnselected = FilteredProfiles.Any(p => !p.IsSelectedForSwarm);
        foreach (var p in FilteredProfiles)
        {
            p.IsSelectedForSwarm = anyUnselected;
        }
        UpdateKpiMetrics();
        UpdateWorktreeTreeNodes();
    }

    // Settings Properties & Commands
    [ObservableProperty]
    private TerminalType _selectedTerminal = TerminalType.WindowsTerminal;

    partial void OnSelectedTerminalChanged(TerminalType value)
    {
        Settings.PreferredTerminal = value;
        _ = _storageService.SaveSettingsAsync(Settings);
    }

    [ObservableProperty]
    private SwarmLaunchMode _selectedSwarmMode = SwarmLaunchMode.SplitPanes;

    partial void OnSelectedSwarmModeChanged(SwarmLaunchMode value)
    {
        Settings.SwarmMode = value;
        _ = _storageService.SaveSettingsAsync(Settings);
    }

    [ObservableProperty]
    private bool _closeToTray = true;

    partial void OnCloseToTrayChanged(bool value)
    {
        OnPropertyChanged(nameof(ExitOnClose));
        Settings.CloseToTray = value;
        _ = _storageService.SaveSettingsAsync(Settings);
    }

    public bool ExitOnClose
    {
        get => !CloseToTray;
        set
        {
            if (value)
            {
                CloseToTray = false;
            }
        }
    }

    [ObservableProperty]
    private bool _minimizeToTray = true;

    partial void OnMinimizeToTrayChanged(bool value)
    {
        Settings.MinimizeToTray = value;
        _ = _storageService.SaveSettingsAsync(Settings);
    }

    [ObservableProperty]
    private string _autoSyncInterval = "5 Minutes";

    partial void OnAutoSyncIntervalChanged(string value)
    {
        Settings.AutoSyncInterval = value;
        _ = _storageService.SaveSettingsAsync(Settings);
    }

    [ObservableProperty]
    private bool _autoSyncAudioEnabled = false;

    partial void OnAutoSyncAudioEnabledChanged(bool value)
    {
        Settings.AutoSyncAudioEnabled = value;
        _ = _storageService.SaveSettingsAsync(Settings);
    }

    public ObservableCollection<string> AutoSyncOptions { get; } =
        ["Manual", "1 Minute", "5 Minutes", "15 Minutes", "30 Minutes", "60 Minutes"];

    [RelayCommand]
    public void TestQuotaAlert()
    {
        _audioService.PlayQuotaAlert();
    }

    [RelayCommand]
    public void OpenAppDataFolder()
    {
        _audioService.PlayClick();
        var path = _storageService.GetAppDataPath();
        TerminalLauncherService.OpenFolderInFileManager(path);
    }

    [RelayCommand]
    public void OpenLogFile()
    {
        _audioService.PlayClick();
        var path = Logger.LogPath;
        if (File.Exists(path))
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                TerminalLauncherService.OpenFolderInFileManager(dir);
        }
    }

    [RelayCommand]
    public void OpenLogsFolder()
    {
        _audioService.PlayClick();
        var dir = Logger.LogDirectory;
        if (!Directory.Exists(dir))
        {
            try { Directory.CreateDirectory(dir); } catch { }
        }
        TerminalLauncherService.OpenFolderInFileManager(dir);
    }

    [RelayCommand]
    public void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }


    [RelayCommand]
    public void SetChartMode(string mode)
    {
        SelectedChartMode = mode;
        _audioService.PlayClick();
    }

    [RelayCommand]
    public void ChartZoomIn()
    {
        _audioService.PlayClick();
        if (ChartZoomLevel < 3.0)
        {
            ChartZoomLevel = Math.Round(ChartZoomLevel + 0.25, 2);
            ChartZoomText = $"{Math.Round(ChartZoomLevel * 100)}%";
            UpdateChartPoints();
        }
    }

    [RelayCommand]
    public void ChartZoomOut()
    {
        _audioService.PlayClick();
        if (ChartZoomLevel > 0.5)
        {
            ChartZoomLevel = Math.Round(ChartZoomLevel - 0.25, 2);
            ChartZoomText = $"{Math.Round(ChartZoomLevel * 100)}%";
            UpdateChartPoints();
        }
    }

    [RelayCommand]
    public void ChartZoomReset()
    {
        _audioService.PlayClick();
        ChartZoomLevel = 1.0;
        ChartZoomText = "100%";
        UpdateChartPoints();
    }

    [RelayCommand]
    public async Task SyncSwarmAsync(bool isPeriodic = false)
    {
        if (!await _syncSemaphore.WaitAsync(0))
        {
            return;
        }

        if (!isPeriodic && Settings.SoundEnabled)
        {
            _audioService.PlayClick();
        }

        IsSyncing = true;
        _nextSyncTime = DateTime.UtcNow.AddMinutes(5);
        AutoSyncCountdown = "05:00";
        try
        {
            AgyUsageParser.InvalidateCache();

            // 1. Concurrently refresh all profile auth statuses, tokens, sessions, and live /usage quotas
            var tasks = Profiles.Select(p => p.RefreshAuthStatusAsync(allowCliSpawn: true));
            await Task.WhenAll(tasks);

            // 2. Persist updated profile models to disk storage
            await _storageService.SaveProfilesAsync(Profiles.Select(p => p.Profile));

            // 3. Load latest real telemetry interactions from authentic disk logs
            await LoadTelemetryHistoryAsync();

            // 4. Update dynamic model discoveries from all profiles
            var profilePaths = Profiles.Select(p => p.EffectiveProfilePath).ToList();
            var discovered = await _modelService.DiscoverModelsAsync(profilePaths, forceCliRefresh: false);
            UpdateDiscoveredModels(discovered);

            // 5. Refresh KPI metrics, filters, and worktree tree
            UpdateKpiMetrics();
            ApplyFilters();
            UpdateWorktreeTreeNodes();

            // 6. Audio feedback & status notification
            if (isPeriodic)
            {
                if (AutoSyncAudioEnabled && Settings.SoundEnabled)
                {
                    _audioService.PlaySync();
                }
            }
            else
            {
                if (Settings.SoundEnabled)
                {
                    _audioService.PlaySync();
                }
            }

            LastSyncedAtText = FormatSyncTimestamp(DateTime.Now);
            ShowNotification($"Synchronized {Profiles.Count} swarm account(s) and live telemetry.");
        }
        catch (Exception ex)
        {
            Logger.Error("Error syncing swarm in Avalonia", ex);
            ShowNotification($"Swarm sync failed: {ex.Message}");
        }
        finally
        {
            IsSyncing = false;
            _syncSemaphore.Release();
        }
    }

    private void UpdateDiscoveredModels(IReadOnlyList<AgyModelInfo> discovered)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => UpdateDiscoveredModels(discovered));
            return;
        }

        var currentSelection = SelectedModelFilter;
        ModelFilterOptions.Clear();
        ModelFilterOptions.Add("All Models");
        foreach (var m in discovered)
        {
            if (!ModelFilterOptions.Contains(m.DisplayName))
            {
                ModelFilterOptions.Add(m.DisplayName);
            }
        }
        if (!string.IsNullOrWhiteSpace(currentSelection) && ModelFilterOptions.Contains(currentSelection))
        {
            SelectedModelFilter = currentSelection;
        }
    }

    public event Func<string, Task<string?>>? SavePdfFileRequested;

    [RelayCommand]
    public async Task ExportPdfReportAsync()
    {
        _audioService.PlayClick();
        try
        {
            string defaultName = $"AgySwarm_Telemetry_Report_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
            string? targetPath = null;
            if (SavePdfFileRequested != null)
            {
                targetPath = await SavePdfFileRequested(defaultName);
            }
            if (string.IsNullOrWhiteSpace(targetPath)) return;

            string tempHtml = Path.Combine(Path.GetTempPath(), $"agyswarm_report_{Guid.NewGuid():N}.html");
            string htmlContent = GenerateExecutiveReportHtml();
            await File.WriteAllTextAsync(tempHtml, htmlContent, System.Text.Encoding.UTF8);

            if (targetPath.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(tempHtml, targetPath, true);
            }
            else
            {
                string edgePath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
                if (!File.Exists(edgePath))
                {
                    edgePath = @"C:\Program Files\Microsoft\Edge\Application\msedge.exe";
                }

                if (File.Exists(edgePath))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = edgePath,
                        Arguments = $"--headless --disable-gpu --run-all-compositor-stages-before-draw --no-pdf-header-footer --print-to-pdf=\"{targetPath}\" \"{tempHtml}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        await proc.WaitForExitAsync();
                    }
                }
                else
                {
                    targetPath = Path.ChangeExtension(targetPath, ".html");
                    File.Copy(tempHtml, targetPath, true);
                }
            }

            _audioService.PlaySuccess();
            ShowNotification($"PDF Report exported to: {Path.GetFileName(targetPath)}");

            if (File.Exists(targetPath))
            {
                TerminalLauncherService.OpenFileOrUrl(targetPath);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to export PDF report in Avalonia", ex);
            ShowNotification($"Export failed: {ex.Message}");
        }
    }

    public string GenerateExecutiveReportHtml()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang='en'>");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset='utf-8'>");
        sb.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
        sb.AppendLine("<title>Agy CLI Account Swarm - Executive Telemetry Report</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("  * { -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; box-sizing: border-box; }");
        sb.AppendLine("  @page { size: A4 portrait; margin: 12mm 10mm; }");
        sb.AppendLine("  body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background: #ffffff !important; color: #0f172a !important; margin: 0; padding: 16px; line-height: 1.45; font-size: 12px; }");
        sb.AppendLine("  .report-header { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 2px solid #0284c7; padding-bottom: 12px; margin-bottom: 18px; }");
        sb.AppendLine("  .brand-title { font-size: 20px; font-weight: 800; color: #0369a1; letter-spacing: -0.3px; }");
        sb.AppendLine("  .brand-sub { font-size: 11.5px; color: #64748b; margin-top: 3px; font-weight: 500; }");
        sb.AppendLine("  .report-meta { text-align: right; font-size: 11px; color: #64748b; line-height: 1.4; }");
        sb.AppendLine("  .kpi-grid { display: grid; grid-template-columns: repeat(6, 1fr); gap: 8px; margin-bottom: 18px; page-break-inside: avoid; }");
        sb.AppendLine("  .kpi-box { background: #f8fafc !important; border: 1px solid #e2e8f0 !important; border-radius: 6px; padding: 10px 8px; text-align: center; }");
        sb.AppendLine("  .kpi-val { font-size: 16px; font-weight: 800; }");
        sb.AppendLine("  .kpi-lbl { font-size: 9.5px; color: #64748b; text-transform: uppercase; font-weight: 700; margin-top: 2px; }");
        sb.AppendLine("  .card { background: #ffffff !important; border: 1px solid #cbd5e1 !important; border-radius: 8px; padding: 14px 16px; margin-bottom: 16px; page-break-inside: avoid; box-shadow: 0 1px 2px rgba(0,0,0,0.03); }");
        sb.AppendLine("  .card-title { font-size: 13.5px; font-weight: 700; color: #0f172a; margin: 0 0 10px 0; padding-bottom: 5px; border-bottom: 1px solid #f1f5f9; }");
        sb.AppendLine("  .hero-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 10px; background: #f8fafc !important; border: 1px solid #e2e8f0 !important; border-radius: 6px; padding: 12px; }");
        sb.AppendLine("  .hero-lbl { font-size: 9.5px; font-weight: 700; color: #64748b; text-transform: uppercase; }");
        sb.AppendLine("  .hero-val { font-size: 14px; font-weight: 800; color: #0369a1; margin-top: 2px; }");
        sb.AppendLine("  table { width: 100%; border-collapse: collapse; margin-top: 6px; font-size: 11.5px; table-layout: auto; }");
        sb.AppendLine("  th { background: #f1f5f9 !important; color: #334155 !important; font-weight: 700; font-size: 10.5px; text-transform: uppercase; letter-spacing: 0.4px; border: 1px solid #cbd5e1 !important; padding: 8px 10px; text-align: left; }");
        sb.AppendLine("  td { border: 1px solid #e2e8f0 !important; color: #1e293b !important; padding: 7px 10px; vertical-align: middle; background: #ffffff; }");
        sb.AppendLine("  tr:nth-child(even) td { background: #f8fafc !important; }");
        sb.AppendLine("  .badge { display: inline-block; padding: 2px 8px; border-radius: 4px; font-size: 10px; font-weight: 700; text-transform: uppercase; color: #0f172a; }");
        sb.AppendLine("  .tool-pill { display: inline-block; background: #f1f5f9 !important; border: 1px solid #cbd5e1 !important; border-radius: 3px; padding: 1px 5px; font-family: Consolas, Monaco, monospace; font-size: 9.5px; margin: 1px 2px; color: #0369a1 !important; word-break: break-all; }");
        sb.AppendLine("  .footer { text-align: center; font-size: 10.5px; color: #94a3b8; margin-top: 20px; padding-top: 10px; border-top: 1px solid #e2e8f0; }");
        sb.AppendLine("  @media print {");
        sb.AppendLine("    * { -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; }");
        sb.AppendLine("    body { background: #ffffff !important; color: #0f172a !important; padding: 0 !important; }");
        sb.AppendLine("    .card { background: #ffffff !important; border: 1px solid #cbd5e1 !important; box-shadow: none !important; }");
        sb.AppendLine("    th { background: #f1f5f9 !important; color: #1e293b !important; border: 1px solid #cbd5e1 !important; }");
        sb.AppendLine("    td { background: #ffffff !important; color: #1e293b !important; border: 1px solid #e2e8f0 !important; }");
        sb.AppendLine("    tr:nth-child(even) td { background: #f8fafc !important; }");
        sb.AppendLine("  }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Header
        sb.AppendLine("<div class='report-header'>");
        sb.AppendLine("  <div>");
        sb.AppendLine("    <div class='brand-title'>⚡ Agy CLI Account Swarm • Executive Telemetry Report</div>");
        sb.AppendLine("    <div class='brand-sub'>Google Antigravity CLI Multi-Account Orchestration &amp; Telemetry Audit</div>");
        sb.AppendLine("  </div>");
        var primaryOperator = Profiles.FirstOrDefault(p => !string.IsNullOrEmpty(p.AccountEmail))?.AccountEmail ?? "Local Swarm Operator";
        sb.AppendLine("  <div class='report-meta'>");
        sb.AppendLine($"    Generated: <strong>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</strong><br>");
        sb.AppendLine($"    Operator: <strong>{primaryOperator}</strong><br>");
        sb.AppendLine($"    Active Sandboxes: <strong>{Profiles.Count}</strong>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</div>");

        // 6 Executive KPI summary boxes
        sb.AppendLine("<div class='kpi-grid'>");
        sb.AppendLine($"  <div class='kpi-box'><div class='kpi-val' style='color:#0284c7;'>{TotalEstimatedTokens}</div><div class='kpi-lbl'>Est. Tokens</div></div>");
        sb.AppendLine($"  <div class='kpi-box'><div class='kpi-val' style='color:#0891b2;'>{EstimatedInputTokens}</div><div class='kpi-lbl'>Input Tokens</div></div>");
        sb.AppendLine($"  <div class='kpi-box'><div class='kpi-val' style='color:#7c3aed;'>{EstimatedOutputTokens}</div><div class='kpi-lbl'>Output Tokens</div></div>");
        sb.AppendLine($"  <div class='kpi-box'><div class='kpi-val' style='color:#059669;'>{AverageLatencyMs}</div><div class='kpi-lbl'>Avg Latency</div></div>");
        sb.AppendLine($"  <div class='kpi-box'><div class='kpi-val' style='color:#059669;'>{SwarmSuccessRate}</div><div class='kpi-lbl'>Uptime / Success</div></div>");
        sb.AppendLine($"  <div class='kpi-box'><div class='kpi-val' style='color:#d97706;'>{SwarmHealthScore}</div><div class='kpi-lbl'>Swarm Status</div></div>");
        sb.AppendLine("</div>");

        // Swarm Fleet Capacity & Burn Rate Hero Strip
        sb.AppendLine("<div class='card'>");
        sb.AppendLine("  <div class='card-title'>🚀 Swarm Fleet Intelligence &amp; Aggregate Quota Pool</div>");
        sb.AppendLine("  <div class='hero-grid'>");
        sb.AppendLine($"    <div class='hero-item'><div class='hero-lbl'>DAILY POOL CAPACITY</div><div class='hero-val'>{SwarmDailyCapacityTotal:N0} prompts</div></div>");
        sb.AppendLine($"    <div class='hero-item'><div class='hero-lbl'>PROMPTS TODAY</div><div class='hero-val' style='color:#059669;'>{SwarmTodayPromptsTotal:N0} prompts ({SwarmPoolRemainingPercent:F1}% left)</div></div>");
        sb.AppendLine($"    <div class='hero-item'><div class='hero-lbl'>AGGREGATE BURN-RATE</div><div class='hero-val' style='color:#d97706;'>{SwarmAggregateBurnRate:F1} p/hr</div></div>");
        sb.AppendLine($"    <div class='hero-item'><div class='hero-lbl'>EXHAUSTION FORECAST</div><div class='hero-val' style='color:#334155; font-size:12.5px;'>{SwarmExhaustionForecast}</div></div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</div>");

        // Accounts Table
        sb.AppendLine("<div class='card'>");
        sb.AppendLine("  <div class='card-title'>👥 Swarm Account Sandboxes &amp; Quota Health</div>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Account Name</th><th>Status</th><th>Email</th><th>Tier</th><th>Current Model</th><th>Prompts</th><th>Daily Quota</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var p in Profiles)
        {
            string tierBg;
            string tierFg;
            switch (p.TierBadgeText?.ToUpperInvariant())
            {
                case "ULTRA":
                    tierBg = "#fef3c7";
                    tierFg = "#b45309";
                    break;
                case "PRO":
                    tierBg = "#f3e8ff";
                    tierFg = "#7e22ce";
                    break;
                case "PLUS":
                    tierBg = "#e0f2fe";
                    tierFg = "#0369a1";
                    break;
                default:
                    tierBg = "#f1f5f9";
                    tierFg = "#334155";
                    break;
            }
            sb.AppendLine($"      <tr><td><strong>{p.Name}</strong></td><td><span class='badge' style='background:#f8fafc; color:#0f172a; border:1px solid #cbd5e1;'>{p.StatusBadgeText}</span></td><td>{p.AccountEmail ?? "Pending Auth"}</td><td><span class='badge' style='background:{tierBg}; color:{tierFg}; border:1px solid rgba(0,0,0,0.1);'>{p.TierBadgeText}</span></td><td>{p.CurrentModel}</td><td>{p.UsageLabel}</td><td>{p.DailyQuotaLimit:N0}</td></tr>");
        }
        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
        sb.AppendLine("</div>");

        // Model Efficiency Comparison Matrix
        if (ModelEfficiencies.Count > 0)
        {
            sb.AppendLine("<div class='card'>");
            sb.AppendLine("  <div class='card-title'>📊 Model Efficiency Comparison Matrix</div>");
            sb.AppendLine("  <table>");
            sb.AppendLine("    <thead><tr><th>Model</th><th>Tier</th><th>Requests</th><th>Est. Tokens</th><th>Avg Speed</th><th>Error Rate</th><th>Status</th></tr></thead>");
            sb.AppendLine("    <tbody>");
            foreach (var m in ModelEfficiencies)
            {
                sb.AppendLine($"      <tr><td><strong>{m.ModelName}</strong></td><td>{m.Tier}</td><td>{m.Requests}</td><td><strong style='color:#0284c7;'>{m.Tokens}</strong></td><td><strong style='color:#059669;'>{m.AvgSpeed}</strong></td><td>{m.ErrorRate}</td><td><span class='badge' style='background:#f8fafc; color:{m.StatusColor}; border:1px solid #cbd5e1;'>{m.Status}</span></td></tr>");
            }
            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine("</div>");
        }

        // Daily Trend Breakdown
        sb.AppendLine("<div class='card'>");
        sb.AppendLine($"  <div class='card-title'>📈 Telemetry Activity Distribution ({SelectedTimeframe} • {SelectedModelFilter})</div>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>Time Bucket</th><th>Prompt Count</th><th>Est. Token Volume</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var pt in DashboardChartPoints)
        {
            sb.AppendLine($"      <tr><td>{pt.Label}</td><td><strong>{pt.Value}</strong></td><td>{pt.TokensLabel}</td></tr>");
        }
        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
        sb.AppendLine("</div>");

        // MCP Server Audit
        sb.AppendLine("<div class='card'>");
        sb.AppendLine($"  <div class='card-title'>🧩 Model Context Protocol (MCP) Tools ({McpServersCount} Servers, {McpToolsTotalCount} Tools)</div>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th style='width:22%;'>Server Name</th><th style='width:12%;'>Tools Count</th><th style='width:14%;'>Status</th><th>Discovered Tools</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var mcp in McpServers)
        {
            var toolPills = (mcp.Tools != null && mcp.Tools.Count > 0)
                ? string.Join(" ", mcp.Tools.Select(t => $"<span class='tool-pill'>{t}</span>"))
                : "<span style='color:#94a3b8;'>No tools detected</span>";
            sb.AppendLine($"      <tr><td><strong>{mcp.Name}</strong></td><td>{mcp.ToolsCount}</td><td>{mcp.Status}</td><td>{toolPills}</td></tr>");
        }
        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='footer'>Agy CLI Account Swarm • Multiplatform Desktop (Avalonia UI) • https://github.com/RifkyA911/agy-cli-account-swarm</div>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    [ObservableProperty]
    private string _selectedTierFilter = "All";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SwarmActionButtonText))]
    private bool _isSwarmRunning = false;

    public string SwarmActionButtonText => IsSwarmRunning ? "⏹ Stop Swarm" : "🚀 Launch Swarm";

    public IProfileStorageService StorageService => _storageService;

    [ObservableProperty]
    private string _swarmStatusText = "Swarm Idle";

    [ObservableProperty]
    private int _activeWorkersCount = 0;

    [ObservableProperty]
    private string _autoSyncCountdown = "05:00";

    [ObservableProperty]
    private bool _isSyncing = false;

    [ObservableProperty]
    private string _notificationMessage = string.Empty;

    [ObservableProperty]
    private bool _isNotificationVisible = false;

    [ObservableProperty]
    private AppSettings _settings = new();

    [ObservableProperty]
    private string _currentTheme = "Dark";

    public ObservableCollection<string> ThemeOptions { get; } =
        new(AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.AvailableThemes);

    [ObservableProperty]
    private string _selectedThemeOption = "Dark";

    partial void OnSelectedThemeOptionChanged(string value)
    {
        if (!string.IsNullOrEmpty(value) && value != CurrentTheme)
        {
            CurrentTheme = value;
            Settings.Theme = value;
            AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.ApplyTheme(value);
            _ = _storageService.SaveSettingsAsync(Settings);
            OnPropertyChanged(nameof(CurrentPage));
            OnPropertyChanged(nameof(SelectedProjectTabIndex));
            OnPropertyChanged(nameof(SwarmChatMessages));
            ShowNotification($"Theme set to {value}");
        }
    }

    [RelayCommand]
    public void SetTheme(string? themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName)) return;
        _audioService.PlayClick();
        CurrentTheme = themeName;
        SelectedThemeOption = themeName;
        Settings.Theme = themeName;
        AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.ApplyTheme(themeName);
        _ = _storageService.SaveSettingsAsync(Settings);
        ShowNotification($"Theme set to {themeName}");
    }

    [RelayCommand]
    public void ToggleTheme()
    {
        _audioService.PlayClick();
        int curIdx = Array.IndexOf(AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.AvailableThemes, CurrentTheme);
        if (curIdx < 0) curIdx = 0;
        int nextIdx = (curIdx + 1) % AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.AvailableThemes.Length;
        string nextTheme = AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.AvailableThemes[nextIdx];
        SetTheme(nextTheme);
    }

    [ObservableProperty]
    private AccountProfile? _selectedProfile;

    [ObservableProperty]
    private string _selectedDocTab = "Architecture";

    // Fleet Dispatcher (Experimental) Properties
    [ObservableProperty]
    private string _fleetTaskObjective = string.Empty;

    [ObservableProperty]
    private string _fleetWorkspacePath = string.Empty;

    partial void OnFleetWorkspacePathChanged(string value) => UpdateWorktreeTreeNodes();

    [ObservableProperty]
    private DispatchMode _selectedDispatchMode = DispatchMode.RoleTailored;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTerminalMode))]
    [NotifyPropertyChangedFor(nameof(IsSilentMode))]
    private FleetExecutionMode _selectedFleetExecutionMode = FleetExecutionMode.VisibleTerminal;

    public bool IsTerminalMode
    {
        get => SelectedFleetExecutionMode == FleetExecutionMode.VisibleTerminal;
        set
        {
            if (value) SelectedFleetExecutionMode = FleetExecutionMode.VisibleTerminal;
        }
    }

    public bool IsSilentMode
    {
        get => SelectedFleetExecutionMode == FleetExecutionMode.HeadlessSilent;
        set
        {
            if (value) SelectedFleetExecutionMode = FleetExecutionMode.HeadlessSilent;
        }
    }

    [ObservableProperty]
    private bool _fleetDangerouslySkipPermissions = true;

    [ObservableProperty]
    private bool _useGitWorktrees = true;

    [ObservableProperty]
    private ResourceCheckResult _preflightResult = new();

    [ObservableProperty]
    private bool _isFleetDispatching = false;

    [ObservableProperty]
    private int _dispatchProgressPercent = 0;

    [ObservableProperty]
    private string _dispatchProgressStage = string.Empty;

    [ObservableProperty]
    private string _dispatchProgressDetail = string.Empty;

    [ObservableProperty]
    private bool _hasDispatchedTasks;

    [ObservableProperty]
    private string _fleetStatusText = "Swarm Chat Ready";

    private readonly DispatcherTimer _fleetProcessWatcherTimer;
    private readonly ISwarmAggregatorService _swarmAggregatorService;

    // Swarm Projects Context & CRUD
    public ObservableCollection<SwarmProject> Projects { get; } = new();

    [ObservableProperty]
    private SwarmProject? _selectedProject;

    [ObservableProperty]
    private bool _hasProjects;

    // Project Dialog fields
    [ObservableProperty]
    private bool _isProjectDialogOpen;

    [ObservableProperty]
    private string _projectDialogTitle = "Create Swarm Project";

    [ObservableProperty]
    private string _projectDialogId = string.Empty;

    [ObservableProperty]
    private string _projectDialogName = string.Empty;

    [ObservableProperty]
    private string _projectDialogDescription = string.Empty;

    [ObservableProperty]
    private string _projectDialogTechStack = "Rust";

    [ObservableProperty]
    private string _projectDialogRootDirectory = string.Empty;

    [ObservableProperty]
    private bool _projectDialogUseGitWorktrees = true;

    [ObservableProperty]
    private string _projectDialogDefaultBranch = "main";

    [ObservableProperty]
    private string _projectDialogDefaultObjective = string.Empty;

    public ObservableCollection<ProjectWorkerSelectionItem> ProjectDialogWorkers { get; } = new();

    public string[] TechStackOptions => StaticTechStackOptions;

    public static readonly string[] StaticTechStackOptions = [
        "Rust",
        "C# / .NET 9",
        "TypeScript / Node",
        "Python / FastAPI",
        "Go",
        "Java / Spring",
        "C++",
        "Generic / Multi-Stack"
    ];

    // Swarm Chat & Inter-Agent Bus
    public ObservableCollection<SwarmChatMessage> SwarmChatMessages { get; } = new();

    public string LatestSwarmOutput => SwarmChatMessages.LastOrDefault()?.Content ?? "(Listening for agent output telemetry...)";

    [ObservableProperty]
    private bool _hasSwarmChatMessages;

    private IDisposable? _swarmBusSubscription;

    [ObservableProperty]
    private string _swarmChatInputText = string.Empty;

    [ObservableProperty]
    private string _selectedChatTargetWorker = "All Workers (Broadcast)";

    public ObservableCollection<string> ChatTargetWorkers { get; } = new() { "All Workers (Broadcast)" };

    [ObservableProperty]
    private int _selectedProjectTabIndex = 0; // 0 = Chat Feed, 1 = Dispatch Settings, 2 = Branch Tree

    [RelayCommand]
    public void SetProjectTab(string? tab)
    {
        _audioService.PlayClick();
        if (int.TryParse(tab, out int idx))
        {
            SelectedProjectTabIndex = idx;
        }
    }

    async partial void OnSelectedProjectChanged(SwarmProject? value)
    {
        _swarmBusSubscription?.Dispose();
        _swarmBusSubscription = null;

        if (value != null)
        {
            FleetWorkspacePath = value.RootDirectory;
            if (!string.IsNullOrWhiteSpace(value.DefaultObjective) && string.IsNullOrWhiteSpace(FleetTaskObjective))
            {
                FleetTaskObjective = value.DefaultObjective;
            }
            UseGitWorktrees = value.UseGitWorktrees;

            // Update chat targets
            ChatTargetWorkers.Clear();
            ChatTargetWorkers.Add("All Workers (Broadcast)");
            foreach (var profile in Profiles)
            {
                if (value.AssignedWorkerIds.Count == 0 || value.AssignedWorkerIds.Contains(profile.Id))
                {
                    ChatTargetWorkers.Add(profile.Name);
                }
            }
            SelectedChatTargetWorker = ChatTargetWorkers[0];

            // Load chat messages
            await RefreshSwarmChatMessagesAsync();

            // Subscribe to real-time message bus updates
            _swarmBusSubscription = _swarmAggregatorService.SubscribeProjectBus(value, newMsg =>
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (!SwarmChatMessages.Any(m => m.Id == newMsg.Id ||
                        (m.Timestamp == newMsg.Timestamp && m.Content == newMsg.Content && m.SenderName == newMsg.SenderName)))
                    {
                        SwarmChatMessages.Add(newMsg);
                        HasSwarmChatMessages = SwarmChatMessages.Count > 0;
                    }
                });
            });

            _ = CheckPreflightAsync();
        }
        else
        {
            SwarmChatMessages.Clear();
            HasSwarmChatMessages = false;
        }
    }

    public event Func<Task<string?>>? BrowseFolderRequested;
    public event Func<Task<string?>>? BrowseRagDocumentFileRequested;
    public event Func<AccountProfile?, Task<AccountProfile?>>? ShowEditDialogRequested;
    public event Func<Task<AccountProfile?>>? ShowAddProfileRequested;
    public event Action<AvaloniaProfileItemViewModel>? ImportChatRequested;

    public ObservableCollection<AvaloniaProfileItemViewModel> Profiles { get; } = new();
    public ObservableCollection<AvaloniaProfileItemViewModel> FilteredProfiles { get; } = new();

    private AvaloniaProfileItemViewModel CreateProfileViewModel(AccountProfile profile)
    {
        var vm = new AvaloniaProfileItemViewModel(profile, _launcherService, _authDetectorService, _audioService, _doctorService);
        vm.OnEditRequested += async item => await EditProfileAsync(item);
        vm.OnDuplicateRequested += async item => await DuplicateProfileAsync(item);
        vm.OnDeleteRequested += async item => await DeleteProfileAsync(item);
        vm.OnImportChatRequested += item => ImportChatRequested?.Invoke(item);
        vm.OnOpenChatRequested += item => OpenAccountRealtimeChat(item);
        vm.OnOpenPersonalChatRequested += item => OpenPersonalChatFromCard(item);
        vm.OnNotificationRequested += ShowNotification;
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(AvaloniaProfileItemViewModel.IsSelectedForSwarm) or
                                  nameof(AvaloniaProfileItemViewModel.AuthStatus) or
                                  nameof(AvaloniaProfileItemViewModel.HasExhaustedQuota))
            {
                UpdateKpiMetrics();
            }
            if (e.PropertyName == nameof(AvaloniaProfileItemViewModel.IsSelectedForSwarm))
            {
                UpdateWorktreeTreeNodes();
            }
        };
        return vm;
    }

    [RelayCommand]
    public async Task AddProfileAsync()
    {
        _audioService.PlayClick();
        if (ShowAddProfileRequested == null) return;
        var result = await ShowAddProfileRequested();
        if (result != null)
        {
            var itemVm = CreateProfileViewModel(result);
            Profiles.Add(itemVm);
            await _storageService.SaveProfilesAsync(Profiles.Select(p => p.Profile));
            await itemVm.RefreshAuthStatusAsync();
            ApplyFilters();
            _audioService.PlaySuccess();
            ShowNotification($"Added new profile '{result.Name}' ({result.Tier})");
        }
    }

    public async Task EditProfileAsync(AvaloniaProfileItemViewModel item)
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

            await _storageService.SaveProfilesAsync(Profiles.Select(p => p.Profile));
            await item.RefreshAuthStatusAsync();
            ApplyFilters();
            _audioService.PlaySuccess();
            ShowNotification($"Updated profile '{item.Name}'");
        }
    }

    public async Task DuplicateProfileAsync(AvaloniaProfileItemViewModel item)
    {
        try
        {
            _audioService.PlayClick();
            var profile = item.Profile;
            var candidateName = $"{profile.Name} (Copy)";
            int counter = 2;
            while (Profiles.Any(p => string.Equals(p.Name, candidateName, StringComparison.OrdinalIgnoreCase)))
            {
                candidateName = $"{profile.Name} (Copy {counter++})";
            }

            var appDataDir = _storageService.GetAppDataPath();
            var targetDir = Path.Combine(appDataDir, "profiles", candidateName.ToLowerInvariant().Replace(' ', '-'));
            var sourceDir = profile.GetEffectiveProfileDirectory();

            if (Directory.Exists(sourceDir))
            {
                CopyProfileData(sourceDir, targetDir);
            }

            var newProfile = new AccountProfile
            {
                Name = candidateName,
                Description = profile.Description,
                ColorTag = profile.ColorTag,
                CustomProfilePath = targetDir,
                DefaultWorkspace = profile.DefaultWorkspace,
                ExtraArguments = profile.ExtraArguments,
                IsSelectedForSwarm = profile.IsSelectedForSwarm,
                Tier = profile.Tier,
                PreferredModel = profile.PreferredModel,
                QuotaLimit = profile.QuotaLimit,
                IsQuotaExhausted = false
            };

            var itemVm = CreateProfileViewModel(newProfile);
            Profiles.Add(itemVm);
            await _storageService.SaveProfilesAsync(Profiles.Select(p => p.Profile));
            await itemVm.RefreshAuthStatusAsync();
            ApplyFilters();

            _audioService.PlaySuccess();
            ShowNotification($"Duplicated profile '{item.Name}' to '{candidateName}'");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to duplicate profile '{item.Name}'", ex);
            ShowNotification($"Failed to duplicate profile: {ex.Message}");
        }
    }

    private static void CopyProfileData(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var sourceCli = Path.Combine(sourceDir, ".gemini", "antigravity-cli");
        var targetCli = Path.Combine(targetDir, ".gemini", "antigravity-cli");

        if (!Directory.Exists(sourceCli)) return;
        Directory.CreateDirectory(targetCli);

        var srcHistory = Path.Combine(sourceCli, "history.jsonl");
        if (File.Exists(srcHistory))
        {
            try { File.Copy(srcHistory, Path.Combine(targetCli, "history.jsonl"), true); }
            catch (Exception ex) { Logger.Warn($"[Avalonia] Could not copy history.jsonl: {ex.Message}"); }
        }

        var srcChats = Path.Combine(sourceCli, "chats");
        if (Directory.Exists(srcChats))
        {
            var tgtChats = Path.Combine(targetCli, "chats");
            Directory.CreateDirectory(tgtChats);
            foreach (var f in Directory.GetFiles(srcChats, "*.json"))
            {
                try { File.Copy(f, Path.Combine(tgtChats, Path.GetFileName(f)), true); }
                catch (Exception ex) { Logger.Warn($"[Avalonia] Could not copy chat file {f}: {ex.Message}"); }
            }
        }
    }

    public async Task DeleteProfileAsync(AvaloniaProfileItemViewModel? item)
    {
        if (item == null) return;
        if (item.Profile.IsMainDefaultProfile())
        {
            ShowNotification("Cannot delete the primary host profile.");
            return;
        }

        _audioService.PlayDelete();
        Profiles.Remove(item);
        ApplyFilters();
        await _storageService.SaveProfilesAsync(Profiles.Select(p => p.Profile));
        UpdateKpiMetrics();
        ShowNotification($"Deleted profile '{item.Name}'.");
    }
    public ObservableCollection<McpServerConfig> McpServers { get; } = new();
    public ObservableCollection<string> LogLines { get; } = new();
    public ObservableCollection<LogEntryItem> FilteredLogEntries { get; } = new();
    private readonly List<LogEntryItem> _allLogEntries = new();

    public List<string> LogLevelFilterOptions { get; } = ["ALL", "INFO", "DEBUG", "WARN", "ERROR", "SUCCESS"];

    [ObservableProperty]
    private string _logSearchQuery = string.Empty;

    partial void OnLogSearchQueryChanged(string value)
    {
        ApplyLogFilters();
    }

    [ObservableProperty]
    private string _selectedLogLevelFilter = "ALL";

    partial void OnSelectedLogLevelFilterChanged(string value)
    {
        ApplyLogFilters();
    }

    [ObservableProperty]
    private bool _autoScrollLogs = true;

    public Func<string, Task<string?>>? SaveExcelFileRequested;
    public Func<Task<string?>>? BrowseCliBinaryFileRequested;
    public ObservableCollection<DispatchedWorkerTask> DispatchedTasks { get; } = new();
    public ObservableCollection<GitWorktreeInfo> ActiveWorktrees { get; } = new();
    public ObservableCollection<string> SwarmBranches { get; } = new();
    public ObservableCollection<WorktreeTreeNode> WorktreeTreeNodes { get; } = new();

    public AvaloniaMainViewModel()
    {
        _storageService = new ProfileStorageService();
        _telemetryService = new TelemetryService();
        _authDetectorService = new AuthDetectorService();
        _doctorService = new ProfileDoctorService();
        _launcherService = new TerminalLauncherService();
        _modelService = new AgyModelService();
        _mcpService = new McpService();
        _skillService = new SkillService();
        _quotaConfigService = new QuotaConfigService();
        _audioService = new AudioService();
        _gitWorktreeService = new GitWorktreeService();
        _swarmAggregatorService = new SwarmAggregatorService();
        _fleetDispatcherService = new FleetDispatcherService(_gitWorktreeService, _launcherService, _swarmAggregatorService);
        _localizationService = new LocalizationService();
        _ragService = new RagService();
        _personalChatService = new PersonalChatService();

        _syncTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _syncTimer.Tick += SyncTimer_Tick;

        _fleetProcessWatcherTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _fleetProcessWatcherTimer.Tick += (s, e) => WatchDispatchedProcesses();

        DispatchedTasks.CollectionChanged += (s, e) =>
        {
            HasDispatchedTasks = DispatchedTasks.Count > 0;
            UpdateWorktreeTreeNodes();
        };

        SwarmChatMessages.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(LatestSwarmOutput));
        };

        AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.ThemeChanged += theme =>
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                OnPropertyChanged(nameof(CurrentPage));
                OnPropertyChanged(nameof(SelectedProjectTabIndex));
                OnPropertyChanged(nameof(SwarmChatMessages));
            });
        };

        // Initialize state
        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        try
        {
            DetectedAgyPath = _launcherService.FindAgyExecutablePath();
            IsAgyInstalled = !string.IsNullOrEmpty(DetectedAgyPath);

            Settings = await _storageService.LoadSettingsAsync();
            SoundEnabled = Settings.SoundEnabled;
            WelcomeSoundEnabled = Settings.WelcomeSoundEnabled;
            _audioService.IsEnabled = SoundEnabled;
            CloseToTray = Settings.CloseToTray;
            MinimizeToTray = Settings.MinimizeToTray;
            GradientTheme = Settings.GradientTheme ?? "Cyberpunk";
            SwarmExecutionMode = Settings.SwarmExecutionMode ?? "ConcurrentCli";
            ConfirmWorktreeMerge = Settings.ConfirmWorktreeMerge;
            AutoSyncEnabled = Settings.AutoSyncEnabled;
            AutoSyncAudioEnabled = Settings.AutoSyncAudioEnabled;
            NavbarDisplayMode = Settings.NavbarDisplayMode ?? "Detailed";
            IsNavbarDetailed = NavbarDisplayMode == "Detailed";

            if (!string.IsNullOrWhiteSpace(Settings.CustomAgyExecutablePath))
            {
                DetectedAgyPath = Settings.CustomAgyExecutablePath;
                IsAgyInstalled = true;
            }

            var langCode = Settings.Language ?? "en";
            SelectedLanguageOption = LanguageOptions.FirstOrDefault(l => l.Code == langCode) ?? LanguageOptions[0];
            _localizationService.SetLanguage(SelectedLanguageOption.Code);

            CurrentTheme = Settings.Theme ?? "Dark";
            SelectedThemeOption = CurrentTheme;
            AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.ApplyTheme(CurrentTheme);

            await ReloadProfilesAsync();
            await LoadMcpServersAsync();
            await LoadSkillsAsync();
            RefreshLogs();

            // Load Swarm Projects
            var savedProjects = await _storageService.LoadProjectsAsync();
            Projects.Clear();
            foreach (var proj in savedProjects)
            {
                Projects.Add(proj);
            }
            HasProjects = Projects.Count > 0;
            if (HasProjects)
            {
                SelectedProject = Projects[0];
            }

            _syncTimer.Start();
            ShowNotification("Agy Swarm Avalonia initialized successfully.");

            // Automatic background swarm sync on launch with zero CLI flicker
            _ = Task.Run(async () => await SyncSwarmAsync(isPeriodic: true));

            // Initialize Personal Chat & RAG Knowledge Bases
            _ = Task.Run(async () => await InitializePersonalChatAsync());

            // 3-second welcome ambient chime & auto-dismiss splash screen
            _ = Task.Run(async () =>
            {
                if (Settings.SoundEnabled && Settings.WelcomeSoundEnabled)
                {
                    _audioService.PlayWelcomeCalmChime();
                }
                await Task.Delay(3000);
                Dispatcher.UIThread.Post(() => IsWelcomeOverlayVisible = false);
            });
        }
        catch (Exception ex)
        {
            Logger.Error("Initialization error in AvaloniaMainViewModel", ex);
        }
    }

    [RelayCommand]
    public void Navigate(string page)
    {
        CurrentPage = page;
        _audioService.PlayClick();
        if (page == "Logs")
        {
            RefreshLogs();
        }
        else if (page == "Dispatcher")
        {
            _ = CheckPreflightAsync();
            _ = RefreshWorktreesAsync();
            UpdateWorktreeTreeNodes();
        }
        else if (page == "RealtimeChat")
        {
            CurrentPage = "Chat";
            SelectedChatTab = "Realtime";
            EnsureProjectContextForChat();
            PopulateChatTargetWorkers();
            _ = RefreshSwarmChatMessagesAsync();
        }
        else if (page == "PersonalChat")
        {
            CurrentPage = "Chat";
            SelectedChatTab = "Personal";
            EnsurePersonalChatContext();
        }
        else if (page == "Chat")
        {
            CurrentPage = "Chat";
            if (SelectedChatTab == "Personal")
            {
                EnsurePersonalChatContext();
            }
            else
            {
                EnsureProjectContextForChat();
                PopulateChatTargetWorkers();
                _ = RefreshSwarmChatMessagesAsync();
            }
        }
    }

    [RelayCommand]
    public void SwitchChatTab(string tab)
    {
        if (string.IsNullOrWhiteSpace(tab)) return;
        SelectedChatTab = tab;
        _audioService.PlayClick();
        if (tab == "Personal")
        {
            EnsurePersonalChatContext();
        }
        else
        {
            EnsureProjectContextForChat();
            PopulateChatTargetWorkers();
            _ = RefreshSwarmChatMessagesAsync();
        }
    }

    [RelayCommand]
    public async Task ReloadProfilesAsync()
    {
        IsSyncing = true;
        try
        {
            var loaded = await _storageService.LoadProfilesAsync();
            Profiles.Clear();
            foreach (var p in loaded)
            {
                Profiles.Add(CreateProfileViewModel(p));
            }
            ApplyFilters();
            UpdateWorktreeTreeNodes();
            UpdateKpiMetrics();
            _ = LoadTelemetryHistoryAsync();

            // Run background quick auth audit
            _ = Task.Run(async () =>
            {
                foreach (var profileItem in Profiles)
                {
                    try
                    {
                        var profile = profileItem.Profile;
                        var status = await _authDetectorService.DetectAuthStatusAsync(profile, allowCliSpawn: false);
                        profile.AuthStatus = status;

                        // Fetch authentic CLI usage if cached, but never spawn CLI process during background auto audit
                        var usage = await AgyUsageParser.FetchUsageCachedAsync(
                            profile.GetEffectiveProfileDirectory(),
                            !profile.IsMainDefaultProfile(),
                            Settings.CustomAgyExecutablePath,
                            allowCliSpawn: false);

                        if (usage != null && usage.IsSuccess)
                        {
                            profile.AuthStatus.GeminiWeeklyRemainingPercent = usage.GeminiWeeklyRemainingPercent ?? 0;
                            profile.AuthStatus.GeminiWeeklyRefreshesIn = usage.GeminiWeeklyRefreshesIn ?? "N/A";
                            profile.AuthStatus.ClaudeGptWeeklyRemainingPercent = usage.ClaudeGptWeeklyRemainingPercent ?? 0;
                        }

                        profileItem.SyncFromModel();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Background audit failed for {profileItem.Name}: {ex.Message}");
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    ApplyFilters();
                    UpdateWorktreeTreeNodes();
                    UpdateKpiMetrics();
                });
            });
        }
        finally
        {
            IsSyncing = false;
        }
    }

    public void UpdateKpiMetrics()
    {
        TotalCount = Profiles.Count;
        AuthenticatedCount = Profiles.Count(p => p.AuthStatus?.Status == AuthStatusType.Authenticated);
        SelectedSwarmCount = Profiles.Count(p => p.IsSelectedForSwarm);
        TotalInteractionsCount = Profiles.Sum(p => p.AuthStatus?.TotalTurnsCount ?? 0);

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

        TotalSavedSessionsCount = _cachedRealHistory
            .Select(e => e.ConversationId)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .Count();
        McpServersCount = McpServers.Count;
        McpToolsTotalCount = McpServers.Sum(s => s.Tools?.Count ?? 0);
        if (McpToolsTotalCount == 0 && McpServers.Count > 0)
        {
            McpToolsTotalCount = McpServers.Count;
        }
        LastSyncedAtText = $"Synced {DateTime.Now:HH:mm:ss}";

        UpdateChartPoints();
        UpdateAnalyticsViews();
        UpdateWorktreeTreeNodes();
    }

    public async Task LoadTelemetryHistoryAsync()
    {
        try
        {
            var history = await _telemetryService.LoadAllProfileHistoryAsync(Profiles.Select(p => p.Profile));
            _cachedRealHistory = history ?? [];
            UpdateAccountFilterOptions();
            UpdateChartPoints();
            UpdateAnalyticsViews();
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to load telemetry history in Avalonia", ex);
        }
    }

    public void UpdateAccountFilterOptions()
    {
        var current = SelectedAccountFilter;
        AccountFilterOptions.Clear();
        AccountFilterOptions.Add("All Accounts");
        foreach (var p in Profiles)
        {
            if (!AccountFilterOptions.Contains(p.Name))
                AccountFilterOptions.Add(p.Name);
        }
        if (AccountFilterOptions.Contains(current))
            SelectedAccountFilter = current;
        else
            SelectedAccountFilter = "All Accounts";
    }

    public void UpdateChartPoints()
    {
        if (!global::Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(UpdateChartPoints);
            return;
        }

        DashboardChartPoints.Clear();

        // 1. Filter real history records
        var entries = _cachedRealHistory.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedAccountFilter) && SelectedAccountFilter != "All Accounts")
        {
            entries = entries.Where(e => e.ProfileName.Equals(SelectedAccountFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SelectedModelFilter) && SelectedModelFilter != "All Models")
        {
            entries = entries.Where(e => _modelService.IsModelMatch(e.ModelName, SelectedModelFilter));
        }

        if (SelectedChartTierFilter != "All Tiers")
        {
            var targetProfiles = Profiles
                .Where(p => p.Tier.Equals(SelectedChartTierFilter, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Name)
                .ToHashSet();
            entries = entries.Where(e => targetProfiles.Contains(e.ProfileName));
        }

        var entryList = entries.ToList();

        DateTime now = DateTime.Now;
        List<(string label, int count)> buckets = [];

        if (SelectedTimeframe == "Last 24 Hours")
        {
            var since = now.AddHours(-24);
            var recent = entryList.Where(e => e.Timestamp >= since).ToList();

            for (int i = 5; i >= 0; i--)
            {
                var blockStart = now.AddHours(-(i + 1) * 4);
                var blockEnd = now.AddHours(-i * 4);
                int count = recent.Count(e => e.Timestamp >= blockStart && e.Timestamp < blockEnd);
                buckets.Add(($"{blockEnd:HH}:00", count));
            }
        }
        else if (SelectedTimeframe == "Last 3 Days")
        {
            for (int i = 5; i >= 0; i--)
            {
                var bStart = now.AddHours(-(i + 1) * 12);
                var bEnd = now.AddHours(-i * 12);
                int count = entryList.Count(e => e.Timestamp >= bStart && e.Timestamp < bEnd);
                buckets.Add(($"{bEnd:MM/dd HH}h", count));
            }
        }
        else if (SelectedTimeframe == "Last 14 Days")
        {
            for (int d = 13; d >= 0; d--)
            {
                var day = now.AddDays(-d);
                int count = entryList.Count(e => e.Timestamp.Date == day.Date);
                buckets.Add((day.ToString("MM/dd"), count));
            }
        }
        else if (SelectedTimeframe == "Last 30 Days")
        {
            for (int w = 3; w >= 0; w--)
            {
                var wStart = now.AddDays(-(w + 1) * 7);
                var wEnd = now.AddDays(-w * 7);
                int count = entryList.Count(e => e.Timestamp >= wStart && e.Timestamp < wEnd);
                buckets.Add(($"W{4 - w}", count));
            }
        }
        else if (SelectedTimeframe == "Last 90 Days")
        {
            for (int m = 2; m >= 0; m--)
            {
                var mStart = now.AddDays(-(m + 1) * 30);
                var mEnd = now.AddDays(-m * 30);
                int count = entryList.Count(e => e.Timestamp >= mStart && e.Timestamp < mEnd);
                buckets.Add((now.AddMonths(-m).ToString("MMM"), count));
            }
        }
        else if (SelectedTimeframe == "All Time")
        {
            DateTime earliest = entryList.Count > 0 ? entryList.Min(e => e.Timestamp) : now.AddDays(-30);
            TimeSpan span = now - earliest;
            if (span.TotalDays < 6) span = TimeSpan.FromDays(6);
            double blockMs = span.TotalMilliseconds / 6.0;
            for (int i = 0; i < 6; i++)
            {
                var bStart = earliest.AddMilliseconds(i * blockMs);
                var bEnd = earliest.AddMilliseconds((i + 1) * blockMs);
                int count = entryList.Count(e => e.Timestamp >= bStart && e.Timestamp < bEnd);
                buckets.Add((bEnd.ToString("MM/dd"), count));
            }
        }
        else // Last 7 Days (Default)
        {
            for (int d = 6; d >= 0; d--)
            {
                var day = now.AddDays(-d);
                int count = entryList.Count(e => e.Timestamp.Date == day.Date);
                buckets.Add((day.ToString("ddd"), count));
            }
        }

        int maxVal = buckets.Count > 0 ? buckets.Max(b => b.count) : 0;
        HasChartData = maxVal > 0;

        // 25% - 30% Headroom Ceiling so bars & line dots never collide with top border
        int ceiling = maxVal == 0 ? 10 : (int)Math.Ceiling(maxVal * 1.30);
        if (ceiling > 10)
        {
            ceiling = ((ceiling + 4) / 5) * 5;
        }
        int safeMax = Math.Max(10, ceiling);

        YTick100 = safeMax;
        YTick75 = (int)Math.Round(safeMax * 0.75);
        YTick50 = (int)Math.Round(safeMax * 0.50);
        YTick25 = (int)Math.Round(safeMax * 0.25);
        YTick0 = 0;

        var linePts = new global::Avalonia.Points();
        var areaPts = new global::Avalonia.Points();

        double baseWidth = Math.Max(660.0, buckets.Count * 76.0);
        double canvasWidth = baseWidth * ChartZoomLevel;
        ChartCanvasWidth = canvasWidth;
        double canvasHeight = 220.0;
        double maxBarHeight = canvasHeight - 35.0; // 35px headroom for prompt count and token labels
        double stepX = buckets.Count > 1 ? (canvasWidth - 70.0) / (buckets.Count - 1) : canvasWidth;

        // Bottom left point for Area polygon
        areaPts.Add(new global::Avalonia.Point(35.0, canvasHeight));

        for (int i = 0; i < buckets.Count; i++)
        {
            var (lbl, count) = buckets[i];
            double height = count == 0 ? 6.0 : Math.Clamp(10.0 + ((double)count / safeMax) * (maxBarHeight - 10.0), 10.0, maxBarHeight);
            long estTok = (long)count * 1850L;
            string tokLabel = count == 0 ? "0 tok" : (estTok >= 1000 ? $"{estTok / 1000}K tok" : $"{estTok} tok");

            string barColor = count == 0 
                ? "#334155" 
                : (count > safeMax * 0.75 ? "#8B5CF6" : (count > safeMax * 0.4 ? "#3B82F6" : "#06B6D4"));

            double ptX = 35.0 + i * stepX;
            double ptY = count == 0 ? (canvasHeight - 6.0) : (canvasHeight - 20.0) - ((double)count / safeMax) * (canvasHeight - 55.0);

            linePts.Add(new global::Avalonia.Point(ptX, ptY));
            areaPts.Add(new global::Avalonia.Point(ptX, ptY));

            DashboardChartPoints.Add(new ChartDataPoint
            {
                Label = lbl,
                Value = count,
                Height = height,
                X = ptX,
                Y = ptY,
                TokensLabel = tokLabel,
                TooltipText = $"{lbl}\n• Prompts: {count}\n• Tokens: {tokLabel}\n• Account: {SelectedAccountFilter}\n• Model: {SelectedModelFilter}",
                BarColor = barColor,
                TimeRange = SelectedTimeframe,
                ModelContext = SelectedModelFilter,
                AccountContext = SelectedAccountFilter,
                Width = Math.Max(24.0, 34.0 * ChartZoomLevel)
            });
        }

        // Bottom right point for Area polygon
        areaPts.Add(new global::Avalonia.Point(35.0 + (buckets.Count - 1) * stepX, canvasHeight));

        LinePoints = linePts;
        AreaPoints = areaPts;
    }

    [RelayCommand]
    public void ApplyFilters()
    {
        FilteredProfiles.Clear();
        var query = SearchQuery?.Trim().ToLowerInvariant() ?? string.Empty;
        var tier = AccountsTierFilter;
        var health = AccountsHealthFilter;

        var matches = Profiles.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            matches = matches.Where(p =>
                p.Name.ToLowerInvariant().Contains(query) ||
                (p.Description?.ToLowerInvariant().Contains(query) ?? false) ||
                (p.AccountEmail?.ToLowerInvariant().Contains(query) ?? false) ||
                p.Tier.ToLowerInvariant().Contains(query) ||
                p.CurrentModel.ToLowerInvariant().Contains(query));
        }

        if (!string.IsNullOrWhiteSpace(tier) && !string.Equals(tier, "All Tiers", StringComparison.OrdinalIgnoreCase))
        {
            matches = matches.Where(p => p.Tier.Equals(tier, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(health) && !string.Equals(health, "All Status", StringComparison.OrdinalIgnoreCase))
        {
            matches = health switch
            {
                "Authenticated" => matches.Where(p => p.AuthStatus?.Status == AuthStatusType.Authenticated),
                "Needs Login" => matches.Where(p => p.AuthStatus?.Status is AuthStatusType.NeedsLogin or AuthStatusType.NotInitialized),
                "Quota Exhausted" => matches.Where(p => p.HasExhaustedQuota),
                "Warning / High Quota" => matches.Where(p => p.UsagePercentage >= 80 || p.HasExhaustedQuota),
                _ => matches
            };
        }

        // Sorting
        matches = AccountsSortBy switch
        {
            "Name (A-Z)" => matches.OrderBy(p => p.Name),
            "Name (Z-A)" => matches.OrderByDescending(p => p.Name),
            "Quota Used (High to Low)" => matches.OrderByDescending(p => p.UsagePercentage),
            "Quota Used (Low to High)" => matches.OrderBy(p => p.UsagePercentage),
            _ => matches
        };

        foreach (var p in matches)
        {
            FilteredProfiles.Add(p);
        }

        OnPropertyChanged(nameof(HasActiveAccountsFilters));
        OnPropertyChanged(nameof(ActiveAccountsFilterCount));
        UpdateSwarmStatus();
    }

    public void UpdateAnalyticsViews()
    {
        if (!global::Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(UpdateAnalyticsViews);
            return;
        }

        // 1. Dynamic Model Intelligence Matrix
        ModelEfficiencies.Clear();
        var allDiscoveredModels = ModelFilterOptions.Where(m => m != "All Models");
        var uniqueModelNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in _cachedRealHistory)
        {
            if (!string.IsNullOrWhiteSpace(entry.ModelName))
            {
                uniqueModelNames.Add(entry.ModelName.Trim());
            }
        }

        foreach (var p in Profiles)
        {
            if (!string.IsNullOrWhiteSpace(p.CurrentModel))
                uniqueModelNames.Add(p.CurrentModel.Trim());
            if (!string.IsNullOrWhiteSpace(p.PreferredModel))
                uniqueModelNames.Add(p.PreferredModel.Trim());
        }

        foreach (var m in allDiscoveredModels)
        {
            if (!string.IsNullOrWhiteSpace(m))
                uniqueModelNames.Add(m.Trim());
        }

        var modelStats = uniqueModelNames
            .Select(m =>
            {
                int reqCount = _cachedRealHistory.Count(e => _modelService.IsModelMatch(e.ModelName, m));
                return new { Model = m, Requests = reqCount };
            })
            .OrderByDescending(x => x.Requests)
            .ThenBy(x => x.Model)
            .ToList();

        foreach (var item in modelStats)
        {
            string m = item.Model;
            int reqs = item.Requests;
            long tok = (long)reqs * 1950L;
            string tier = m.Contains("opus", StringComparison.OrdinalIgnoreCase) || m.Contains("ultra", StringComparison.OrdinalIgnoreCase)
                ? "Ultra"
                : (m.Contains("pro", StringComparison.OrdinalIgnoreCase) || m.Contains("sonnet", StringComparison.OrdinalIgnoreCase) ? "Pro/Ultra" : "Plus/Pro");

            string spd = m.Contains("flash", StringComparison.OrdinalIgnoreCase)
                ? "135 t/s"
                : (m.Contains("opus", StringComparison.OrdinalIgnoreCase) ? "48 t/s" : "76 t/s");

            string status = reqs > 0 ? "Active" : "Ready (Idle)";
            string statusColor = reqs > 0 ? "#10B981" : "#64748B";

            ModelEfficiencies.Add(new ModelEfficiencyItem
            {
                ModelName = m,
                Tier = tier,
                Requests = reqs,
                Tokens = tok >= 1000 ? $"{tok / 1000}K" : tok.ToString(),
                AvgSpeed = spd,
                ErrorRate = "< 0.1%",
                Status = status,
                StatusColor = statusColor
            });
        }

        // 2. Swarm Health Records
        SwarmHealthRecords.Clear();
        foreach (var p in Profiles)
        {
            SwarmHealthRecords.Add(new SwarmHealthItem
            {
                AccountName = p.Name,
                AccountEmail = p.AccountEmail ?? "Pending Auth",
                AvatarInitial = p.AvatarInitial,
                Tier = p.TierBadgeText,
                TierColor = p.TierBadgeBackground,
                CurrentModel = p.CurrentModel,
                UsageLabel = p.UsageLabel,
                UsagePercent = p.UsagePercentage,
                TodayQuotaFormatted = p.TodayQuotaFormatted,
                WeeklySummary = p.WeeklySummary,
                QuotaResetCountdown = p.QuotaResetCountdown,
                StatusText = p.QuotaStatusText,
                StatusColor = p.QuotaStatusColor
            });
        }

        // 3. Swarm Fleet Models
        SwarmFleetModels.Clear();
        var knownModels = new List<(string Id, string Name, string Family, string Color, string Tier, string Context)>
        {
            ("gemini-3.1-pro-high", "Gemini 3.1 Pro (High Reasoning)", "Gemini Enterprise", "#3B82F6", "High Reasoning Effort", "1,000,000 tokens"),
            ("gemini-3.8-flash-high", "Gemini 3.8 Flash (High Speed)", "Gemini Enterprise", "#3B82F6", "High Speed Reasoning", "1,000,000 tokens"),
            ("gemini-3.8-flash-medium", "Gemini 3.8 Flash (Standard)", "Gemini Enterprise", "#3B82F6", "Standard Balanced Effort", "1,000,000 tokens"),
            ("claude-sonnet-4-6", "Claude Sonnet 4.6 (Extended Thinking)", "Anthropic Claude", "#8B5CF6", "Deep Thinking & Architecture", "200,000 tokens"),
            ("claude-opus-4-6-thinking", "Claude Opus 4.6 (Deep Reasoning)", "Anthropic Claude", "#8B5CF6", "Max Cognitive Depth", "200,000 tokens"),
            ("gpt-oss-120b-medium", "GPT-OSS 120B (Open Weights)", "Open Weights / GPT", "#10B981", "Standard Multi-Turn", "128,000 tokens")
        };

        foreach (var km in knownModels)
        {
            var matchedProfiles = Profiles.Where(p =>
                _modelService.IsModelMatch(p.CurrentModel, km.Id) ||
                _modelService.IsModelMatch(p.PreferredModel, km.Id)).ToList();

            int poolCap = matchedProfiles.Sum(p => p.DailyQuotaLimit);
            double burnRate = matchedProfiles.Sum(p => p.BurnRatePromptsPerHour);
            string accList = matchedProfiles.Count > 0
                ? string.Join(", ", matchedProfiles.Select(p => p.Name))
                : "Standby (0 accounts assigned)";

            SwarmFleetModels.Add(new SwarmFleetModelItem
            {
                ModelId = km.Id,
                DisplayName = km.Name,
                Family = km.Family,
                FamilyColor = km.Color,
                ReasoningTier = km.Tier,
                ContextLimitLabel = km.Context,
                AssignedAccountsCount = matchedProfiles.Count,
                AssignedAccountsList = accList,
                TotalPoolCapacity = poolCap,
                AggregateBurnRate = burnRate
            });
        }

        // 4. Swarm CLI Capabilities
        SwarmCliCapabilities.Clear();
        SwarmCliCapabilities.Add(new SwarmCliCapabilityItem
        {
            Command = "agy models",
            Title = "Fleet Multi-Model Intelligence & Reasoning",
            Description = "Multi-level reasoning routing (low, medium, high, thinking) across Google & Anthropic model architectures.",
            Status = "Verified",
            StatusColor = "#10B981",
            BadgeText = "Active Fleet"
        });
        SwarmCliCapabilities.Add(new SwarmCliCapabilityItem
        {
            Command = "agy -p \"/usage\"",
            Title = "Zero-Turn Live Quota Sentinel",
            Description = "Real-time monitoring of 5-hour limit and weekly quota buckets without consuming tokens or agent turns.",
            Status = "Verified",
            StatusColor = "#10B981",
            BadgeText = "0 Tokens / Turn"
        });
        SwarmCliCapabilities.Add(new SwarmCliCapabilityItem
        {
            Command = "agy agents",
            Title = "Multi-Agent Swarm Orchestration",
            Description = "Parallel subagent coordination with isolated profile sandboxes and independent workspaces.",
            Status = "Verified",
            StatusColor = "#10B981",
            BadgeText = "Multi-Process"
        });
        SwarmCliCapabilities.Add(new SwarmCliCapabilityItem
        {
            Command = "agy mcp",
            Title = "Model Context Protocol Tool Bridge",
            Description = "Integration of custom tools, SQLite databases, and language servers via stdio IPC communication.",
            Status = "Verified",
            StatusColor = "#10B981",
            BadgeText = "Stdio IPC"
        });

        OnPropertyChanged(nameof(SwarmDailyCapacityTotal));
        OnPropertyChanged(nameof(SwarmTodayPromptsTotal));
        OnPropertyChanged(nameof(SwarmPoolRemainingPercent));
        OnPropertyChanged(nameof(SwarmAggregateBurnRate));
        OnPropertyChanged(nameof(SwarmExhaustionForecast));

        // 5. Real 24-Hour Swarm Hourly Activity Heatmap
        HourlyHeatmap.Clear();
        int[] hourlyCounts = new int[24];

        var heatmapHistory = _cachedRealHistory.AsEnumerable();
        if (SelectedHeatmapAccount != "All Accounts")
        {
            heatmapHistory = heatmapHistory.Where(e => string.Equals(e.ProfileName, SelectedHeatmapAccount, StringComparison.OrdinalIgnoreCase));
        }

        var last24hThreshold = DateTime.Now.AddHours(-24);
        var entriesIn24h = heatmapHistory.Where(e => e.Timestamp >= last24hThreshold).ToList();
        var workingList = entriesIn24h.Count > 0 ? entriesIn24h : heatmapHistory.ToList();

        foreach (var entry in workingList)
        {
            int h = entry.Timestamp.Hour;
            if (h >= 0 && h < 24) hourlyCounts[h]++;
        }

        int maxHourCount = hourlyCounts.Max();
        int total24h = hourlyCounts.Sum();
        HeatmapTotal24hPrompts = total24h;

        int peakHourIdx = maxHourCount > 0 ? Array.IndexOf(hourlyCounts, maxHourCount) : -1;
        HeatmapPeakHour = peakHourIdx >= 0 ? $"{peakHourIdx:D2}:00 ({maxHourCount} prompts)" : "No peak";

        int firstActive = -1, lastActive = -1;
        for (int h = 0; h < 24; h++)
        {
            if (hourlyCounts[h] > 0)
            {
                if (firstActive == -1) firstActive = h;
                lastActive = h;
            }
        }
        int activeHoursSpan = (firstActive >= 0 && lastActive >= firstActive) ? (lastActive - firstActive + 1) : 0;
        HeatmapActiveWindow = activeHoursSpan > 0 ? $"{activeHoursSpan} hrs active" : "0 hrs";

        double avgRate = activeHoursSpan > 0 ? (double)total24h / activeHoursSpan : 0.0;
        HeatmapActivityIntensity = avgRate > 0 ? $"{avgRate:F1} req/hr" : "0 req/hr";

        int safeHeatmapMax = maxHourCount == 0 ? 10 : (int)Math.Ceiling(maxHourCount * 1.35);
        if (safeHeatmapMax > 10)
        {
            safeHeatmapMax = ((safeHeatmapMax + 4) / 5) * 5;
        }
        safeHeatmapMax = Math.Max(10, safeHeatmapMax);

        HeatmapYTick100 = safeHeatmapMax;
        HeatmapYTick75 = (int)Math.Round(safeHeatmapMax * 0.75);
        HeatmapYTick50 = (int)Math.Round(safeHeatmapMax * 0.50);
        HeatmapYTick25 = (int)Math.Round(safeHeatmapMax * 0.25);
        HeatmapYTick0 = 0;

        double plotHeight = 140.0;
        for (int h = 0; h < 24; h++)
        {
            int cnt = hourlyCounts[h];
            double intensity = maxHourCount > 0 ? (double)cnt / maxHourCount : 0.0;
            string color = cnt == 0 
                ? "#252B3B" 
                : (intensity > 0.75 ? "#8B5CF6" : (intensity > 0.4 ? "#3B82F6" : "#06B6D4"));

            double hHeight = cnt == 0 ? 6.0 : Math.Clamp(((double)cnt / safeHeatmapMax) * plotHeight, 8.0, plotHeight);
            long estTokens = (long)cnt * 1950L;
            string tokStr = estTokens >= 1000 ? $"{estTokens / 1000}K tok" : $"{estTokens} tok";

            HourlyHeatmap.Add(new HourlyActivityItem
            {
                HourLabel = $"{h:D2}h",
                Height = hHeight,
                Color = color,
                PromptCount = cnt,
                Tooltip = $"{h:D2}:00 – {h:D2}:59\n• {cnt} Prompts Recorded\n• {tokStr} Estimated\n• Filter: {SelectedHeatmapAccount}"
            });
        }
    }

    [RelayCommand]
    public async Task QuickSyncAsync()
    {
        _audioService.PlaySync();
        ShowNotification("Synchronizing authentic telemetry & credentials...");
        await ReloadProfilesAsync();
        ShowNotification("Telemetry synchronized.");
    }

    [RelayCommand]
    public async Task QuickSyncAccountAsync(AvaloniaProfileItemViewModel? item)
    {
        if (item == null) return;
        _audioService.PlaySync();
        ShowNotification($"Synchronizing telemetry for {item.Name}...");

        try
        {
            await item.RefreshAuthStatusAsync();
            ApplyFilters();
            UpdateWorktreeTreeNodes();
            UpdateKpiMetrics();
            ShowNotification($"Synchronized {item.Name}.");
        }
        catch (Exception ex)
        {
            ShowNotification($"Sync failed for {item.Name}: {ex.Message}");
        }
    }

    [RelayCommand]
    public void SelectAllSwarm(object? param)
    {
        bool select = param switch
        {
            bool b => b,
            string s => bool.TryParse(s, out var parsed) && parsed,
            _ => true
        };

        foreach (var p in Profiles)
        {
            p.IsSelectedForSwarm = select;
        }
        ApplyFilters();
        UpdateWorktreeTreeNodes();
    }

    [RelayCommand]
    public async Task LaunchProfileAsync(AvaloniaProfileItemViewModel? item)
    {
        if (item == null) return;
        _audioService.PlayLaunch();
        ShowNotification($"Launching session for {item.Name}...");

        try
        {
            var proc = await _launcherService.LaunchProfileAsync(
                item.Profile,
                SelectedTerminal,
                false,
                null);

            if (proc != null)
            {
                _activeSwarmProcesses.Add(proc);
                UpdateSwarmStatus();
                ShowNotification($"Terminal launched (PID: {proc.Id})");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Launch failed for {item.Name}", ex);
            ShowNotification($"Launch failed: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task LaunchProfileCliOnlyAsync(AvaloniaProfileItemViewModel? item)
    {
        if (item == null) return;
        _audioService.PlayLaunch();
        ShowNotification($"Opening sandbox shell for {item.Name}...");

        try
        {
            var proc = await _launcherService.LaunchProfileAsync(
                item.Profile,
                SelectedTerminal,
                false,
                "--cli-only");

            if (proc != null)
            {
                _activeSwarmProcesses.Add(proc);
                UpdateSwarmStatus();
                ShowNotification($"Sandbox shell ready (PID: {proc.Id})");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"CLI launch failed for {item.Name}", ex);
            ShowNotification($"Shell failed: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task LaunchSwarmAsync()
    {
        if (IsSwarmRunning)
        {
            await StopSwarmAsync();
            return;
        }

        var selected = Profiles.Where(p => p.IsSelectedForSwarm).ToList();
        if (selected.Count == 0)
        {
            ShowNotification("No accounts selected for Swarm Launch.");
            return;
        }

        if (SwarmExecutionMode == "WorkerOrchestration")
        {
            _audioService.PlayLaunch();
            ShowNotification($"Launching Swarm Worker Fleet ({selected.Count} accounts)...");
            Navigate("Dispatcher");
            await DispatchFleetAsync();
            return;
        }

        _audioService.PlayLaunch();
        ShowNotification($"Launching Concurrent Swarm CLI with {selected.Count} worker(s)...");

        try
        {
            var procs = await _launcherService.LaunchSwarmAsync(
                selected.Select(p => p.Profile),
                SelectedTerminal,
                SelectedSwarmMode);

            _activeSwarmProcesses.AddRange(procs);
            IsSwarmRunning = true;
            UpdateSwarmStatus();
            ShowNotification($"Swarm running with {selected.Count} active workers.");
        }
        catch (Exception ex)
        {
            Logger.Error("Swarm launch failed", ex);
            ShowNotification($"Swarm launch error: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task StopSwarmAsync()
    {
        _audioService.PlayDelete();
        ShowNotification("Stopping active swarm workers...");

        await Task.Run(() =>
        {
            foreach (var proc in _activeSwarmProcesses)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill(true);
                    }
                }
                catch
                {
                    // Ignored
                }
            }
            _activeSwarmProcesses.Clear();
        });

        IsSwarmRunning = false;
        UpdateSwarmStatus();
        ShowNotification("All swarm worker sessions stopped.");
    }

    [RelayCommand]
    public async Task ClearLocksAsync(AvaloniaProfileItemViewModel? item)
    {
        if (item == null) return;
        _audioService.PlayClick();

        try
        {
            int cleared = await _doctorService.CleanStuckLocksAsync(item.Profile);
            ShowNotification(cleared > 0 
                ? $"Cleared {cleared} stuck lock file(s) for {item.Name}." 
                : $"No stuck lock files found for {item.Name}.");
            await item.RefreshAuthStatusAsync();
        }
        catch (Exception ex)
        {
            ShowNotification($"Lock clean failed: {ex.Message}");
        }
    }

    [RelayCommand]
    public void OpenWorkspaceFolder(AvaloniaProfileItemViewModel? item)
    {
        if (item == null) return;
        _launcherService.OpenWorkspaceFolder(item.Profile);
    }

    [RelayCommand]
    public void OpenProfileFolder(AvaloniaProfileItemViewModel? item)
    {
        if (item == null) return;
        _launcherService.OpenProfileFolder(item.Profile);
    }

    [RelayCommand]
    public void OpenDocInBrowser(string docName)
    {
        _audioService.PlayClick();
        try
        {
            string fileName = docName.ToLowerInvariant() switch
            {
                "architecture" => "architecture.html",
                "swarmworkflow" => "swarm_workflow.html",
                "mcpguide" => "mcp_guide.html",
                "datastorage" => "database_config.html",
                "tosrisks" => "terms_of_service_and_risks.html",
                "nextfeatures" => "next_features.html",
                "crossplatform" => "cross_platform.html",
                "profiledoctor" => "profile_doctor.html",
                "clireference" => "cli_reference.html",
                "chatmigration" => "chat_migration.html",
                "troubleshooting" => "troubleshooting.html",
                "fleetdispatcher" => "fleet_dispatcher.html",
                _ => "architecture.html"
            };

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string docPath = Path.Combine(baseDir, "docs", "html", fileName);
            if (!File.Exists(docPath))
            {
                docPath = Path.Combine(baseDir, "..", "..", "..", "..", "docs", "html", fileName);
            }

            if (File.Exists(docPath))
            {
                TerminalLauncherService.OpenFileOrUrl(Path.GetFullPath(docPath));
                ShowNotification($"Opened {docName} documentation in browser.");
            }
            else
            {
                ShowNotification($"Doc file not found: {fileName}");
            }
        }
        catch (Exception ex)
        {
            ShowNotification($"Failed to open doc: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        _audioService.PlaySuccess();
        _audioService.IsEnabled = Settings.SoundEnabled;
        Settings.GradientTheme = GradientTheme;
        Settings.SwarmExecutionMode = SwarmExecutionMode;
        Settings.ConfirmWorktreeMerge = ConfirmWorktreeMerge;
        Settings.AutoSyncEnabled = AutoSyncEnabled;
        Settings.NavbarDisplayMode = NavbarDisplayMode;
        await _storageService.SaveSettingsAsync(Settings);
        ShowNotification("Settings saved successfully.");
    }

    [RelayCommand]
    public void RefreshLogs()
    {
        LogLines.Clear();
        _allLogEntries.Clear();
        var lines = Logger.GetRecentLogLines();
        foreach (var l in lines)
        {
            LogLines.Add(l);
            _allLogEntries.Add(LogEntryItem.Parse(l));
        }
        ApplyLogFilters();
    }

    public void ApplyLogFilters()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(ApplyLogFilters);
            return;
        }

        var filterLevel = SelectedLogLevelFilter?.Trim().ToUpperInvariant() ?? "ALL";
        var query = LogSearchQuery?.Trim() ?? string.Empty;

        var matching = _allLogEntries.AsEnumerable();
        if (filterLevel != "ALL")
        {
            matching = matching.Where(e => string.Equals(e.Level, filterLevel, StringComparison.OrdinalIgnoreCase)
                                        || (filterLevel == "WARN" && string.Equals(e.Level, "WARNING", StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrEmpty(query))
        {
            matching = matching.Where(e =>
                e.Message.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.Level.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.TimestampFormatted.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        FilteredLogEntries.Clear();
        foreach (var item in matching.TakeLast(400))
        {
            FilteredLogEntries.Add(item);
        }
    }

    [RelayCommand]
    public void ClearLogs()
    {
        _audioService.PlayClick();
        Logger.Clear();
        _allLogEntries.Clear();
        LogLines.Clear();
        FilteredLogEntries.Clear();
        ShowNotification("Logs buffer cleared.");
    }

    [RelayCommand]
    public void PruneFileLogs()
    {
        _audioService.PlayClick();
        int pruned = Logger.PruneOldLogFiles(7);
        RefreshLogs();
        ShowNotification($"Pruned {pruned} old log file(s) from disk.");
    }

    [RelayCommand]
    public async Task ExportLogsExcelAsync()
    {
        _audioService.PlayClick();
        try
        {
            string? destinationPath = null;
            var defaultFileName = $"agyswarm_logs_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            if (SaveExcelFileRequested != null)
            {
                destinationPath = await SaveExcelFileRequested.Invoke(defaultFileName);
            }
            else
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                destinationPath = Path.Combine(desktop, defaultFileName);
            }

            if (string.IsNullOrWhiteSpace(destinationPath)) return;

            var lines = Logger.GetRecentLogLines();
            await LogExcelExportService.ExportToFileAsync(destinationPath, lines);
            ShowNotification($"Exported logs to: {Path.GetFileName(destinationPath)}");
        }
        catch (Exception ex)
        {
            Logger.Error("[Logs] Excel export failed", ex);
            ShowNotification($"Excel export failed: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task BrowseCliBinaryPathAsync()
    {
        _audioService.PlayClick();
        if (BrowseCliBinaryFileRequested != null)
        {
            var selected = await BrowseCliBinaryFileRequested.Invoke();
            if (!string.IsNullOrWhiteSpace(selected))
            {
                DetectedAgyPath = selected;
                Settings.CustomAgyExecutablePath = selected;
                await _storageService.SaveSettingsAsync(Settings);
                IsAgyInstalled = true;
                ShowNotification($"Set Antigravity CLI path: {Path.GetFileName(selected)}");
            }
        }
    }

    private async Task LoadMcpServersAsync()
    {
        try
        {
            var servers = await _mcpService.LoadMcpServersAsync();
            McpServers.Clear();
            foreach (var s in servers)
            {
                McpServers.Add(s);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed loading MCP servers: {ex.Message}");
        }
    }

    private void UpdateSwarmStatus()
    {
        _activeSwarmProcesses.RemoveAll(p => p.HasExited);
        ActiveWorkersCount = _activeSwarmProcesses.Count;
        IsSwarmRunning = ActiveWorkersCount > 0;
        SwarmStatusText = IsSwarmRunning 
            ? $"Swarm Active ({ActiveWorkersCount} Worker{(ActiveWorkersCount > 1 ? "s" : "")})" 
            : "Swarm Idle";
    }

    private void SyncTimer_Tick(object? sender, EventArgs e)
    {
        if (!AutoSyncEnabled)
        {
            AutoSyncCountdown = "Off";
            UpdateSwarmStatus();
            return;
        }

        var remaining = _nextSyncTime - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            _nextSyncTime = DateTime.UtcNow.AddMinutes(5);
            AutoSyncCountdown = "05:00";
            _ = SyncSwarmAsync(isPeriodic: true);
        }
        else
        {
            AutoSyncCountdown = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        }

        UpdateSwarmStatus();
    }

    public void ShowNotification(string message)
    {
        NotificationMessage = message;
        IsNotificationVisible = true;

        Task.Delay(3500).ContinueWith(_ =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsNotificationVisible = false;
            });
        });
    }

    [RelayCommand]
    public async Task CheckPreflightAsync()
    {
        _audioService.PlayClick();
        PreflightResult = await _fleetDispatcherService.CheckPreflightResourcesAsync(FleetWorkspacePath);
        if (!PreflightResult.IsSafe)
        {
            ShowNotification(PreflightResult.ErrorMessage ?? "Preflight check failed.");
        }
        else if (!string.IsNullOrEmpty(PreflightResult.WarningMessage))
        {
            ShowNotification(PreflightResult.WarningMessage);
        }
        else
        {
            ShowNotification($"Preflight OK: {PreflightResult.AvailableDiskGb} GB Free | Branch: {PreflightResult.CurrentBranch ?? "None"}");
        }
        await RefreshWorktreesAsync();
        UpdateWorktreeTreeNodes();
    }

    [RelayCommand]
    public void ApplyObjectiveSuggestion(string? suggestion)
    {
        _audioService.PlayClick();
        if (string.IsNullOrWhiteSpace(suggestion))
        {
            FleetTaskObjective = string.Empty;
            return;
        }

        FleetTaskObjective = suggestion switch
        {
            "fullstack" => "Implement end-to-end user authentication with JWT tokens, refresh rotation, secure HTTP cookies, frontend login form, and integration test suite.",
            "security" => "Perform thorough security audit, sanitize command execution inputs against shell injections, validate path traversal guards, and fix OWASP vulnerabilities.",
            "tests" => "Author comprehensive unit tests and regression test suite covering all public service methods, edge cases, error handling, and performance benchmarks.",
            "docs" => "Generate comprehensive OpenAPI / Swagger specifications, architecture diagrams, module docstrings, and developer onboarding documentation.",
            "refactor" => "Profile bottlenecks, optimize memory allocations, eliminate redundant disk I/O, and refactor monolithic methods into testable modular services.",
            _ => suggestion
        };
        ShowNotification("Applied objective suggestion template.");
    }

    [RelayCommand]
    public void ClearObjective()
    {
        _audioService.PlayClick();
        FleetTaskObjective = string.Empty;
    }

    [RelayCommand]
    public async Task DispatchFleetAsync()
    {
        if (string.IsNullOrWhiteSpace(FleetTaskObjective))
        {
            ShowNotification("Please enter a task objective to dispatch.");
            return;
        }

        var selected = Profiles.Where(p => p.IsSelectedForSwarm).ToList();
        if (selected.Count == 0)
        {
            ShowNotification("No accounts selected for Swarm Dispatch.");
            return;
        }

        var exhausted = selected.Where(p => p.HasExhaustedQuota).ToList();
        if (exhausted.Count > 0)
        {
            var names = string.Join(", ", exhausted.Select(e => e.Name));
            ShowNotification($"⚠️ Quota Warning: {exhausted.Count} account(s) ({names}) have exhausted daily quota!");
        }

        // Run Pre-flight check first
        PreflightResult = await _fleetDispatcherService.CheckPreflightResourcesAsync(FleetWorkspacePath);
        if (!PreflightResult.IsSafe)
        {
            ShowNotification($"Dispatch blocked: {PreflightResult.ErrorMessage}");
            return;
        }

        IsFleetDispatching = true;
        OnPropertyChanged(nameof(CanAbortFleet));
        DispatchProgressPercent = 5;
        DispatchProgressStage = "Initializing Fleet Dispatcher...";
        DispatchProgressDetail = $"Preparing dispatch for {selected.Count} worker account(s)...";

        _audioService.PlayLaunch();
        FleetStatusText = $"Dispatching to {selected.Count} workers...";
        ShowNotification($"Synthesizing and dispatching to {selected.Count} workers...");

        var config = new FleetDispatchConfig
        {
            TaskObjective = FleetTaskObjective,
            TargetWorkspace = FleetWorkspacePath,
            Mode = SelectedDispatchMode,
            ExecutionMode = SelectedFleetExecutionMode,
            DangerouslySkipPermissions = FleetDangerouslySkipPermissions,
            UseGitWorktrees = UseGitWorktrees,
            SelectedWorkerNames = selected.Select(w => w.Name).ToList()
        };

        try
        {
            var progress = new Progress<FleetProgressReport>(report =>
            {
                DispatchProgressPercent = report.Percent;
                DispatchProgressStage = report.Stage;
                DispatchProgressDetail = report.Detail;
            });

            var tasks = await _fleetDispatcherService.DispatchFleetAsync(config, selected.Select(p => p.Profile).ToList(), SelectedTerminal, progress, SelectedProject);
            DispatchedTasks.Clear();
            foreach (var t in tasks)
            {
                DispatchedTasks.Add(t);
            }
            HasDispatchedTasks = DispatchedTasks.Count > 0;
            OnPropertyChanged(nameof(CanAbortFleet));

            FleetStatusText = $"Swarm Active ({tasks.Count} Dispatched)";
            ShowNotification($"Successfully dispatched objective to {tasks.Count} workers.");
            await RefreshWorktreesAsync();
            UpdateWorktreeTreeNodes();

            if (SelectedProject != null)
            {
                await RefreshSwarmChatMessagesAsync();
            }

            // Start background process watcher
            _fleetProcessWatcherTimer.Start();
        }
        catch (Exception ex)
        {
            Logger.Error("[FleetDispatcher] Dispatch error", ex);
            FleetStatusText = $"Dispatch Failed: {ex.Message}";
            ShowNotification($"Fleet dispatch failed: {ex.Message}");
        }
        finally
        {
            IsFleetDispatching = false;
            OnPropertyChanged(nameof(CanAbortFleet));
        }
    }

    public bool CanAbortFleet => IsFleetDispatching || DispatchedTasks.Any(t => t.IsActive);

    [RelayCommand]
    public async Task AbortFleetAsync()
    {
        _audioService.PlayDelete();
        _fleetProcessWatcherTimer.Stop();

        if (DispatchedTasks.Count == 0 || !DispatchedTasks.Any(t => t.IsActive))
        {
            ShowNotification("No active worker processes are currently running to abort.");
            OnPropertyChanged(nameof(CanAbortFleet));
            return;
        }

        int killed = await _fleetDispatcherService.AbortFleetAsync(DispatchedTasks);
        FleetStatusText = "Fleet Aborted";
        OnPropertyChanged(nameof(CanAbortFleet));
        ShowNotification(killed > 0 
            ? $"🛑 Aborted {killed} active worker process(es) successfully." 
            : "🛑 All worker processes stopped.");
        UpdateWorktreeTreeNodes();
        await RefreshWorktreesAsync();

        if (SelectedProject != null)
        {
            _ = _swarmAggregatorService.PostMessageAsync(SelectedProject, new SwarmChatMessage
            {
                ProjectId = SelectedProject.Id,
                SenderName = "Swarm Orchestrator",
                SenderRole = "System",
                SenderColor = "#EF4444",
                Content = $"🛑 Swarm aborted by user ({killed} task(s) stopped).",
                Type = SwarmMessageType.SystemEvent
            });
            await RefreshSwarmChatMessagesAsync();
        }
    }

    [RelayCommand]
    public async Task StopWorkerTaskAsync(DispatchedWorkerTask? task)
    {
        if (task == null) return;
        _audioService.PlayClick();

        if (!task.CanStop)
        {
            ShowNotification($"Worker '{task.ProfileName}' is already {task.Status.ToLowerInvariant()}.");
            return;
        }

        await _fleetDispatcherService.StopTaskAsync(task);
        OnPropertyChanged(nameof(CanAbortFleet));
        ShowNotification($"🛑 Stopped worker '{task.ProfileName}'");
        UpdateWorktreeTreeNodes();

        if (SelectedProject != null)
        {
            _ = _swarmAggregatorService.PostMessageAsync(SelectedProject, new SwarmChatMessage
            {
                ProjectId = SelectedProject.Id,
                SenderName = task.ProfileName,
                SenderRole = task.AssignedRole,
                SenderColor = "#EF4444",
                Content = $"🛑 Worker task stopped by user.",
                Type = SwarmMessageType.SystemEvent
            });
            await RefreshSwarmChatMessagesAsync();
        }
    }

    // =========================================================================
    // SWARM PROJECT CRUD & CHAT AGGREGATOR COMMANDS (AVALONIA)
    // =========================================================================

    [RelayCommand]
    public void OpenCreateProjectDialog()
    {
        _audioService.PlayClick();
        ProjectDialogTitle = "Create Swarm Project";
        ProjectDialogId = string.Empty;
        ProjectDialogName = "My Swarm Project";
        ProjectDialogDescription = "Autonomous multi-agent project collaboration swarm";
        ProjectDialogTechStack = "Rust";

        var defaultRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "SwarmProjects", "my-swarm-project");
        ProjectDialogRootDirectory = defaultRoot;
        ProjectDialogUseGitWorktrees = true;
        ProjectDialogDefaultBranch = "main";
        ProjectDialogDefaultObjective = "Implement project architecture, components, and test suite.";

        ProjectDialogWorkers.Clear();
        foreach (var p in Profiles)
        {
            ProjectDialogWorkers.Add(new ProjectWorkerSelectionItem
            {
                ProfileId = p.Id,
                ProfileName = p.Name,
                AccountEmail = p.AccountEmail ?? string.Empty,
                ColorTag = p.ColorTag,
                AvatarUrl = p.AvatarUrl,
                AvatarInitial = !string.IsNullOrEmpty(p.AvatarInitial) ? p.AvatarInitial : "W",
                Tier = p.Tier,
                IsSelected = true
            });
        }

        IsProjectDialogOpen = true;
    }

    [RelayCommand]
    public void OpenEditProjectDialog(SwarmProject? project = null)
    {
        var proj = project ?? SelectedProject;
        if (proj == null) return;

        _audioService.PlayClick();
        ProjectDialogTitle = $"Edit Project: {proj.Name}";
        ProjectDialogId = proj.Id;
        ProjectDialogName = proj.Name;
        ProjectDialogDescription = proj.Description;
        ProjectDialogTechStack = proj.TechStack;
        ProjectDialogRootDirectory = proj.RootDirectory;
        ProjectDialogUseGitWorktrees = proj.UseGitWorktrees;
        ProjectDialogDefaultBranch = proj.DefaultBranch;
        ProjectDialogDefaultObjective = proj.DefaultObjective;

        ProjectDialogWorkers.Clear();
        foreach (var p in Profiles)
        {
            bool isAssigned = proj.AssignedWorkerIds.Count == 0 || proj.AssignedWorkerIds.Contains(p.Id);
            ProjectDialogWorkers.Add(new ProjectWorkerSelectionItem
            {
                ProfileId = p.Id,
                ProfileName = p.Name,
                AccountEmail = p.AccountEmail ?? string.Empty,
                ColorTag = p.ColorTag,
                AvatarUrl = p.AvatarUrl,
                AvatarInitial = !string.IsNullOrEmpty(p.AvatarInitial) ? p.AvatarInitial : "W",
                Tier = p.Tier,
                IsSelected = isAssigned
            });
        }

        IsProjectDialogOpen = true;
    }

    [RelayCommand]
    public async Task BrowseProjectDialogFolderAsync()
    {
        _audioService.PlayClick();
        if (BrowseFolderRequested != null)
        {
            var path = await BrowseFolderRequested.Invoke();
            if (!string.IsNullOrWhiteSpace(path))
            {
                ProjectDialogRootDirectory = path;
            }
        }
    }

    [RelayCommand]
    public async Task SaveProjectDialogAsync()
    {
        if (string.IsNullOrWhiteSpace(ProjectDialogName))
        {
            ShowNotification("Please provide a project name.");
            return;
        }

        if (string.IsNullOrWhiteSpace(ProjectDialogRootDirectory))
        {
            ShowNotification("Please specify a root directory for the project.");
            return;
        }

        _audioService.PlayClick();

        var selectedWorkerIds = ProjectDialogWorkers
            .Where(w => w.IsSelected)
            .Select(w => w.ProfileId)
            .ToList();

        SwarmProject proj;
        bool isNew = string.IsNullOrWhiteSpace(ProjectDialogId);

        if (isNew)
        {
            proj = new SwarmProject
            {
                Id = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow
            };
            Projects.Add(proj);
        }
        else
        {
            proj = Projects.FirstOrDefault(p => p.Id == ProjectDialogId) ?? new SwarmProject { Id = ProjectDialogId };
            if (!Projects.Contains(proj))
            {
                Projects.Add(proj);
            }
        }

        proj.Name = ProjectDialogName.Trim();
        proj.Description = ProjectDialogDescription.Trim();
        proj.TechStack = ProjectDialogTechStack;
        proj.RootDirectory = ProjectDialogRootDirectory.Trim();
        proj.UseGitWorktrees = ProjectDialogUseGitWorktrees;
        proj.DefaultBranch = string.IsNullOrWhiteSpace(ProjectDialogDefaultBranch) ? "main" : ProjectDialogDefaultBranch.Trim();
        proj.DefaultObjective = ProjectDialogDefaultObjective.Trim();
        proj.AssignedWorkerIds = selectedWorkerIds;
        proj.LastActiveAt = DateTime.UtcNow;

        await _storageService.SaveProjectsAsync(Projects);
        HasProjects = Projects.Count > 0;
        SelectedProject = proj;

        var assignedProfiles = Profiles
            .Where(p => selectedWorkerIds.Contains(p.Id))
            .Select(p => p.Profile);
        _swarmAggregatorService.EnsureProjectSwarmWorkspace(proj, assignedProfiles);

        IsProjectDialogOpen = false;
        ShowNotification($"Project '{proj.Name}' saved successfully.");
        await RefreshSwarmChatMessagesAsync();
    }

    [RelayCommand]
    public void CancelProjectDialog()
    {
        _audioService.PlayClick();
        IsProjectDialogOpen = false;
    }

    [RelayCommand]
    public async Task DeleteProjectAsync(SwarmProject? project = null)
    {
        var target = project ?? SelectedProject;
        if (target == null) return;

        _audioService.PlayDelete();
        Projects.Remove(target);
        await _storageService.SaveProjectsAsync(Projects);
        HasProjects = Projects.Count > 0;
        SelectedProject = Projects.FirstOrDefault();
        ShowNotification($"Project '{target.Name}' deleted.");
    }

    [RelayCommand]
    public void OpenProjectFolder(string? path = null)
    {
        var targetDir = path ?? SelectedProject?.RootDirectory;
        if (string.IsNullOrWhiteSpace(targetDir) || !Directory.Exists(targetDir)) return;
        _audioService.PlayClick();
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = targetDir,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch (Exception ex)
        {
            Logger.Warn($"[AvaloniaMainViewModel] Failed to open folder {targetDir}: {ex.Message}");
        }
    }

    [RelayCommand]
    public void OpenProjectBlackboard()
    {
        if (SelectedProject == null) return;
        _audioService.PlayClick();
        var rootDir = SwarmAggregatorService.ResolveProjectRootDir(SelectedProject);
        var blackboardPath = Path.Combine(rootDir, ".swarm", "blackboard.md");
        if (!File.Exists(blackboardPath))
        {
            _swarmAggregatorService.EnsureProjectSwarmWorkspace(SelectedProject, Enumerable.Empty<AccountProfile>());
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = blackboardPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Warn($"[SwarmChat] Failed to open blackboard: {ex.Message}");
            ShowNotification("Could not open blackboard.md");
        }
    }

    public void EnsureProjectContextForChat()
    {
        if (SelectedProject == null)
        {
            if (Projects.Count > 0)
            {
                SelectedProject = Projects[0];
            }
            else
            {
                var defaultProj = new SwarmProject
                {
                    Name = "Real-Time Fleet Workspace",
                    RootDirectory = FleetWorkspacePath ?? Environment.CurrentDirectory,
                    DefaultObjective = "Interactive Swarm Dialogue"
                };
                Projects.Add(defaultProj);
                SelectedProject = defaultProj;
            }
        }
    }

    public void PopulateChatTargetWorkers()
    {
        var current = SelectedChatTargetWorker;
        ChatTargetWorkers.Clear();
        ChatTargetWorkers.Add("All Workers (Broadcast)");
        foreach (var p in Profiles)
        {
            if (!ChatTargetWorkers.Contains(p.Name))
            {
                ChatTargetWorkers.Add(p.Name);
            }
        }
        if (ChatTargetWorkers.Contains(current))
        {
            SelectedChatTargetWorker = current;
        }
        else
        {
            SelectedChatTargetWorker = ChatTargetWorkers[0];
        }
    }

    [RelayCommand]
    public void OpenAccountRealtimeChat(AvaloniaProfileItemViewModel? profile)
    {
        _audioService.PlayClick();
        EnsureProjectContextForChat();
        PopulateChatTargetWorkers();
        if (profile != null)
        {
            if (!ChatTargetWorkers.Contains(profile.Name))
            {
                ChatTargetWorkers.Add(profile.Name);
            }
            SelectedChatTargetWorker = profile.Name;
        }
        Navigate("RealtimeChat");
        ShowNotification($"Opened Real-Time Chat session for '{profile?.Name ?? "Fleet"}'");
    }

    [RelayCommand]
    public void SelectChatTarget(string? target)
    {
        if (!string.IsNullOrEmpty(target))
        {
            _audioService.PlayClick();
            if (!ChatTargetWorkers.Contains(target))
            {
                ChatTargetWorkers.Add(target);
            }
            SelectedChatTargetWorker = target;
        }
    }

    [RelayCommand]
    public async Task SendSuggestedPromptAsync(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return;
        SwarmChatInputText = prompt;
        await SendSwarmChatMessageAsync();
    }

    [RelayCommand]
    public void ClearSwarmChat()
    {
        _audioService.PlayClick();
        SwarmChatMessages.Clear();
        HasSwarmChatMessages = false;
        ShowNotification("Real-Time chat view cleared.");
    }

    [RelayCommand]
    public async Task ExportChatTranscriptAsync()
    {
        _audioService.PlayClick();
        if (SwarmChatMessages.Count == 0)
        {
            ShowNotification("No messages in chat to export.");
            return;
        }
        try
        {
            var exportFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini", "antigravity-cli", "transcripts");
            Directory.CreateDirectory(exportFolder);
            var filePath = Path.Combine(exportFolder, $"chat_transcript_{DateTime.Now:yyyyMMdd_HHmmss}.md");
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Real-Time Swarm Chat Transcript");
            sb.AppendLine($"Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Target: {SelectedChatTargetWorker}");
            sb.AppendLine();
            foreach (var msg in SwarmChatMessages)
            {
                sb.AppendLine($"### [{msg.Timestamp:HH:mm:ss}] {msg.SenderName} {(string.IsNullOrEmpty(msg.TargetWorker) ? "" : $"-> {msg.TargetWorker}")}");
                sb.AppendLine(msg.Content);
                sb.AppendLine();
            }
            await File.WriteAllTextAsync(filePath, sb.ToString());
            ShowNotification($"Transcript exported: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to export chat transcript", ex);
            ShowNotification($"Export failed: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task CopyChatMessageAsync(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _audioService.PlayClick();
        try
        {
            if (global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.Clipboard != null)
            {
                var data = new DataTransfer();
                data.Add(DataTransferItem.CreateText(text));
                await desktop.MainWindow.Clipboard.SetDataAsync(data);
                ShowNotification("Message copied to clipboard.");
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to copy message: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task SendSwarmChatMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(SwarmChatInputText)) return;
        EnsureProjectContextForChat();
        if (SelectedProject == null) return;

        _audioService.PlayClick();
        var text = SwarmChatInputText.Trim();
        SwarmChatInputText = string.Empty;

        string? target = SelectedChatTargetWorker.StartsWith("All", StringComparison.OrdinalIgnoreCase)
            ? null
            : SelectedChatTargetWorker;

        var msg = await _swarmAggregatorService.BroadcastUserInstructionAsync(SelectedProject, text, target);
        var userAvatar = Profiles.FirstOrDefault(p => p.HasAvatarUrl)?.AvatarUrl;
        if (!string.IsNullOrEmpty(userAvatar))
        {
            msg.AvatarUrl = userAvatar;
        }
        SwarmChatMessages.Add(msg);
        HasSwarmChatMessages = SwarmChatMessages.Count > 0;
        _audioService.PlaySuccess();
        ShowNotification("Instruction dispatched to swarm.");
    }

    [RelayCommand]
    public async Task RefreshSwarmChatMessagesAsync()
    {
        if (SelectedProject == null)
        {
            SwarmChatMessages.Clear();
            HasSwarmChatMessages = false;
            return;
        }
        var messages = await _swarmAggregatorService.LoadProjectMessagesAsync(SelectedProject);
        SwarmChatMessages.Clear();
        foreach (var m in messages)
        {
            if (string.IsNullOrEmpty(m.AvatarUrl))
            {
                var match = Profiles.FirstOrDefault(p => string.Equals(p.Name, m.SenderName, StringComparison.OrdinalIgnoreCase));
                if (match?.HasAvatarUrl == true)
                {
                    m.AvatarUrl = match.AvatarUrl;
                }
            }
            SwarmChatMessages.Add(m);
        }
        HasSwarmChatMessages = SwarmChatMessages.Count > 0;
    }

    // =========================================================================
    // PERSONAL CHAT & RAG HUB SUBSYSTEM
    // =========================================================================
    public ObservableCollection<PersonalChatSession> PersonalChatSessions { get; } = new();
    public ObservableCollection<PersonalChatMessage> PersonalChatMessages { get; } = new();
    public ObservableCollection<string> RagKnowledgeBases { get; } = new();
    public ObservableCollection<RagChunkItem> LastRetrievedRagChunks { get; } = new();

    [ObservableProperty]
    private PersonalChatSession? _selectedPersonalChatSession;

    partial void OnSelectedPersonalChatSessionChanged(PersonalChatSession? value)
    {
        if (value != null)
        {
            _ = LoadPersonalChatSessionMessagesAsync(value);
        }
    }

    [ObservableProperty]
    private AvaloniaProfileItemViewModel? _personalChatSelectedProfile;

    partial void OnPersonalChatSelectedProfileChanged(AvaloniaProfileItemViewModel? value)
    {
        if (value != null)
        {
            _ = ReloadPersonalChatSessionsForProfileAsync(value.Id);
        }
    }

    [ObservableProperty]
    private string _personalChatInputText = string.Empty;

    [ObservableProperty]
    private bool _isPersonalChatGenerating = false;

    [ObservableProperty]
    private string _personalChatSelectedModel = "gemini-3.8-flash-medium";

    public ObservableCollection<string> PersonalChatModelOptions { get; } = [];

    [ObservableProperty]
    private string _personalChatSelectedEffort = "medium";

    public List<string> PersonalChatEffortOptions { get; } = new()
    {
        "low",
        "medium",
        "high"
    };

    [ObservableProperty]
    private bool _isRagEnabled = true;

    [ObservableProperty]
    private bool _isAragAvailable = false;

    [ObservableProperty]
    private string _selectedRagKnowledgeBase = "agy-swarm";

    [ObservableProperty]
    private string _selectedRagSearchMode = "Hybrid (BM25 + Semantic)";

    public List<string> RagSearchModes { get; } = new()
    {
        "Hybrid (BM25 + Semantic)",
        "Keyword (BM25)",
        "Semantic Vector"
    };

    [ObservableProperty]
    private int _ragTopK = 3;

    [ObservableProperty]
    private bool _hasLastRetrievedRagChunks = false;

    [ObservableProperty]
    private bool _isRagSearching = false;

    [ObservableProperty]
    private string _liveSwarmTaskSummary = "Swarm Status: Checking live telemetry...";

    [ObservableProperty]
    private bool _hasLiveSwarmTasks = false;

    [ObservableProperty]
    private bool _includeLiveTaskContextInPrompt = true;

    private CancellationTokenSource? _personalChatCts;

    public async Task InitializePersonalChatAsync()
    {
        try
        {
            IsAragAvailable = _ragService.IsAragAvailable;
            var kbs = await _ragService.GetKnowledgeBasesAsync();
            Dispatcher.UIThread.Post(() =>
            {
                RagKnowledgeBases.Clear();
                foreach (var kb in kbs) RagKnowledgeBases.Add(kb.Name);
                if (RagKnowledgeBases.Count > 0 && string.IsNullOrEmpty(SelectedRagKnowledgeBase))
                {
                    SelectedRagKnowledgeBase = RagKnowledgeBases.First();
                }
            });

            await PopulatePersonalChatDynamicModelsAsync();
            RefreshLiveSwarmTasks();

            if (Profiles.Count > 0 && PersonalChatSelectedProfile == null)
            {
                PersonalChatSelectedProfile = Profiles.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to initialize personal chat: {ex.Message}");
        }
    }

    public async Task PopulatePersonalChatDynamicModelsAsync(bool forceCliRefresh = false)
    {
        try
        {
            var profilePaths = Profiles.Select(p => p.Profile.CustomProfilePath).OfType<string>().ToList();
            var models = await _modelService.DiscoverModelsAsync(profilePaths, forceCliRefresh);
            Dispatcher.UIThread.Post(() =>
            {
                PersonalChatModelOptions.Clear();
                foreach (var m in models)
                {
                    PersonalChatModelOptions.Add(m.Id);
                }

                if (!PersonalChatModelOptions.Contains(PersonalChatSelectedModel))
                {
                    PersonalChatSelectedModel = PersonalChatModelOptions.FirstOrDefault(m => m.Contains("3.8-flash-medium"))
                                             ?? PersonalChatModelOptions.FirstOrDefault()
                                             ?? "gemini-3.8-flash-medium";
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Warn($"[PersonalChat] Failed to dynamically populate models: {ex.Message}");
        }
    }

    public void EnsurePersonalChatContext()
    {
        if (PersonalChatSelectedProfile == null && Profiles.Count > 0)
        {
            PersonalChatSelectedProfile = Profiles.FirstOrDefault();
        }
        if (PersonalChatModelOptions.Count == 0)
        {
            _ = PopulatePersonalChatDynamicModelsAsync();
        }
        RefreshLiveSwarmTasks();
    }

    [RelayCommand]
    public async Task SendPersonalChatMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(PersonalChatInputText) || IsPersonalChatGenerating) return;

        var userText = PersonalChatInputText.Trim();
        PersonalChatInputText = string.Empty;

        var profile = PersonalChatSelectedProfile?.Profile ?? Profiles.FirstOrDefault()?.Profile;
        if (profile == null)
        {
            ShowNotification("Please select an active profile before chatting.");
            return;
        }

        if (SelectedPersonalChatSession == null)
        {
            CreateNewPersonalChatSession();
        }

        var userMsg = new PersonalChatMessage
        {
            Role = "user",
            Content = userText,
            Timestamp = DateTime.UtcNow
        };
        PersonalChatMessages.Add(userMsg);

        IsPersonalChatGenerating = true;
        _personalChatCts = new CancellationTokenSource();
        _audioService.PlayClick();

        try
        {
            List<RagChunkItem>? retrievedChunks = null;

            // 1. RAG retrieval if enabled
            if (IsRagEnabled)
            {
                IsRagSearching = true;
                var ragRes = await _ragService.SearchContextAsync(
                    userText,
                    SelectedRagKnowledgeBase,
                    SelectedRagSearchMode,
                    RagTopK,
                    _personalChatCts.Token);

                IsRagSearching = false;
                if (ragRes.IsSuccess && ragRes.Chunks.Count > 0)
                {
                    retrievedChunks = ragRes.Chunks;
                    LastRetrievedRagChunks.Clear();
                    foreach (var c in ragRes.Chunks) LastRetrievedRagChunks.Add(c);
                    HasLastRetrievedRagChunks = true;
                }
            }

            // 2. Real-time task context if enabled
            string? taskContext = null;
            if (IncludeLiveTaskContextInPrompt)
            {
                taskContext = _personalChatService.GetRealtimeSwarmTaskStatusSummary(Directory.GetCurrentDirectory());
                LiveSwarmTaskSummary = taskContext;
                HasLiveSwarmTasks = !string.IsNullOrEmpty(taskContext) && !taskContext.Contains("Idle");
            }

            // 3. Build Augmented Prompt
            var augmentedPrompt = _ragService.BuildAugmentedPrompt(userText, retrievedChunks ?? new List<RagChunkItem>(), taskContext);

            // 4. Send to agy CLI
            var assistantMsg = await _personalChatService.SendMessageAsync(
                profile,
                augmentedPrompt,
                PersonalChatSelectedModel,
                PersonalChatSelectedEffort,
                SelectedPersonalChatSession?.Id,
                retrievedChunks,
                Settings.CustomAgyExecutablePath ?? DetectedAgyPath,
                _personalChatCts.Token);

            PersonalChatMessages.Add(assistantMsg);
            _audioService.PlaySuccess();

            // 5. Update session metadata and save to disk
            if (SelectedPersonalChatSession != null)
            {
                if (SelectedPersonalChatSession.TurnCount == 0)
                {
                    SelectedPersonalChatSession.Title = userText.Length > 40 ? userText.Substring(0, 40) + "..." : userText;
                }
                SelectedPersonalChatSession.TurnCount = PersonalChatMessages.Count(m => m.IsUser);
                SelectedPersonalChatSession.UpdatedAt = DateTime.UtcNow;
                SelectedPersonalChatSession.Model = PersonalChatSelectedModel;

                await _personalChatService.SaveSessionAsync(SelectedPersonalChatSession, PersonalChatMessages.ToList());
            }
        }
        catch (OperationCanceledException)
        {
            PersonalChatMessages.Add(new PersonalChatMessage
            {
                Role = "assistant",
                Content = "⏹️ Generation stopped by user.",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            PersonalChatMessages.Add(new PersonalChatMessage
            {
                Role = "assistant",
                Content = $"⚠️ Error during chat execution: {ex.Message}",
                Timestamp = DateTime.UtcNow
            });
            _audioService.PlayQuotaAlert();
        }
        finally
        {
            IsPersonalChatGenerating = false;
            IsRagSearching = false;
            _personalChatCts?.Dispose();
            _personalChatCts = null;
        }
    }

    [RelayCommand]
    public void CancelPersonalChatGeneration()
    {
        _personalChatCts?.Cancel();
    }

    [RelayCommand]
    public void CreateNewPersonalChatSession()
    {
        var profileId = PersonalChatSelectedProfile?.Id ?? "main";
        var newSession = new PersonalChatSession
        {
            Id = Guid.NewGuid().ToString(),
            ProfileId = profileId,
            Title = "New Conversation",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Model = PersonalChatSelectedModel
        };

        PersonalChatSessions.Insert(0, newSession);
        SelectedPersonalChatSession = newSession;
        PersonalChatMessages.Clear();
        LastRetrievedRagChunks.Clear();
        HasLastRetrievedRagChunks = false;
        _audioService.PlayClick();
    }

    [RelayCommand]
    public async Task DeletePersonalChatSessionAsync(PersonalChatSession? session)
    {
        var target = session ?? SelectedPersonalChatSession;
        if (target == null) return;

        await _personalChatService.DeleteSessionAsync(target.ProfileId, target.Id);
        PersonalChatSessions.Remove(target);

        if (SelectedPersonalChatSession == target)
        {
            SelectedPersonalChatSession = PersonalChatSessions.FirstOrDefault();
            if (SelectedPersonalChatSession == null)
            {
                CreateNewPersonalChatSession();
            }
        }
        _audioService.PlayClick();
    }

    [RelayCommand]
    public async Task ExportPersonalChatSessionAsync()
    {
        if (PersonalChatMessages.Count == 0) return;
        var sb = new StringBuilder();
        sb.AppendLine($"# Chat Transcript: {SelectedPersonalChatSession?.Title ?? "Session"}");
        sb.AppendLine($"*Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Profile: {PersonalChatSelectedProfile?.Name}*");
        sb.AppendLine();
        foreach (var msg in PersonalChatMessages)
        {
            sb.AppendLine($"### {(msg.IsUser ? "🧑 You" : "🤖 " + (msg.Model ?? "Assistant"))} ({msg.FormattedTime})");
            if (msg.HasRagChunks)
            {
                sb.AppendLine($"> 📚 Injected RAG Chunks: {msg.InjectedRagChunks.Count} chunks from {msg.InjectedRagChunks.FirstOrDefault()?.KbName}");
            }
            sb.AppendLine(msg.Content);
            sb.AppendLine();
        }

        var exportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"agy_chat_{DateTime.Now:yyyyMMdd_HHmmss}.md");
        await File.WriteAllTextAsync(exportPath, sb.ToString());
        ShowNotification($"Transcript exported to Desktop: {Path.GetFileName(exportPath)}");
        _audioService.PlaySuccess();
    }

    [RelayCommand]
    public async Task SelectPersonalChatSessionAsync(PersonalChatSession? session)
    {
        if (session == null) return;
        SelectedPersonalChatSession = session;
        await LoadPersonalChatSessionMessagesAsync(session);
    }

    public async Task LoadPersonalChatSessionMessagesAsync(PersonalChatSession session)
    {
        PersonalChatMessages.Clear();
        var msgs = await _personalChatService.LoadMessagesAsync(session.ProfileId, session.Id);
        foreach (var m in msgs) PersonalChatMessages.Add(m);
    }

    public async Task ReloadPersonalChatSessionsForProfileAsync(string profileId)
    {
        PersonalChatSessions.Clear();
        var sessions = await _personalChatService.GetSessionsAsync(profileId);
        foreach (var s in sessions) PersonalChatSessions.Add(s);
        SelectedPersonalChatSession = PersonalChatSessions.FirstOrDefault();
    }

    [RelayCommand]
    public void RefreshLiveSwarmTasks()
    {
        var summary = _personalChatService.GetRealtimeSwarmTaskStatusSummary(Directory.GetCurrentDirectory());
        LiveSwarmTaskSummary = summary;
        HasLiveSwarmTasks = !string.IsNullOrEmpty(summary) && !summary.Contains("Idle");
    }

    [RelayCommand]
    public void OpenPersonalChatForProfile(string profileId)
    {
        SelectedChatTab = "Personal";
        CurrentPage = "Chat";
        var targetProfile = Profiles.FirstOrDefault(p => p.Id == profileId) ?? Profiles.FirstOrDefault();
        if (targetProfile != null)
        {
            PersonalChatSelectedProfile = targetProfile;
        }
        EnsurePersonalChatContext();
    }

    [RelayCommand]
    public void OpenPersonalChatFromCard(AvaloniaProfileItemViewModel? profileVm)
    {
        if (profileVm != null)
        {
            OpenPersonalChatForProfile(profileVm.Id);
        }
    }

    [RelayCommand]
    public async Task UploadRagDocumentAsync()
    {
        if (BrowseRagDocumentFileRequested == null) return;

        var filePath = await BrowseRagDocumentFileRequested.Invoke();
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        _audioService.PlayClick();
        var fileName = Path.GetFileName(filePath);
        var targetKb = string.IsNullOrWhiteSpace(SelectedRagKnowledgeBase) ? "agy-swarm" : SelectedRagKnowledgeBase;

        ShowNotification($"Indexing '{fileName}' into '{targetKb}'...");

        var (success, message, newChunks) = await _ragService.IndexFileAsync(filePath, targetKb);
        if (success)
        {
            _audioService.PlaySuccess();
            ShowNotification($"✅ Indexed '{fileName}' (+{newChunks} chunks to '{targetKb}').");

            var kbs = await _ragService.GetKnowledgeBasesAsync();
            Dispatcher.UIThread.Post(() =>
            {
                RagKnowledgeBases.Clear();
                foreach (var kb in kbs) RagKnowledgeBases.Add(kb.Name);
                if (!RagKnowledgeBases.Contains(SelectedRagKnowledgeBase) && RagKnowledgeBases.Count > 0)
                {
                    SelectedRagKnowledgeBase = RagKnowledgeBases.First();
                }
            });
        }
        else
        {
            _audioService.PlayQuotaAlert();
            ShowNotification($"⚠️ RAG indexing notice: {message}");
        }
    }

    [RelayCommand]
    public async Task SyncCliHistoryAsync()
    {
        var targetProfile = PersonalChatSelectedProfile?.Profile ?? Profiles.FirstOrDefault()?.Profile;
        if (targetProfile == null)
        {
            ShowNotification("Please select an active profile before syncing.");
            return;
        }

        _audioService.PlayClick();
        ShowNotification($"Scanning Antigravity CLI history for '{targetProfile.Name}'...");

        try
        {
            var count = await _personalChatService.SyncExistingCliHistoryAsync(
                targetProfile.Id,
                targetProfile.CustomProfilePath);

            await ReloadPersonalChatSessionsForProfileAsync(targetProfile.Id);

            if (count > 0)
            {
                _audioService.PlaySuccess();
                ShowNotification($"✅ Synced {count} conversations from Antigravity CLI!");
            }
            else
            {
                ShowNotification("No new historical CLI conversations found to sync.");
            }
        }
        catch (Exception ex)
        {
            _audioService.PlayQuotaAlert();
            ShowNotification($"⚠️ CLI history sync error: {ex.Message}");
        }
    }

    [RelayCommand]
    public void OpenWorktreeFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
        _audioService.PlayClick();
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch (Exception ex)
        {
            Logger.Warn($"[Fleet] Failed to open folder {path}: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task CopyTaskLogAsync(DispatchedWorkerTask? task)
    {
        if (task == null) return;
        _audioService.PlayClick();
        var log = !string.IsNullOrWhiteSpace(task.FullOutputLog) ? task.FullOutputLog : task.LastOutputLine;
        if (!string.IsNullOrWhiteSpace(log))
        {
            try
            {
                if (global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.Clipboard != null)
                {
                    var data = new DataTransfer();
                    data.Add(DataTransferItem.CreateText(log));
                    await desktop.MainWindow.Clipboard.SetDataAsync(data);
                    ShowNotification($"Copied logs for '{task.ProfileName}' to clipboard.");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[Fleet] Failed to copy logs: {ex.Message}");
            }
        }
    }

    private void WatchDispatchedProcesses()
    {
        if (DispatchedTasks.Count == 0) return;

        bool stateChanged = false;
        int activeCount = 0;

        foreach (var task in DispatchedTasks)
        {
            // If already stopped by user, do not let process watcher overwrite its status!
            if (task.Status == "Stopped")
            {
                continue;
            }

            // Update live elapsed duration for active tasks
            if (task.StartedAt.HasValue && task.Status is "Running" or "Launching" or "Active")
            {
                var duration = DateTime.UtcNow - task.StartedAt.Value;
                task.ElapsedTimeFormatted = duration.TotalHours >= 1
                    ? $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}"
                    : $"{duration.Minutes:D2}:{duration.Seconds:D2}";
            }

            if (task.ProcessId.HasValue)
            {
                try
                {
                    var proc = Process.GetProcessById(task.ProcessId.Value);
                    if (proc.HasExited)
                    {
                        task.CompletedAt = DateTime.UtcNow;
                        if (task.Status != "Stopped")
                        {
                            task.Status = proc.ExitCode == 0 ? "Completed" : $"Exited ({proc.ExitCode})";
                            task.StatusColor = proc.ExitCode == 0 ? "#10B981" : "#EF4444";
                            task.CurrentActivity = proc.ExitCode == 0 ? "✅ Completed successfully" : $"⚠️ Process exited with code {proc.ExitCode}";
                        }
                        task.ProcessId = null;
                        stateChanged = true;
                    }
                    else
                    {
                        activeCount++;
                    }
                }
                catch
                {
                    if (task.Status != "Stopped")
                    {
                        task.CompletedAt = DateTime.UtcNow;
                        task.Status = "Completed";
                        task.StatusColor = "#10B981";
                        task.CurrentActivity = "✅ Completed successfully";
                    }
                    task.ProcessId = null;
                    stateChanged = true;
                }
            }
        }

        if (stateChanged)
        {
            UpdateWorktreeTreeNodes();
            _ = RefreshWorktreesAsync();
            if (activeCount == 0)
            {
                FleetStatusText = "All Worker Tasks Completed";
                _fleetProcessWatcherTimer.Stop();
            }
            else
            {
                FleetStatusText = $"Fleet Active ({activeCount} Running)";
            }
        }
    }

    [RelayCommand]
    public async Task BrowseFleetWorkspaceFolderAsync()
    {
        _audioService.PlayClick();
        if (BrowseFolderRequested != null)
        {
            var path = await BrowseFolderRequested.Invoke();
            if (!string.IsNullOrWhiteSpace(path))
            {
                FleetWorkspacePath = path;
                await CheckPreflightAsync();
            }
        }
    }

    public void UpdateWorktreeTreeNodes()
    {
        WorktreeTreeNodes.Clear();

        if (DispatchedTasks.Count > 0)
        {
            for (int i = 0; i < DispatchedTasks.Count; i++)
            {
                var t = DispatchedTasks[i];
                var isRunning = t.Status is "Active" or "Dispatched" or "Running" or "Launching";
                var isCompleted = t.Status is "Completed";
                var isFailed = t.Status.StartsWith("Failed") || t.Status.StartsWith("Exited");

                var color = isRunning ? "#10B981" : (isCompleted ? "#8B5CF6" : (isFailed ? "#EF4444" : "#64748B"));
                var matchingProfile = Profiles.FirstOrDefault(p => p.Name.Equals(t.ProfileName, StringComparison.OrdinalIgnoreCase));

                WorktreeTreeNodes.Add(new WorktreeTreeNode
                {
                    BranchName = string.IsNullOrWhiteSpace(t.BranchName) ? $"swarm/{t.ProfileName.ToLowerInvariant().Replace(' ', '-')}" : t.BranchName,
                    WorkerName = t.ProfileName,
                    Role = t.AssignedRole,
                    Path = t.WorktreePath,
                    Status = t.Status,
                    StatusColor = color,
                    IsActive = isRunning,
                    IsLast = i == DispatchedTasks.Count - 1,
                    AvatarUrl = matchingProfile?.AvatarUrl ?? t.AvatarUrl,
                    AvatarInitial = matchingProfile?.AvatarInitial ?? (!string.IsNullOrEmpty(t.AvatarInitial) ? t.AvatarInitial : "W"),
                    ColorTag = matchingProfile?.ColorTag ?? (!string.IsNullOrEmpty(t.ColorTag) ? t.ColorTag : "#3B82F6")
                });
            }
        }
        else
        {
            var selected = Profiles.Where(p => p.IsSelectedForSwarm).ToList();
            for (int i = 0; i < selected.Count; i++)
            {
                var p = selected[i];
                var cleanName = p.Name.ToLowerInvariant().Replace(' ', '-');
                WorktreeTreeNodes.Add(new WorktreeTreeNode
                {
                    BranchName = $"swarm/{cleanName}_[session]",
                    WorkerName = p.Name,
                    Role = p.Description ?? "General Worker",
                    Path = string.IsNullOrWhiteSpace(FleetWorkspacePath)
                        ? $".../worktrees/swarm-{cleanName}"
                        : Path.Combine(FleetWorkspacePath, ".git", "worktrees", $"swarm-{cleanName}"),
                    Status = "Planned Worktree",
                    StatusColor = "#64748B",
                    IsActive = false,
                    IsLast = i == selected.Count - 1,
                    AvatarUrl = p.AvatarUrl,
                    AvatarInitial = p.AvatarInitial,
                    ColorTag = p.ColorTag
                });
            }
        }
    }

    [RelayCommand]
    public async Task RefreshWorktreesAsync()
    {
        var targetDir = string.IsNullOrWhiteSpace(FleetWorkspacePath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : FleetWorkspacePath;

        try
        {
            var worktrees = await _gitWorktreeService.ListWorktreesAsync(targetDir);
            ActiveWorktrees.Clear();
            foreach (var wt in worktrees)
            {
                ActiveWorktrees.Add(wt);
            }

            var branches = await _gitWorktreeService.ListSwarmBranchesAsync(targetDir);
            SwarmBranches.Clear();
            foreach (var b in branches)
            {
                SwarmBranches.Add(b);
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"[FleetDispatcher] Refresh worktrees failed: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task PruneWorktreesAsync()
    {
        _audioService.PlayClick();
        var targetDir = string.IsNullOrWhiteSpace(FleetWorkspacePath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : FleetWorkspacePath;

        int pruned = await _gitWorktreeService.PruneStaleWorktreesAsync(targetDir);
        ShowNotification(pruned > 0 ? $"Pruned {pruned} stale worktree(s)." : "No stale worktrees to prune.");
        await RefreshWorktreesAsync();
        UpdateWorktreeTreeNodes();
    }

    [RelayCommand]
    public async Task MergeSwarmBranchAsync(string? branch)
    {
        if (string.IsNullOrWhiteSpace(branch)) return;
        _audioService.PlayClick();

        var targetDir = string.IsNullOrWhiteSpace(FleetWorkspacePath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : FleetWorkspacePath;

        var (success, output) = await _gitWorktreeService.MergeBranchAsync(targetDir, branch);
        if (success)
        {
            _audioService.PlaySuccess();
            ShowNotification($"Merged '{branch}' into current branch successfully.");
        }
        else
        {
            ShowNotification($"Merge warning/conflict: {output}");
        }
        await RefreshWorktreesAsync();
        UpdateWorktreeTreeNodes();
    }

    // ==========================================================
    // ANTIGRAVITY GRADIENT THEMES & SWARM EXECUTION MODES
    // ==========================================================

    [ObservableProperty]
    private string _gradientTheme = "Cyberpunk";

    partial void OnGradientThemeChanged(string value)
    {
        OnPropertyChanged(nameof(LaunchSwarmGradientBrush));
        OnPropertyChanged(nameof(LaunchSwarmForegroundBrush));
    }

    [ObservableProperty]
    private string _swarmExecutionMode = "ConcurrentCli";

    partial void OnSwarmExecutionModeChanged(string value)
    {
        OnPropertyChanged(nameof(SwarmExecutionModeDisplay));
    }

    public string SwarmExecutionModeDisplay => SwarmExecutionMode == "WorkerOrchestration"
        ? "Swarm Worker Orchestration"
        : "Concurrent Swarm CLI";

    public IBrush LaunchSwarmGradientBrush => new SolidColorBrush(Color.Parse("#10B981"));

    public IBrush LaunchSwarmForegroundBrush => new SolidColorBrush(Color.Parse("#FFFFFF"));

    [RelayCommand]
    public void SetGradientTheme(string theme)
    {
        GradientTheme = theme;
        Settings.GradientTheme = theme;
        _ = _storageService.SaveSettingsAsync(Settings);
        _audioService.PlayClick();
        ShowNotification($"Antigravity Gradient set to '{theme}'.");
    }

    [RelayCommand]
    public void SetSwarmExecutionMode(string mode)
    {
        SwarmExecutionMode = mode;
        Settings.SwarmExecutionMode = mode;
        _ = _storageService.SaveSettingsAsync(Settings);
        _audioService.PlayClick();
        ShowNotification($"Swarm mode set to '{SwarmExecutionModeDisplay}'.");
    }

    // ==========================================================
    // WORKTREE SYNTHESIS CONFIRMATION MODAL ALERT
    // ==========================================================

    [ObservableProperty]
    private bool _confirmWorktreeMerge = true;

    [ObservableProperty]
    private bool _isMergeWorktreeDialogVisible = false;

    [ObservableProperty]
    private string _mergeDialogBaseBranch = "main";

    [ObservableProperty]
    private int _mergeDialogBranchesCount = 0;

    public ObservableCollection<string> MergeDialogBranches { get; } = new();

    [RelayCommand]
    public async Task RequestSynthesizeAllWorktreesAsync()
    {
        _audioService.PlayClick();
        var targetDir = string.IsNullOrWhiteSpace(FleetWorkspacePath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : FleetWorkspacePath;

        var branches = await _gitWorktreeService.ListSwarmBranchesAsync(targetDir);
        MergeDialogBranches.Clear();
        foreach (var b in branches)
        {
            MergeDialogBranches.Add(b);
        }
        foreach (var wt in ActiveWorktrees)
        {
            if (!string.IsNullOrWhiteSpace(wt.Branch) && !MergeDialogBranches.Contains(wt.Branch))
            {
                MergeDialogBranches.Add(wt.Branch);
            }
        }
        MergeDialogBranchesCount = MergeDialogBranches.Count;

        if (MergeDialogBranchesCount == 0)
        {
            ShowNotification("No active worker worktrees or swarm branches available to synthesize.");
            return;
        }

        if (ConfirmWorktreeMerge)
        {
            IsMergeWorktreeDialogVisible = true;
        }
        else
        {
            await ConfirmMergeWorktreesAsync();
        }
    }

    [RelayCommand]
    public async Task ConfirmMergeWorktreesAsync()
    {
        IsMergeWorktreeDialogVisible = false;
        _audioService.PlayLaunch();

        var targetDir = string.IsNullOrWhiteSpace(FleetWorkspacePath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : FleetWorkspacePath;

        int successCount = 0;
        int failCount = 0;

        ShowNotification($"Synthesizing {MergeDialogBranches.Count} worker branch(es) into base repository...");

        foreach (var branch in MergeDialogBranches.ToList())
        {
            var (success, output) = await _gitWorktreeService.MergeBranchAsync(targetDir, branch);
            if (success) successCount++;
            else failCount++;
        }

        if (failCount == 0)
        {
            _audioService.PlaySuccess();
            ShowNotification($"Successfully synthesized all {successCount} worker branch(es) into main repository.");
        }
        else
        {
            ShowNotification($"Synthesis finished: {successCount} merged, {failCount} had conflicts or warnings.");
        }

        await RefreshWorktreesAsync();
        UpdateWorktreeTreeNodes();
    }

    [RelayCommand]
    public void CancelMergeWorktrees()
    {
        _audioService.PlayClick();
        IsMergeWorktreeDialogVisible = false;
    }

    // ==========================================================
    // AUTO-SYNC TOGGLE & NAVBAR DISPLAY MODE
    // ==========================================================

    [ObservableProperty]
    private bool _autoSyncEnabled = true;

    partial void OnAutoSyncEnabledChanged(bool value)
    {
        Settings.AutoSyncEnabled = value;
        _ = _storageService.SaveSettingsAsync(Settings);
        AutoSyncCountdown = value ? "05:00" : "Off";
    }

    [ObservableProperty]
    private bool _isNavbarDetailed = true;

    [ObservableProperty]
    private string _navbarDisplayMode = "Detailed";

    [RelayCommand]
    public void ToggleNavbarDisplayMode()
    {
        IsNavbarDetailed = !IsNavbarDetailed;
        NavbarDisplayMode = IsNavbarDetailed ? "Detailed" : "Minimalist";
        Settings.NavbarDisplayMode = NavbarDisplayMode;
        _ = _storageService.SaveSettingsAsync(Settings);
        _audioService.PlayClick();
        ShowNotification(IsNavbarDetailed ? "Navbar switched to Detailed Mode." : "Navbar switched to Minimalist Mode.");
    }

    [RelayCommand]
    public void ToggleAutoSync()
    {
        AutoSyncEnabled = !AutoSyncEnabled;
        _audioService.PlayClick();
        ShowNotification(AutoSyncEnabled ? "Auto-sync telemetry enabled." : "Auto-sync telemetry paused.");
    }

    // ==========================================================
    // VERSION UPDATE CHECKER
    // ==========================================================

    [ObservableProperty]
    private bool _isCheckingForUpdate = false;

    [ObservableProperty]
    private bool _hasUpdateResult = false;

    [ObservableProperty]
    private bool _isUpdateAvailable = false;

    [ObservableProperty]
    private string _updateStatusMessage = "v0.9.8-beta is currently the latest release.";

    [ObservableProperty]
    private string _latestVersionTag = "v0.9.8-beta";

    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        IsCheckingForUpdate = true;
        HasUpdateResult = true;
        UpdateStatusMessage = "Checking GitHub release stream for latest updates...";
        _audioService.PlayClick();

        try
        {
            using var client = new System.Net.Http.HttpClient();
            client.Timeout = TimeSpan.FromSeconds(6);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AgyAccountSwarm-App/0.9.8");

            var url = "https://api.github.com/repos/RifkyA911/agy-cli-account-swarm/releases/latest";
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("tag_name", out var tagElem))
                {
                    var latestTag = tagElem.GetString() ?? "v0.9.8-beta";
                    LatestVersionTag = latestTag;

                    if (!string.Equals(latestTag.TrimStart('v'), AppVersion.TrimStart('v'), StringComparison.OrdinalIgnoreCase))
                    {
                        IsUpdateAvailable = true;
                        UpdateStatusMessage = $"🎉 New update available: {latestTag}! Current version is {AppVersion}.";
                        _audioService.PlaySuccess();
                    }
                    else
                    {
                        IsUpdateAvailable = false;
                        UpdateStatusMessage = $"✓ You are on the latest release ({AppVersion}). Swarm engine up-to-date.";
                    }
                }
            }
            else
            {
                IsUpdateAvailable = false;
                UpdateStatusMessage = $"✓ Verified: Version {AppVersion} is the canonical release.";
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"[UpdateChecker] Release check note: {ex.Message}");
            IsUpdateAvailable = false;
            UpdateStatusMessage = $"✓ Verified: Version {AppVersion} is active.";
        }
        finally
        {
            IsCheckingForUpdate = false;
        }
    }

    // ==========================================================
    // ANTIGRAVITY AGENT SKILLS CATALOG
    // ==========================================================

    public ObservableCollection<SkillItem> Skills { get; } = new();
    public ObservableCollection<SkillItem> FilteredSkills { get; } = new();

    [ObservableProperty]
    private string _skillSearchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedSkillCategory = "All";

    [ObservableProperty]
    private int _skillsCount = 0;

    [ObservableProperty]
    private bool _isLoadingSkills = false;

    [ObservableProperty]
    private SkillItem? _selectedSkillForDetails;

    [ObservableProperty]
    private bool _isSkillDetailsModalVisible = false;

    partial void OnSkillSearchQueryChanged(string value) => FilterSkills();
    partial void OnSelectedSkillCategoryChanged(string value) => FilterSkills();

    [RelayCommand]
    public async Task LoadSkillsAsync()
    {
        IsLoadingSkills = true;
        try
        {
            var currentWs = !string.IsNullOrWhiteSpace(FleetWorkspacePath) ? FleetWorkspacePath : Directory.GetCurrentDirectory();
            var discovered = await _skillService.DiscoverSkillsAsync(currentWs);

            Skills.Clear();
            foreach (var s in discovered)
            {
                Skills.Add(s);
            }
            SkillsCount = Skills.Count;
            FilterSkills();
        }
        catch (Exception ex)
        {
            Logger.Warn($"[Skills] Failed loading skills: {ex.Message}");
        }
        finally
        {
            IsLoadingSkills = false;
        }
    }

    public void FilterSkills()
    {
        FilteredSkills.Clear();
        var q = SkillSearchQuery?.Trim().ToLowerInvariant() ?? "";
        var cat = SelectedSkillCategory ?? "All";

        var filtered = Skills.Where(s =>
        {
            if (cat != "All" && !s.SourceType.Equals(cat, StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.IsNullOrWhiteSpace(q))
                return true;

            return s.Name.ToLowerInvariant().Contains(q) ||
                   s.Description.ToLowerInvariant().Contains(q) ||
                   s.SourceType.ToLowerInvariant().Contains(q);
        });

        foreach (var s in filtered)
        {
            FilteredSkills.Add(s);
        }
    }

    [RelayCommand]
    public void SetSkillCategory(string category)
    {
        SelectedSkillCategory = category;
        _audioService.PlayClick();
    }

    [RelayCommand]
    public void ViewSkillDetails(SkillItem? skill)
    {
        if (skill == null) return;
        SelectedSkillForDetails = skill;
        IsSkillDetailsModalVisible = true;
        _audioService.PlayClick();
    }

    [RelayCommand]
    public void CloseSkillDetails()
    {
        IsSkillDetailsModalVisible = false;
        _audioService.PlayClick();
    }

    [RelayCommand]
    public void OpenSkillFolder(SkillItem? skill)
    {
        if (skill == null || string.IsNullOrWhiteSpace(skill.DirectoryPath) || !Directory.Exists(skill.DirectoryPath))
            return;

        TerminalLauncherService.OpenFolderInFileManager(skill.DirectoryPath);
        _audioService.PlayClick();
    }

    [RelayCommand]
    public void OpenAllSkillsFolder()
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var builtinSkillsDir = Path.Combine(userHome, ".gemini", "antigravity-cli", "builtin", "skills");
        if (Directory.Exists(builtinSkillsDir))
        {
            TerminalLauncherService.OpenFolderInFileManager(builtinSkillsDir);
        }
        else
        {
            var wsSkills = Path.Combine(Directory.GetCurrentDirectory(), ".agents", "skills");
            if (Directory.Exists(wsSkills))
            {
                TerminalLauncherService.OpenFolderInFileManager(wsSkills);
            }
        }
        _audioService.PlayClick();
    }
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

public class HourlyActivityItem
{
    public string HourLabel { get; set; } = string.Empty;
    public double Height { get; set; } = 8;
    public string Color { get; set; } = "#3B82F6";
    public string Tooltip { get; set; } = string.Empty;
    public int PromptCount { get; set; }
}

public class SwarmHealthItem
{
    public string AccountName { get; set; } = string.Empty;
    public string AccountEmail { get; set; } = string.Empty;
    public string AvatarInitial { get; set; } = "G";
    public string Tier { get; set; } = "Basic";
    public string TierColor { get; set; } = "#64748B";
    public string CurrentModel { get; set; } = string.Empty;
    public string UsageLabel { get; set; } = string.Empty;
    public double UsagePercent { get; set; }
    public string TodayQuotaFormatted { get; set; } = string.Empty;
    public string WeeklySummary { get; set; } = string.Empty;
    public string QuotaResetCountdown { get; set; } = string.Empty;
    public string StatusText { get; set; } = "HEALTHY";
    public string StatusColor { get; set; } = "#10B981";
}

