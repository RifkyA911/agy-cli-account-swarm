using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgyAccountSwarm.Models;
using AgyAccountSwarm.Services;

namespace AgyAccountSwarm.ViewModels;

public partial class ColorOptionItem : ObservableObject
{
    public string Hex { get; }
    public string Name { get; }

    [ObservableProperty]
    private bool _isSelected;

    public ColorOptionItem(string hex, string name, bool isSelected = false)
    {
        Hex = hex;
        Name = name;
        _isSelected = isSelected;
    }
}

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

    [ObservableProperty]
    private string _tier = "Basic";

    [ObservableProperty]
    private string _preferredModel = "Gemini 3.8 Flash (Medium)";

    [ObservableProperty]
    private int _quotaLimit = 100;

    [ObservableProperty]
    private bool _isQuotaExhausted = false;

    public ObservableCollection<string> TierOptions { get; } = ["Basic", "Plus", "Pro", "Ultra"];
    public ObservableCollection<string> ModelOptions { get; } = [];
    public ObservableCollection<ColorOptionItem> ColorOptions { get; } = [];
    public ObservableCollection<ColorOptionItem> ColorPresets => ColorOptions;

    public bool IsEditMode { get; private set; }
    public bool IsDefaultProfile { get; }
    public bool CanEditProfilePath => !IsDefaultProfile;
    public bool CanEditName => !IsDefaultProfile;
    public AccountProfile ResultProfile { get; private set; } = new();

    public event Action<bool>? RequestClose;

    public ProfileEditViewModel(IEnumerable<string>? availableModels = null)
    {
        _dialogTitle = "Add Account Profile";
        IsEditMode = false;
        IsDefaultProfile = false;
        _tier = "Basic";
        _quotaLimit = 100;
        _preferredModel = "Gemini 3.8 Flash (Medium)";
        _selectedColor = "#3B82F6";

        InitializeColorOptions();
        PopulateModels(availableModels);
    }

    public ProfileEditViewModel(AccountProfile profileToEdit, IEnumerable<string>? availableModels = null)
    {
        IsEditMode = true;
        ResultProfile = profileToEdit;
        IsDefaultProfile = profileToEdit.IsMainDefaultProfile();

        _dialogTitle = IsDefaultProfile ? "Configure Primary Account Profile" : $"Edit Profile: {profileToEdit.Name}";
        _name = profileToEdit.Name;
        _description = profileToEdit.Description;
        _selectedColor = string.IsNullOrEmpty(profileToEdit.ColorTag) ? "#3B82F6" : profileToEdit.ColorTag;
        _customProfilePath = profileToEdit.CustomProfilePath;
        _defaultWorkspace = profileToEdit.DefaultWorkspace;
        _extraArguments = profileToEdit.ExtraArguments;
        _isSelectedForSwarm = profileToEdit.IsSelectedForSwarm;
        _tier = string.IsNullOrWhiteSpace(profileToEdit.Tier) ? "Pro" : profileToEdit.Tier;
        _preferredModel = string.IsNullOrWhiteSpace(profileToEdit.PreferredModel) ? "Gemini 3.8 Flash (Medium)" : profileToEdit.PreferredModel;
        _quotaLimit = profileToEdit.QuotaLimit > 0 ? profileToEdit.QuotaLimit : GetDefaultQuotaForTier(_tier);
        _isQuotaExhausted = profileToEdit.IsQuotaExhausted;

        InitializeColorOptions();
        PopulateModels(availableModels);
    }

    private void InitializeColorOptions()
    {
        var presets = new (string Hex, string Name)[]
        {
            ("#3B82F6", "Blue"),
            ("#10B981", "Emerald"),
            ("#8B5CF6", "Violet"),
            ("#EC4899", "Rose"),
            ("#F59E0B", "Amber"),
            ("#06B6D4", "Cyan"),
            ("#E11D48", "Crimson"),
            ("#64748B", "Slate")
        };

        ColorOptions.Clear();
        foreach (var (hex, name) in presets)
        {
            ColorOptions.Add(new ColorOptionItem(hex, name, hex.Equals(SelectedColor, StringComparison.OrdinalIgnoreCase)));
        }
    }

    partial void OnSelectedColorChanged(string value)
    {
        foreach (var item in ColorOptions)
        {
            item.IsSelected = item.Hex.Equals(value, StringComparison.OrdinalIgnoreCase);
        }
    }

    public string QuotaSummaryText => $"{QuotaLimit:N0} prompts/day";
    public string TierSummaryText => string.IsNullOrWhiteSpace(Tier) ? "Pro (Dynamic)" : Tier;

    partial void OnTierChanged(string value)
    {
        QuotaLimit = GetDefaultQuotaForTier(value);
        OnPropertyChanged(nameof(QuotaSummaryText));
        OnPropertyChanged(nameof(TierSummaryText));
    }

    private static int GetDefaultQuotaForTier(string? tier) => AuthDetectorService.GetDailyQuotaForTier(tier);


    private void PopulateModels(IEnumerable<string>? availableModels)
    {
        ModelOptions.Clear();
        var defaults = new[]
        {
            "Gemini 3.8 Flash (Medium)",
            "Gemini 3.8 Flash (High)",
            "Gemini 3.8 Flash (Low)",
            "Gemini 3.7 Flash (High)",
            "Gemini 3.7 Flash (Medium)",
            "Gemini 3.6 Flash (Medium)",
            "Gemini 3.1 Pro (High)",
            "Claude Sonnet 4.6 (Thinking)",
            "Claude Opus 4.6 (Thinking)",
            "GPT-OSS 120B (Medium)",
            "Gemini 2.5 Pro",
            "Gemini 2.5 Flash",
            "Gemini 1.5 Pro"
        };

        var allModels = new List<string>();
        if (availableModels != null && availableModels.Any())
        {
            allModels.AddRange(availableModels.Where(m => !string.IsNullOrWhiteSpace(m) && m != "All Models"));
        }
        foreach (var d in defaults)
        {
            if (!allModels.Contains(d))
            {
                allModels.Add(d);
            }
        }

        if (!string.IsNullOrWhiteSpace(PreferredModel) && !allModels.Contains(PreferredModel))
        {
            allModels.Insert(0, PreferredModel);
        }

        foreach (var m in allModels)
        {
            ModelOptions.Add(m);
        }
    }

    [RelayCommand]
    public void SelectColor(object? parameter)
    {
        string? hex = parameter switch
        {
            ColorOptionItem item => item.Hex,
            string s => s,
            _ => null
        };
        if (!string.IsNullOrWhiteSpace(hex))
        {
            SelectedColor = hex;
        }
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
        if (IsDefaultProfile)
        {
            ResultProfile.CustomProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        else
        {
            ResultProfile.CustomProfilePath = string.IsNullOrWhiteSpace(CustomProfilePath) ? null : CustomProfilePath.Trim();
        }
        ResultProfile.DefaultWorkspace = string.IsNullOrWhiteSpace(DefaultWorkspace) ? null : DefaultWorkspace.Trim();
        ResultProfile.ExtraArguments = string.IsNullOrWhiteSpace(ExtraArguments) ? null : ExtraArguments.Trim();
        ResultProfile.IsSelectedForSwarm = IsSelectedForSwarm;
        ResultProfile.Tier = Tier;
        ResultProfile.PreferredModel = PreferredModel;
        ResultProfile.QuotaLimit = QuotaLimit > 0 ? QuotaLimit : GetDefaultQuotaForTier(Tier);
        ResultProfile.IsQuotaExhausted = IsQuotaExhausted;

        Logger.Info($"[ProfileEdit] Saved profile '{ResultProfile.Name}' (Tier: {ResultProfile.Tier}, Model: {ResultProfile.PreferredModel}, Color: {ResultProfile.ColorTag})");
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
