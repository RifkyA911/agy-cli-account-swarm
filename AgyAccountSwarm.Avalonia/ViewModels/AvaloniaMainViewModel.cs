using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
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
    private readonly IQuotaConfigService _quotaConfigService;
    private readonly IAudioService _audioService;
    private readonly IGitWorktreeService _gitWorktreeService;
    private readonly IFleetDispatcherService _fleetDispatcherService;

    private readonly DispatcherTimer _syncTimer;
    private DateTime _nextSyncTime = DateTime.UtcNow.AddMinutes(5);
    private readonly List<Process> _activeSwarmProcesses = new();

    [ObservableProperty]
    private string _currentPage = "Accounts";

    [ObservableProperty]
    private string _searchText = string.Empty;

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
    private AccountProfile? _selectedProfile;

    [ObservableProperty]
    private string _selectedDocTab = "Architecture";

    [ObservableProperty]
    private string _logLevelFilter = "All";

    [ObservableProperty]
    private string _logSearchQuery = string.Empty;

    // Fleet Dispatcher (Experimental) Properties
    [ObservableProperty]
    private string _fleetTaskObjective = string.Empty;

    [ObservableProperty]
    private string _fleetWorkspacePath = string.Empty;

    partial void OnFleetWorkspacePathChanged(string value) => UpdateWorktreeTreeNodes();

    [ObservableProperty]
    private DispatchMode _selectedDispatchMode = DispatchMode.RoleTailored;

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
    private string _fleetStatusText = "Fleet Dispatcher Ready";

    private readonly DispatcherTimer _fleetProcessWatcherTimer;

    public event Func<Task<string?>>? BrowseFolderRequested;

    public ObservableCollection<AccountProfile> Profiles { get; } = new();
    public ObservableCollection<AccountProfile> FilteredProfiles { get; } = new();
    public ObservableCollection<McpServerConfig> McpServers { get; } = new();
    public ObservableCollection<string> LogLines { get; } = new();
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
        _quotaConfigService = new QuotaConfigService();
        _audioService = new AudioService();
        _gitWorktreeService = new GitWorktreeService();
        _fleetDispatcherService = new FleetDispatcherService(_gitWorktreeService, _launcherService);

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

        // Initialize state
        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        try
        {
            Settings = await _storageService.LoadSettingsAsync();
            _audioService.IsEnabled = Settings.SoundEnabled;

            await ReloadProfilesAsync();
            await LoadMcpServersAsync();
            RefreshLogs();

            _syncTimer.Start();
            ShowNotification("Agy Swarm Avalonia initialized successfully.");
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
                Profiles.Add(p);
            }
            ApplyFilters();
            UpdateWorktreeTreeNodes();

            // Run background quick auth audit
            _ = Task.Run(async () =>
            {
                foreach (var profile in Profiles)
                {
                    try
                    {
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

                        profile.NotifyAllPropertiesChanged();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Background audit failed for {profile.Name}: {ex.Message}");
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    ApplyFilters();
                    UpdateWorktreeTreeNodes();
                });
            });
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    public void ApplyFilters()
    {
        FilteredProfiles.Clear();
        var query = SearchText.Trim().ToLowerInvariant();
        var tier = SelectedTierFilter.ToUpperInvariant();

        foreach (var p in Profiles)
        {
            bool matchesQuery = string.IsNullOrWhiteSpace(query) ||
                                p.Name.ToLowerInvariant().Contains(query) ||
                                p.Description.ToLowerInvariant().Contains(query) ||
                                (p.AuthStatus.AccountEmail?.ToLowerInvariant().Contains(query) ?? false);

            bool matchesTier = tier == "ALL" ||
                               p.Tier.Equals(tier, StringComparison.OrdinalIgnoreCase) ||
                               (tier == "WARNING" && (p.IsQuotaExhausted || p.AuthStatus.IsExhausted || p.AuthStatus.Status == AuthStatusType.NeedsLogin));

            if (matchesQuery && matchesTier)
            {
                FilteredProfiles.Add(p);
            }
        }

        UpdateSwarmStatus();
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
    public async Task QuickSyncAccountAsync(AccountProfile? profile)
    {
        if (profile == null) return;
        _audioService.PlaySync();
        ShowNotification($"Synchronizing telemetry for {profile.Name}...");

        try
        {
            var status = await _authDetectorService.DetectAuthStatusAsync(profile, allowCliSpawn: true);
            profile.AuthStatus = status;

            var usage = await AgyUsageParser.FetchUsageCachedAsync(
                profile.GetEffectiveProfileDirectory(),
                !profile.IsMainDefaultProfile(),
                Settings.CustomAgyExecutablePath,
                allowCliSpawn: true);

            if (usage != null && usage.IsSuccess)
            {
                profile.AuthStatus.GeminiWeeklyRemainingPercent = usage.GeminiWeeklyRemainingPercent ?? 0;
                profile.AuthStatus.GeminiWeeklyRefreshesIn = usage.GeminiWeeklyRefreshesIn ?? "N/A";
                profile.AuthStatus.ClaudeGptWeeklyRemainingPercent = usage.ClaudeGptWeeklyRemainingPercent ?? 0;
            }

            profile.NotifyAllPropertiesChanged();
            ApplyFilters();
            UpdateWorktreeTreeNodes();
            ShowNotification($"Synchronized {profile.Name}.");
        }
        catch (Exception ex)
        {
            ShowNotification($"Sync failed for {profile.Name}: {ex.Message}");
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
    public async Task LaunchProfileAsync(AccountProfile? profile)
    {
        if (profile == null) return;
        _audioService.PlayLaunch();
        ShowNotification($"Launching session for {profile.Name}...");

        try
        {
            var proc = await _launcherService.LaunchProfileAsync(
                profile,
                Settings.PreferredTerminal,
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
            Logger.Error($"Launch failed for {profile.Name}", ex);
            ShowNotification($"Launch failed: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task LaunchProfileCliOnlyAsync(AccountProfile? profile)
    {
        if (profile == null) return;
        _audioService.PlayLaunch();
        ShowNotification($"Opening sandbox shell for {profile.Name}...");

        try
        {
            var proc = await _launcherService.LaunchProfileAsync(
                profile,
                Settings.PreferredTerminal,
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
            Logger.Error($"CLI launch failed for {profile.Name}", ex);
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

        _audioService.PlayLaunch();
        ShowNotification($"Launching Swarm with {selected.Count} worker(s)...");

        try
        {
            var procs = await _launcherService.LaunchSwarmAsync(
                selected,
                Settings.PreferredTerminal,
                Settings.SwarmMode);

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
    public async Task ClearLocksAsync(AccountProfile? profile)
    {
        if (profile == null) return;
        _audioService.PlayClick();

        try
        {
            int cleared = await _doctorService.CleanStuckLocksAsync(profile);
            ShowNotification(cleared > 0 
                ? $"Cleared {cleared} stuck lock file(s) for {profile.Name}." 
                : $"No stuck lock files found for {profile.Name}.");
            await ReloadProfilesAsync();
        }
        catch (Exception ex)
        {
            ShowNotification($"Lock clean failed: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task DeleteProfileAsync(AccountProfile? profile)
    {
        if (profile == null) return;
        if (profile.IsMainDefaultProfile())
        {
            ShowNotification("Cannot delete the primary host profile.");
            return;
        }

        _audioService.PlayDelete();
        Profiles.Remove(profile);
        ApplyFilters();
        await _storageService.SaveProfilesAsync(Profiles);
        ShowNotification($"Deleted profile '{profile.Name}'.");
    }

    [RelayCommand]
    public async Task DuplicateProfileAsync(AccountProfile? profile)
    {
        if (profile == null) return;
        _audioService.PlayClick();

        var clone = new AccountProfile
        {
            Name = $"{profile.Name} (Copy)",
            Description = profile.Description,
            Tier = profile.Tier,
            PreferredModel = profile.PreferredModel,
            QuotaLimit = profile.QuotaLimit,
            DefaultWorkspace = profile.DefaultWorkspace,
            DangerouslySkipPermissions = profile.DangerouslySkipPermissions,
            ColorTag = profile.ColorTag,
            IsSelectedForSwarm = true
        };

        Profiles.Add(clone);
        ApplyFilters();
        await _storageService.SaveProfilesAsync(Profiles);
        ShowNotification($"Cloned profile as '{clone.Name}'.");
    }

    [RelayCommand]
    public void OpenWorkspaceFolder(AccountProfile? profile)
    {
        if (profile == null) return;
        _launcherService.OpenWorkspaceFolder(profile);
    }

    [RelayCommand]
    public void OpenProfileFolder(AccountProfile? profile)
    {
        if (profile == null) return;
        _launcherService.OpenProfileFolder(profile);
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
        await _storageService.SaveSettingsAsync(Settings);
        ShowNotification("Settings saved successfully.");
    }

    [RelayCommand]
    public void RefreshLogs()
    {
        LogLines.Clear();
        var lines = Logger.GetRecentLogLines();
        foreach (var l in lines.TakeLast(150))
        {
            LogLines.Add(l);
        }
    }

    [RelayCommand]
    public async Task ExportLogsExcelAsync()
    {
        _audioService.PlayClick();
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var filePath = Path.Combine(desktop, $"agyswarm_logs_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            var lines = Logger.GetRecentLogLines();
            await LogExcelExportService.ExportToFileAsync(filePath, lines);
            ShowNotification($"Exported logs to Desktop: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            ShowNotification($"Excel export failed: {ex.Message}");
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
        var remaining = _nextSyncTime - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            _nextSyncTime = DateTime.UtcNow.AddMinutes(5);
            _ = ReloadProfilesAsync();
            AutoSyncCountdown = "05:00";
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

        // Run Pre-flight check first
        PreflightResult = await _fleetDispatcherService.CheckPreflightResourcesAsync(FleetWorkspacePath);
        if (!PreflightResult.IsSafe)
        {
            ShowNotification($"Dispatch blocked: {PreflightResult.ErrorMessage}");
            return;
        }

        IsFleetDispatching = true;
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

            var tasks = await _fleetDispatcherService.DispatchFleetAsync(config, selected, Settings.PreferredTerminal, progress);
            DispatchedTasks.Clear();
            foreach (var t in tasks)
            {
                DispatchedTasks.Add(t);
            }
            HasDispatchedTasks = DispatchedTasks.Count > 0;

            FleetStatusText = $"Fleet Active ({tasks.Count} Dispatched)";
            ShowNotification($"Successfully dispatched objective to {tasks.Count} workers.");
            await RefreshWorktreesAsync();
            UpdateWorktreeTreeNodes();

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
        }
    }

    [RelayCommand]
    public async Task AbortFleetAsync()
    {
        _audioService.PlayDelete();
        _fleetProcessWatcherTimer.Stop();
        int killed = await _fleetDispatcherService.AbortFleetAsync(DispatchedTasks);
        FleetStatusText = "Fleet Aborted";
        ShowNotification($"Stopped {killed} dispatched worker process(es).");
        UpdateWorktreeTreeNodes();
        await RefreshWorktreesAsync();
    }

    private void WatchDispatchedProcesses()
    {
        if (DispatchedTasks.Count == 0) return;

        bool stateChanged = false;
        int activeCount = 0;

        foreach (var task in DispatchedTasks)
        {
            if (task.ProcessId.HasValue)
            {
                try
                {
                    var proc = Process.GetProcessById(task.ProcessId.Value);
                    if (proc.HasExited)
                    {
                        task.Status = proc.ExitCode == 0 ? "Completed" : $"Exited ({proc.ExitCode})";
                        task.StatusColor = proc.ExitCode == 0 ? "#8B5CF6" : "#EF4444";
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
                    task.Status = "Completed";
                    task.StatusColor = "#8B5CF6";
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

                WorktreeTreeNodes.Add(new WorktreeTreeNode
                {
                    BranchName = string.IsNullOrWhiteSpace(t.BranchName) ? $"swarm/{t.ProfileName.ToLowerInvariant().Replace(' ', '-')}" : t.BranchName,
                    WorkerName = t.ProfileName,
                    Role = t.AssignedRole,
                    Path = t.WorktreePath,
                    Status = t.Status,
                    StatusColor = color,
                    IsActive = isRunning,
                    IsLast = i == DispatchedTasks.Count - 1
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
                    IsLast = i == selected.Count - 1
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
}
