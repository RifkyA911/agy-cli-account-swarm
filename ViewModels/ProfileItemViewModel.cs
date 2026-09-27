using System;
using System.IO;
using System.Threading.Tasks;
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

    public event Action<ProfileItemViewModel>? OnEditRequested;
    public event Action<ProfileItemViewModel>? OnDeleteRequested;
    public event Action<ProfileItemViewModel>? OnDuplicateRequested;
    public event Action<string>? OnNotificationRequested;

    public ProfileItemViewModel(
        AccountProfile profile,
        ITerminalLauncherService launcherService,
        IAuthDetectorService authDetector,
        IAudioService audioService)
    {
        Profile = profile;
        _launcherService = launcherService;
        _authDetector = authDetector;
        _audioService = audioService;

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
    }

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
    public string? AvatarUrl => AuthStatus.AvatarUrl;
    public bool HasAvatarUrl => !string.IsNullOrEmpty(AvatarUrl);

    public int DailyQuotaLimit => AuthStatus.DailyQuotaLimit > 0 ? AuthStatus.DailyQuotaLimit : AuthDetectorService.GetDailyQuotaForTier(TierBadgeText);
    public int TodayTurnsCount => AuthStatus.TodayTurnsCount;
    public int TotalTurnsCount => AuthStatus.TotalTurnsCount;
    public long TodayTokensEstimated => AuthStatus.TodayTokensEstimated;
    public long DailyTokensLimit => AuthStatus.DailyTokensLimit;
    public string QuotaResetCountdown => AuthStatus.QuotaResetCountdown;
    public string TodayQuotaFormatted => $"{TodayTurnsCount:N0} / {DailyQuotaLimit:N0} prompts today ({UsagePercentage:F1}%)";
    public string TodayTokensFormatted => $"{TodayTokensEstimated / 1000:N0}K / {DailyTokensLimit / 1000:N0}K est. tokens";
    public string TierDailySummary => $"{TierBadgeText} Tier ({DailyQuotaLimit:N0} prompts/day)";

    // Weekly Quota Indicators
    public int WeeklyQuotaLimit => AuthStatus.WeeklyQuotaLimit > 0 ? AuthStatus.WeeklyQuotaLimit : (DailyQuotaLimit * 5);
    public int WeeklyTurnsCount => AuthStatus.WeeklyTurnsCount;
    public double WeeklyUsagePercentage => AuthStatus.WeeklyUsagePercentage;
    public double WeeklyRemainingPercentage => AuthStatus.WeeklyRemainingPercentage;
    public string WeeklyRemainingLabel => AuthStatus.WeeklyRemainingLabel;
    public string WeeklyRemainingFormatted => $"{WeeklyRemainingPercentage:F0}% remaining";
    public string WeeklySummary => $"Weekly Quota: {WeeklyRemainingPercentage:F0}% remaining ({Math.Max(0, WeeklyQuotaLimit - WeeklyTurnsCount):N0} / {WeeklyQuotaLimit:N0} left)";

    public async Task RefreshAuthStatusAsync()
    {
        IsBusy = true;
        try
        {
            AuthStatus = await _authDetector.DetectAuthStatusAsync(Profile);
            Profile.AuthStatus = AuthStatus;
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
            await _launcherService.LaunchProfileAsync(Profile, terminal);
            OnNotificationRequested?.Invoke($"Launched session for '{Name}'");
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
