using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.ViewModels;

public partial class ProfileEditViewModel : ObservableObject
{
    [ObservableProperty]
    private string _dialogTitle = "Add Account Profile";

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _selectedColor = "#3B82F6";

    [ObservableProperty]
    private string? _customProfilePath;

    [ObservableProperty]
    private string? _defaultWorkspace;

    [ObservableProperty]
    private string? _extraArguments;

    [ObservableProperty]
    private bool _isSelectedForSwarm = true;

    [ObservableProperty]
    private string? _validationError;

    public ObservableCollection<string> ColorPresets { get; } =
    [
        "#10B981", // Emerald
        "#3B82F6", // Blue
        "#8B5CF6", // Violet
        "#EC4899", // Rose
        "#F59E0B", // Amber
        "#06B6D4", // Cyan
        "#E11D48", // Crimson
        "#64748B"  // Slate
    ];

    [ObservableProperty]
    private string _tier = "Pro";

    [ObservableProperty]
    private string _preferredModel = "gemini-2.5-flash";

    [ObservableProperty]
    private int _quotaLimit = 500;

    [ObservableProperty]
    private bool _isQuotaExhausted = false;

    public ObservableCollection<string> TierOptions { get; } = ["Basic", "Plus", "Pro", "Ultra"];
    public ObservableCollection<string> ModelOptions { get; } = ["gemini-2.5-flash", "gemini-2.5-pro", "gemini-3.8-flash", "claude-3.7-sonnet", "gpt-4o"];

    public bool IsEditMode { get; private set; }
    public AccountProfile ResultProfile { get; private set; } = new();

    public event Action<bool>? RequestClose;

    public ProfileEditViewModel()
    {
        _dialogTitle = "Add Account Profile";
        IsEditMode = false;
        _tier = "Pro";
        _preferredModel = "gemini-2.5-flash";
        _quotaLimit = 500;
    }

    public ProfileEditViewModel(AccountProfile profileToEdit)
    {
        IsEditMode = true;
        _dialogTitle = $"Edit Profile: {profileToEdit.Name}";
        ResultProfile = profileToEdit;

        _name = profileToEdit.Name;
        _description = profileToEdit.Description;
        _selectedColor = string.IsNullOrEmpty(profileToEdit.ColorTag) ? "#3B82F6" : profileToEdit.ColorTag;
        _customProfilePath = profileToEdit.CustomProfilePath;
        _defaultWorkspace = profileToEdit.DefaultWorkspace;
        _extraArguments = profileToEdit.ExtraArguments;
        _isSelectedForSwarm = profileToEdit.IsSelectedForSwarm;
        _tier = string.IsNullOrWhiteSpace(profileToEdit.Tier) ? "Pro" : profileToEdit.Tier;
        _preferredModel = string.IsNullOrWhiteSpace(profileToEdit.PreferredModel) ? "gemini-2.5-flash" : profileToEdit.PreferredModel;
        _quotaLimit = profileToEdit.QuotaLimit > 0 ? profileToEdit.QuotaLimit : 500;
        _isQuotaExhausted = profileToEdit.IsQuotaExhausted;
    }

    [RelayCommand]
    public void SelectColor(string color)
    {
        SelectedColor = color;
    }

    [RelayCommand]
    public void Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ValidationError = "Profile Name is required.";
            return;
        }

        ResultProfile.Name = Name.Trim();
        ResultProfile.Description = Description.Trim();
        ResultProfile.ColorTag = SelectedColor;
        ResultProfile.CustomProfilePath = string.IsNullOrWhiteSpace(CustomProfilePath) ? null : CustomProfilePath.Trim();
        ResultProfile.DefaultWorkspace = string.IsNullOrWhiteSpace(DefaultWorkspace) ? null : DefaultWorkspace.Trim();
        ResultProfile.ExtraArguments = string.IsNullOrWhiteSpace(ExtraArguments) ? null : ExtraArguments.Trim();
        ResultProfile.IsSelectedForSwarm = IsSelectedForSwarm;
        ResultProfile.Tier = Tier;
        ResultProfile.PreferredModel = PreferredModel;
        ResultProfile.QuotaLimit = QuotaLimit > 0 ? QuotaLimit : 500;
        ResultProfile.IsQuotaExhausted = IsQuotaExhausted;

        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
