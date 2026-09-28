using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgyAccountSwarm.Models;
using AgyAccountSwarm.Services;

namespace AgyAccountSwarm.ViewModels;

public partial class ProfileItemViewModel : ObservableObject
{
    private readonly ITerminalLauncherService _launcherService;
    private readonly IAuthDetectorService _authDetector;
    private readonly IAudioService _audioService;
    private readonly IProfileDoctorService _doctorService;

    public AccountProfile Profile { get; }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _description;

    [ObservableProperty]
    private string _colorTag;

    [ObservableProperty]
    private string? _customProfilePath;

    [ObservableProperty]
    private string? _defaultWorkspace;

    [ObservableProperty]
    private string? _extraArguments;

    [ObservableProperty]
    private bool _isSelectedForSwarm;

    [ObservableProperty]
    private ProfileAuthStatus _authStatus = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isDetailsExpanded;

    partial void OnIsDetailsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(DetailsToggleText));
    }

    public string DetailsToggleText => IsDetailsExpanded ? "Hide Details & Diagnostics ▴" : "Show Details & Diagnostics ▾";

    [RelayCommand]
    public void ToggleDetails()
    {
        _audioService.PlayClick();
        IsDetailsExpanded = !IsDetailsExpanded;
    }

    public event Action<ProfileItemViewModel>? OnEditRequested;
    public event Action<ProfileItemViewModel>? OnDeleteRequested;
    public event Action<ProfileItemViewModel>? OnDuplicateRequested;
    public event Action<string>? OnNotificationRequested;

    public ProfileItemViewModel(
        AccountProfile profile,
        ITerminalLauncherService launcherService,
        IAuthDetectorService authDetector,
        IAudioService audioService,
        IProfileDoctorService? doctorService = null)
    {
        Profile = profile;
        _launcherService = launcherService;
        _authDetector = authDetector;
        _audioService = audioService;
        _doctorService = doctorService ?? new ProfileDoctorService();

        _name = profile.Name;
        _description = profile.Description;
        _colorTag = profile.ColorTag;
        _customProfilePath = profile.CustomProfilePath;
        _defaultWorkspace = profile.DefaultWorkspace;
        _extraArguments = profile.ExtraArguments;
        _isSelectedForSwarm = profile.IsSelectedForSwarm;
        _authStatus = profile.AuthStatus;
        _tier = profile.Tier;
        _preferredModel = profile.PreferredModel;
        _quotaLimit = profile.QuotaLimit;
        _isQuotaExhausted = profile.IsQuotaExhausted;

        // Initialize available conversation sessions
        try
        {
            var sessions = _authDetector.GetAvailableSessions(profile);
            foreach (var s in sessions)
            {
                AvailableSessions.Add(s);
            }
            if (AvailableSessions.Count > 0)
            {
                SelectedSession = AvailableSessions[0];
            }
        }
        catch { }
    }

    public ObservableCollection<ConversationSessionItem> AvailableSessions { get; } = new();

    [ObservableProperty]
    private ConversationSessionItem? _selectedSession;

    [ObservableProperty]
    private string _tier = "Pro";

    [ObservableProperty]
    private string _preferredModel = "gemini-2.5-flash";

    [ObservableProperty]
    private int _quotaLimit = 500;

    [ObservableProperty]
    private bool _isQuotaExhausted;

    public string EffectiveProfilePath => Profile.GetEffectiveProfileDirectory().Trim().TrimEnd('\\', '/');

    public string DisplayWorkspace =>
        string.IsNullOrWhiteSpace(DefaultWorkspace)
            ? "(User Home Directory)"
            : DefaultWorkspace;

    public string CurrentModel
    {
        get
        {
            if (AuthStatus.Status is AuthStatusType.NeedsLogin or AuthStatusType.NotInitialized)
            {
                return "Not Connected (Login Required)";
            }
            if (AuthStatus.Status == AuthStatusType.QuotaExhausted)
            {
                return $"{AuthStatus.CurrentModel ?? PreferredModel} [EXHAUSTED]";
            }
            return !string.IsNullOrWhiteSpace(AuthStatus.CurrentModel)
                ? AuthStatus.CurrentModel
                : (!string.IsNullOrWhiteSpace(PreferredModel) ? PreferredModel : "gemini-2.5-flash");
        }
    }

    public bool IsModelActive => AuthStatus.Status is AuthStatusType.Authenticated or AuthStatusType.QuotaExhausted;

    public string TierBadgeText
    {
        get
        {
            if (AuthStatus.Status is AuthStatusType.NeedsLogin or AuthStatusType.NotInitialized)
            {
                return "Pending Login";
            }
            if (!string.IsNullOrWhiteSpace(Tier) && 
                !Tier.Equals("Unverified", StringComparison.OrdinalIgnoreCase) && 
                !Tier.Equals("Basic", StringComparison.OrdinalIgnoreCase))
            {
                return Tier;
            }
            return !string.IsNullOrWhiteSpace(AuthStatus.DetectedTier) ? AuthStatus.DetectedTier : "Pro";
        }
    }

    public string TierBadgeBackground
    {
        get
        {
            if (AuthStatus.Status is AuthStatusType.NeedsLogin or AuthStatusType.NotInitialized)
            {
                return "#475569"; // Neutral slate for unauthenticated accounts
            }
            return TierBadgeText.ToLowerInvariant() switch
            {
                "ultra" => "#D97706", // Amber gold
                "pro" => "#7C3AED",   // Vivid purple
                "plus" => "#0284C7",  // Light blue
                _ => "#64748B"        // Slate / Basic
            };
        }
    }

    public string TierBadgeBorder => TierBadgeText.ToLowerInvariant() switch
    {
        "ultra" => "#F59E0B",
        "pro" => "#8B5CF6",
        "plus" => "#38BDF8",
        _ => "#94A3B8"
    };

    public bool HasExhaustedQuota => IsQuotaExhausted || AuthStatus.Status == AuthStatusType.QuotaExhausted || UsagePercentage >= 100.0;

    public string QuotaStatusText => HasExhaustedQuota
        ? "QUOTA EXHAUSTED"
        : (UsagePercentage >= 85.0 ? "CRITICAL LIMIT" : (UsagePercentage >= 65.0 ? "NEARING LIMIT" : "HEALTHY"));

    public string QuotaStatusColor => HasExhaustedQuota
        ? "#EF4444"
        : (UsagePercentage >= 85.0 ? "#EF4444" : (UsagePercentage >= 65.0 ? "#F59E0B" : "#10B981"));

    public string UsageLabel => AuthStatus.UsageLabel;
    public int SessionTurnsCount => AuthStatus.SessionTurnsCount;
    public string SessionUsageLabel => AuthStatus.SessionUsageLabel;
    
    public double UsagePercentage
    {
        get => AuthStatus.UsagePercentage;
        set
        {
            AuthStatus.UsagePercentage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasExhaustedQuota));
            OnPropertyChanged(nameof(QuotaStatusText));
            OnPropertyChanged(nameof(QuotaStatusColor));
        }
    }

    public string StatusBadgeColor => AuthStatus.Status switch
    {
        AuthStatusType.Authenticated => "#10B981", // Emerald
        AuthStatusType.QuotaExhausted => "#EF4444",// Red
        AuthStatusType.NeedsLogin => "#F59E0B",    // Amber
        AuthStatusType.Error => "#EF4444",         // Red
        _ => "#6B7280"                             // Neutral gray
    };

    public string StatusBadgeText => AuthStatus.Status switch
    {
        AuthStatusType.Authenticated => string.IsNullOrEmpty(AuthStatus.AccountEmail)
            ? "Authenticated"
            : AuthStatus.AccountEmail,
        AuthStatusType.QuotaExhausted => "Quota Exhausted",
        AuthStatusType.NeedsLogin => "Needs Login",
        AuthStatusType.Error => "Auth Error",
        _ => "Not Initialized"
    };

    public string AvatarInitial
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Name)) return Name.Substring(0, 1).ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(AccountEmail)) return AccountEmail.Substring(0, 1).ToUpperInvariant();
            return "G";
        }
    }

    public string? AccountEmail => AuthStatus.AccountEmail;
    public string? AvatarUrl
    {
        get
        {
            if (!string.IsNullOrEmpty(AuthStatus.LocalAvatarPath) && System.IO.File.Exists(AuthStatus.LocalAvatarPath))
                return AuthStatus.LocalAvatarPath;
            if (!string.IsNullOrEmpty(AuthStatus.AvatarUrl))
            {
                if (System.IO.File.Exists(AuthStatus.AvatarUrl)) return AuthStatus.AvatarUrl;
                if (AuthStatus.AvatarUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return AuthStatus.AvatarUrl;
            }
            return null;
        }
    }

    public ImageSource? AvatarImageSource
    {
        get
        {
            var path = AvatarUrl;
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                if (File.Exists(path))
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.DecodePixelWidth = 256;
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        var ms = new MemoryStream();
                        fs.CopyTo(ms);
                        ms.Position = 0;
                        bi.StreamSource = ms;
                        bi.EndInit();
                    }
                    bi.Freeze();
                    return bi;
                }
                else if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.UriSource = uri;
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.DecodePixelWidth = 256;
                    bi.EndInit();
                    bi.Freeze();
                    return bi;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"[ProfileItem] Failed loading avatar image from '{path}': {ex.Message}");
            }
            return null;
        }
    }

    public bool HasAvatarUrl => AvatarImageSource != null;


    public int DailyQuotaLimit => AuthStatus.DailyQuotaLimit > 0 ? AuthStatus.DailyQuotaLimit : AuthDetectorService.GetDailyQuotaForTier(TierBadgeText);
    public int TodayTurnsCount => AuthStatus.TodayTurnsCount;
    public int TotalTurnsCount => AuthStatus.TotalTurnsCount;
    public long TodayTokensEstimated => AuthStatus.TodayTokensEstimated;
    public long DailyTokensLimit => AuthStatus.DailyTokensLimit;
    public string QuotaResetCountdown => AuthStatus.QuotaResetCountdown;
    public string TodayQuotaFormatted => $"{TodayTurnsCount:N0} / {DailyQuotaLimit:N0} prompts today ({UsagePercentage:F1}%)";
    public string TodayTokensFormatted => $"{TodayTokensEstimated / 1000:N0}K / {DailyTokensLimit / 1000:N0}K est. tokens";
    public string TierDailySummary => $"{TierBadgeText} Tier • {AccountRoleText}";
    public string AccountRoleText => IsDefaultProfile ? "Primary System Profile" : "Sandboxed Worker Profile";
    public string HeadroomSummaryText => $"{Math.Max(0, 100 - (int)UsagePercentage)}% headroom remaining";
    public string SessionsCountText => AvailableSessions.Count <= 1
        ? "1 active session"
        : $"{AvailableSessions.Count} sessions detected";

    public bool IsDefaultProfile =>
        Profile.IsMainDefaultProfile() ||
        string.Equals(Profile.Id, "main", StringComparison.OrdinalIgnoreCase) ||
        Name.StartsWith("Default", StringComparison.OrdinalIgnoreCase) ||
        Name.Contains("(Main Account)", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Name, "Primary (Default)", StringComparison.OrdinalIgnoreCase);

    public bool CanDelete => !IsDefaultProfile;

    // Burn-Rate & Quota Exhaustion Forecasting
    public double BurnRatePromptsPerHour
    {
        get
        {
            var nowUtc = DateTime.UtcNow;
            double hoursToday = Math.Max(0.1, nowUtc.TimeOfDay.TotalHours);
            return TodayTurnsCount / hoursToday;
        }
    }

    public string BurnRateFormatted => TodayTurnsCount > 0
        ? $"{BurnRatePromptsPerHour.ToString("F1", CultureInfo.InvariantCulture)} req/hr"
        : "0.0 req/hr (Idle)";

    public int RemainingPrompts => Math.Max(0, DailyQuotaLimit - TodayTurnsCount);

    public double RemainingQuotaPercent => DailyQuotaLimit > 0
        ? (Math.Max(0.0, DailyQuotaLimit - TodayTurnsCount) / (double)DailyQuotaLimit) * 100.0
        : 100.0;

    public bool IsNearQuotaExhausted => RemainingQuotaPercent <= 20.0 || HasExhaustedQuota;

    public string QuotaToastAlertText
    {
        get
        {
            if (HasExhaustedQuota || RemainingPrompts == 0)
                return "⚠️ Daily quota reached: 100% of today's allowance consumed";
            if (RemainingQuotaPercent <= 20.0)
                return $"⚠️ Low quota alert: {RemainingQuotaPercent.ToString("F0", CultureInfo.InvariantCulture)}% remaining ({RemainingPrompts:N0} requests left)";
            return string.Empty;
        }
    }

    public string ExhaustionForecastText
    {
        get
        {
            if (HasExhaustedQuota || RemainingPrompts == 0)
                return "Quota fully depleted for today";
            if (TodayTurnsCount == 0)
                return "No activity today (Full headroom)";

            double rate = BurnRatePromptsPerHour;
            if (rate <= 0.01)
                return "Steady velocity (Low consumption)";

            double hoursRemaining = RemainingPrompts / rate;
            if (hoursRemaining < 1.0)
            {
                int mins = Math.Max(1, (int)(hoursRemaining * 60));
                return $"At current velocity, quota depletes in ~{mins} mins";
            }
            return $"At current velocity, quota depletes in ~{hoursRemaining.ToString("F1", CultureInfo.InvariantCulture)} hrs";
        }
    }

    public string ExhaustionBadgeColor
    {
        get
        {
            if (HasExhaustedQuota || RemainingQuotaPercent <= 10.0) return "#EF4444";
            if (RemainingQuotaPercent <= 20.0) return "#F59E0B";
            return "#10B981";
        }
    }

    [ObservableProperty]
    private bool _isDoctorRunning;

    partial void OnIsDoctorRunningChanged(bool value) => OnPropertyChanged(nameof(DoctorButtonText));

    public string DoctorButtonText => IsDoctorRunning ? "Diagnosing..." : "Profile Doctor";

    [ObservableProperty]
    private bool _isDoctorReportExpanded;

    [ObservableProperty]
    private ProfileDoctorReport? _doctorReport;

    public bool HasDoctorReport => DoctorReport != null;
    public string DoctorSummaryPill => DoctorReport == null ? "Not Checked" : DoctorReport.OverallStatusText;
    public string DoctorSummaryColor => DoctorReport == null ? "#6B7280" : DoctorReport.OverallStatusColor;

    [RelayCommand]
    public async Task RunDoctorAsync()
    {
        _audioService.PlayClick();
        IsDoctorRunning = true;
        try
        {
            DoctorReport = await _doctorService.DiagnoseProfileAsync(Profile);
            IsDoctorReportExpanded = true;
            OnPropertyChanged(nameof(HasDoctorReport));
            OnPropertyChanged(nameof(DoctorSummaryPill));
            OnPropertyChanged(nameof(DoctorSummaryColor));
            OnNotificationRequested?.Invoke($"Health diagnosis completed for '{Name}': {DoctorReport.OverallStatusText}");
        }
        catch (Exception ex)
        {
            Logger.Error($"[ProfileItemViewModel] Doctor failed for '{Name}'", ex);
            OnNotificationRequested?.Invoke($"Failed to diagnose '{Name}': {ex.Message}");
        }
        finally
        {
            IsDoctorRunning = false;
        }
    }

    [RelayCommand]
    public async Task CleanLocksAsync()
    {
        _audioService.PlayClick();
        try
        {
            int cleaned = await _doctorService.CleanStuckLocksAsync(Profile);
            OnNotificationRequested?.Invoke($"Successfully cleared {cleaned} lock file(s) for '{Name}'");
            await RunDoctorAsync();
        }
        catch (Exception ex)
        {
            Logger.Error($"[ProfileItemViewModel] CleanLocks failed for '{Name}'", ex);
        }
    }

    [RelayCommand]
    public async Task TrustWorkspaceAsync()
    {
        _audioService.PlayClick();
        try
        {
            bool ok = await _doctorService.AddWorkspaceToTrustedAsync(Profile);
            if (ok)
            {
                OnNotificationRequested?.Invoke($"Workspace '{DisplayWorkspace}' registered to trustedWorkspaces!");
                await RunDoctorAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"[ProfileItemViewModel] TrustWorkspace failed for '{Name}'", ex);
        }
    }

    [RelayCommand]
    public void ToggleDoctorReport()
    {
        _audioService.PlayClick();
        IsDoctorReportExpanded = !IsDoctorReportExpanded;
    }

    // Weekly Quota Indicators
    public int WeeklyQuotaLimit => AuthStatus.WeeklyQuotaLimit > 0 ? AuthStatus.WeeklyQuotaLimit : (DailyQuotaLimit * 5);
    public int WeeklyTurnsCount => AuthStatus.WeeklyTurnsCount;
    public double WeeklyUsagePercentage => AuthStatus.WeeklyUsagePercentage;
    public double WeeklyRemainingPercentage => AuthStatus.WeeklyRemainingPercentage;
    public string WeeklyRemainingLabel => AuthStatus.WeeklyRemainingLabel;
    public string WeeklyRemainingFormatted => $"{WeeklyRemainingPercentage:F0}% remaining";
    public string WeeklySummary => $"Weekly Quota: {WeeklyRemainingPercentage:F0}% remaining ({Math.Max(0, WeeklyQuotaLimit - WeeklyTurnsCount):N0} / {WeeklyQuotaLimit:N0} left)";

    // Context Metrics
    public long ModelContextLimit => AuthStatus.ModelContextLimit;
    public long EstimatedContextTokens => AuthStatus.EstimatedContextTokens;
    public double ContextUsagePercentage => AuthStatus.ContextUsagePercentage;
    public string ContextWindowLabel => AuthStatus.ContextWindowLabel;
    public string ContextUsageSummary => AuthStatus.ContextUsageSummary;
    public string ContextHeadroomSummary => AuthStatus.ContextHeadroomSummary;

    // Foldable Section States for Account Cards
    [ObservableProperty]
    private bool _isDailyQuotaExpanded = true;

    [ObservableProperty]
    private bool _isModelQuotasExpanded = true;

    [ObservableProperty]
    private bool _isCliInspectorExpanded = false;

    [RelayCommand]
    public void ToggleDailyQuotaSection()
    {
        _audioService.PlayClick();
        IsDailyQuotaExpanded = !IsDailyQuotaExpanded;
    }

    [RelayCommand]
    public void ToggleModelQuotasSection()
    {
        _audioService.PlayClick();
        IsModelQuotasExpanded = !IsModelQuotasExpanded;
    }

    [RelayCommand]
    public void ToggleCliInspectorSection()
    {
        _audioService.PlayClick();
        IsCliInspectorExpanded = !IsCliInspectorExpanded;
    }

    // Antigravity Model Group Quotas (Gemini, Claude & GPT)
    public double GeminiWeeklyRemainingPercent => AuthStatus.GeminiWeeklyRemainingPercent;
    public string GeminiWeeklyRefreshesIn => AuthStatus.GeminiWeeklyRefreshesIn;
    public double Gemini5HourRemainingPercent => AuthStatus.Gemini5HourRemainingPercent;
    public string Gemini5HourRefreshesIn => AuthStatus.Gemini5HourRefreshesIn;

    public double ClaudeGptWeeklyRemainingPercent => AuthStatus.ClaudeGptWeeklyRemainingPercent;
    public string ClaudeGptWeeklyRefreshesIn => AuthStatus.ClaudeGptWeeklyRefreshesIn;
    public double ClaudeGpt5HourRemainingPercent => AuthStatus.ClaudeGpt5HourRemainingPercent;
    public string ClaudeGpt5HourRefreshesIn => AuthStatus.ClaudeGpt5HourRefreshesIn;

    public string GeminiWeeklyBarColor => GetRemainingColor(GeminiWeeklyRemainingPercent);
    public string Gemini5HourBarColor => GetRemainingColor(Gemini5HourRemainingPercent);
    public string ClaudeGptWeeklyBarColor => GetRemainingColor(ClaudeGptWeeklyRemainingPercent);
    public string ClaudeGpt5HourBarColor => GetRemainingColor(ClaudeGpt5HourRemainingPercent);
    public string DailyUsageBarColor => UsagePercentage >= 85.0 ? "#EF4444" : (UsagePercentage >= 65.0 ? "#F59E0B" : "#10B981");

    private static string GetRemainingColor(double remaining) =>
        remaining > 50.0 ? "#10B981" : (remaining > 20.0 ? "#F59E0B" : "#EF4444");

    // CLI Inspection & Drawer
    [ObservableProperty]
    private bool _isInspectorOpen;

    [ObservableProperty]
    private string _inspectorTitle = "AGY CLI /usage";

    [ObservableProperty]
    private string _inspectorContent = string.Empty;

    [ObservableProperty]
    private string _inspectorMode = "usage";

    [RelayCommand]
    public void ToggleUsageInspection()
    {
        _audioService.PlayClick();
        if (IsInspectorOpen && InspectorMode == "usage")
        {
            IsInspectorOpen = false;
        }
        else
        {
            InspectorTitle = $"⚡ AGY CLI /usage: {Name} ({AuthStatus.AccountEmail ?? "Local User"})";
            InspectorContent = string.IsNullOrEmpty(AuthStatus.InspectionUsageText)
                ? "No usage telemetry recorded yet."
                : AuthStatus.InspectionUsageText;
            InspectorMode = "usage";
            IsInspectorOpen = true;
        }
    }

    [RelayCommand]
    public void ToggleContextInspection()
    {
        _audioService.PlayClick();
        if (IsInspectorOpen && InspectorMode == "context")
        {
            IsInspectorOpen = false;
        }
        else
        {
            InspectorTitle = $"🧠 AGY CLI /context: {CurrentModel} ({ContextWindowLabel})";
            InspectorContent = string.IsNullOrEmpty(AuthStatus.InspectionContextText)
                ? "No context telemetry recorded yet."
                : AuthStatus.InspectionContextText;
            InspectorMode = "context";
            IsInspectorOpen = true;
        }
    }

    [RelayCommand]
    public void CloseInspector()
    {
        _audioService.PlayClick();
        IsInspectorOpen = false;
    }

    public async Task RefreshAuthStatusAsync()
    {
        IsBusy = true;
        try
        {
            AuthStatus = await _authDetector.DetectAuthStatusAsync(Profile);
            Profile.AuthStatus = AuthStatus;

            // Refresh available conversation sessions
            var sessions = _authDetector.GetAvailableSessions(Profile);
            AvailableSessions.Clear();
            foreach (var s in sessions)
            {
                AvailableSessions.Add(s);
            }
            if (SelectedSession == null && AvailableSessions.Count > 0)
            {
                SelectedSession = AvailableSessions[0];
            }

            OnPropertyChanged(nameof(StatusBadgeColor));
            OnPropertyChanged(nameof(StatusBadgeText));
            OnPropertyChanged(nameof(CurrentModel));
            OnPropertyChanged(nameof(IsModelActive));
            OnPropertyChanged(nameof(TierBadgeText));
            OnPropertyChanged(nameof(TierBadgeBackground));
            OnPropertyChanged(nameof(TierBadgeBorder));
            OnPropertyChanged(nameof(HasExhaustedQuota));
            OnPropertyChanged(nameof(QuotaStatusText));
            OnPropertyChanged(nameof(QuotaStatusColor));
            OnPropertyChanged(nameof(UsageLabel));
            OnPropertyChanged(nameof(SessionTurnsCount));
            OnPropertyChanged(nameof(SessionUsageLabel));
            OnPropertyChanged(nameof(UsagePercentage));
            OnPropertyChanged(nameof(AvatarInitial));
            OnPropertyChanged(nameof(AccountEmail));
            OnPropertyChanged(nameof(AvatarUrl));
            OnPropertyChanged(nameof(AvatarImageSource));
            OnPropertyChanged(nameof(HasAvatarUrl));
            OnPropertyChanged(nameof(DailyQuotaLimit));
            OnPropertyChanged(nameof(TodayTurnsCount));
            OnPropertyChanged(nameof(TotalTurnsCount));
            OnPropertyChanged(nameof(TodayTokensEstimated));
            OnPropertyChanged(nameof(DailyTokensLimit));
            OnPropertyChanged(nameof(QuotaResetCountdown));
            OnPropertyChanged(nameof(TodayQuotaFormatted));
            OnPropertyChanged(nameof(TodayTokensFormatted));
            OnPropertyChanged(nameof(TierDailySummary));
            OnPropertyChanged(nameof(WeeklyQuotaLimit));
            OnPropertyChanged(nameof(WeeklyTurnsCount));
            OnPropertyChanged(nameof(WeeklyUsagePercentage));
            OnPropertyChanged(nameof(WeeklyRemainingPercentage));
            OnPropertyChanged(nameof(WeeklyRemainingLabel));
            OnPropertyChanged(nameof(WeeklyRemainingFormatted));
            OnPropertyChanged(nameof(WeeklySummary));
            OnPropertyChanged(nameof(ModelContextLimit));
            OnPropertyChanged(nameof(EstimatedContextTokens));
            OnPropertyChanged(nameof(ContextUsagePercentage));
            OnPropertyChanged(nameof(ContextWindowLabel));
            OnPropertyChanged(nameof(ContextUsageSummary));
            OnPropertyChanged(nameof(ContextHeadroomSummary));
            OnPropertyChanged(nameof(GeminiWeeklyRemainingPercent));
            OnPropertyChanged(nameof(GeminiWeeklyRefreshesIn));
            OnPropertyChanged(nameof(Gemini5HourRemainingPercent));
            OnPropertyChanged(nameof(Gemini5HourRefreshesIn));
            OnPropertyChanged(nameof(ClaudeGptWeeklyRemainingPercent));
            OnPropertyChanged(nameof(ClaudeGptWeeklyRefreshesIn));
            OnPropertyChanged(nameof(ClaudeGpt5HourRemainingPercent));
            OnPropertyChanged(nameof(ClaudeGpt5HourRefreshesIn));
            OnPropertyChanged(nameof(GeminiWeeklyBarColor));
            OnPropertyChanged(nameof(Gemini5HourBarColor));
            OnPropertyChanged(nameof(ClaudeGptWeeklyBarColor));
            OnPropertyChanged(nameof(ClaudeGpt5HourBarColor));
            OnPropertyChanged(nameof(DailyUsageBarColor));
            OnPropertyChanged(nameof(BurnRatePromptsPerHour));
            OnPropertyChanged(nameof(BurnRateFormatted));
            OnPropertyChanged(nameof(RemainingPrompts));
            OnPropertyChanged(nameof(RemainingQuotaPercent));
            OnPropertyChanged(nameof(IsNearQuotaExhausted));
            OnPropertyChanged(nameof(QuotaToastAlertText));
            OnPropertyChanged(nameof(ExhaustionForecastText));
            OnPropertyChanged(nameof(ExhaustionBadgeColor));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task LaunchAsync(TerminalType terminal)
    {
        IsBusy = true;
        try
        {
            _audioService.PlayLaunch();
            string? sessionArgs = null;
            if (SelectedSession != null)
            {
                if (SelectedSession.IsContinueRecent)
                {
                    sessionArgs = "--continue";
                }
                else if (SelectedSession.IsSpecificConversation)
                {
                    sessionArgs = $"--conversation {SelectedSession.Id}";
                }
            }
            await _launcherService.LaunchProfileAsync(Profile, terminal, forceLoginPrompt: false, sessionArgs: sessionArgs);
            OnNotificationRequested?.Invoke($"Launched session for '{Name}' {(sessionArgs != null ? $"({sessionArgs})" : "")}");
            // Delay and refresh status in background
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                await RefreshAuthStatusAsync();
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to launch '{Name}'", ex);
            OnNotificationRequested?.Invoke($"Failed to launch '{Name}': {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void CopyCliSnippet(TerminalType terminal)
    {
        _audioService.PlayClick();
        var snippet = _launcherService.GetCliSnippet(Profile, terminal);
        System.Windows.Clipboard.SetText(snippet);
        OnNotificationRequested?.Invoke($"Copied CLI command for '{Name}' to clipboard");
    }

    [RelayCommand]
    public void OpenProfileFolder()
    {
        _audioService.PlayClick();
        _launcherService.OpenProfileFolder(Profile);
    }

    [RelayCommand]
    public void OpenWorkspaceFolder()
    {
        _audioService.PlayClick();
        _launcherService.OpenWorkspaceFolder(Profile);
    }

    [RelayCommand]
    public void RequestEdit()
    {
        _audioService.PlayClick();
        OnEditRequested?.Invoke(this);
    }

    [RelayCommand]
    public void RequestDuplicate()
    {
        _audioService.PlayClick();
        OnDuplicateRequested?.Invoke(this);
    }

    [RelayCommand]
    public void RequestDelete()
    {
        _audioService.PlayClick();
        OnDeleteRequested?.Invoke(this);
    }

    public void SyncBackToModel()
    {
        Profile.Name = Name;
        Profile.Description = Description;
        Profile.ColorTag = ColorTag;
        Profile.CustomProfilePath = CustomProfilePath;
        Profile.DefaultWorkspace = DefaultWorkspace;
        Profile.ExtraArguments = ExtraArguments;
        Profile.IsSelectedForSwarm = IsSelectedForSwarm;
        Profile.Tier = Tier;
        Profile.PreferredModel = PreferredModel;
        Profile.QuotaLimit = QuotaLimit;
        Profile.IsQuotaExhausted = IsQuotaExhausted;
    }
}
