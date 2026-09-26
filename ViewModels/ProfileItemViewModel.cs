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
    }

    public string EffectiveProfilePath => Profile.GetEffectiveProfileDirectory().Trim().TrimEnd('\\', '/');

    public string DisplayWorkspace =>
        string.IsNullOrWhiteSpace(DefaultWorkspace)
            ? "(User Home Directory)"
            : DefaultWorkspace;

    public string CurrentModel => AuthStatus.CurrentModel ?? "Gemini 3.8 Flash";
    public string UsageLabel => AuthStatus.UsageLabel;
    
    public double UsagePercentage
    {
        get => AuthStatus.UsagePercentage;
        set
        {
            AuthStatus.UsagePercentage = value;
            OnPropertyChanged();
        }
    }

    public string StatusBadgeColor => AuthStatus.Status switch
    {
        AuthStatusType.Authenticated => "#10B981", // Emerald
        AuthStatusType.NeedsLogin => "#F59E0B",    // Amber
        AuthStatusType.Error => "#EF4444",         // Red
        _ => "#6B7280"                             // Neutral gray
    };

    public string StatusBadgeText => AuthStatus.Status switch
    {
        AuthStatusType.Authenticated => string.IsNullOrEmpty(AuthStatus.AccountEmail)
            ? "Authenticated"
            : AuthStatus.AccountEmail,
        AuthStatusType.NeedsLogin => "Needs Login",
        AuthStatusType.Error => "Auth Error",
        _ => "Not Initialized"
    };

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
            OnPropertyChanged(nameof(UsageLabel));
            OnPropertyChanged(nameof(UsagePercentage));
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
    }
}
