using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using AgyAccountSwarm.Models;
using AgyAccountSwarm.Services;

namespace AgyAccountSwarm.Avalonia.Views;

public partial class ProfileEditWindow : Window
{
    public AccountProfile Profile { get; private set; }
    public bool IsConfirmed { get; private set; }
    public ILocalizationService Strings { get; }
    private bool _isUpdatingColorInternally;

    public ProfileEditWindow() : this(new AccountProfile(), null)
    {
    }

    public ProfileEditWindow(AccountProfile profile, ILocalizationService? localizationService = null)
    {
        Strings = localizationService ?? LocalizationService.Instance;
        DataContext = this;
        InitializeComponent();
        Profile = profile;
        LoadProfileData();
    }

    private void LoadProfileData()
    {
        TxtName.Text = Profile.Name;
        TxtDescription.Text = Profile.Description;
        TxtWorkspace.Text = Profile.DefaultWorkspace;
        TxtCustomPath.Text = Profile.CustomProfilePath;
        ChkSkipPermissions.IsChecked = Profile.DangerouslySkipPermissions;
        ChkIncludeInSwarm.IsChecked = Profile.IsSelectedForSwarm;

        // Select Role / Archetype
        CmbRole.SelectedIndex = Profile.AccountRole switch
        {
            "Lead Architect" => 0,
            "Backend Specialist" => 1,
            "Frontend Specialist" => 2,
            "Security Auditor" => 3,
            "QA Automation Engineer" => 4,
            "DevOps & Infrastructure" => 5,
            _ => 6 // Generalist Worker
        };

        // Select Model Family
        CmbModelFamily.SelectedIndex = (Profile.ModelFamily?.ToLowerInvariant()) switch
        {
            "claude & gpt" or "claude / gpt" or "claude" or "gpt" => 1,
            _ => 0 // Gemini
        };

        // Select Temperature Preset
        CmbTemperature.SelectedIndex = (Profile.ModelTemperature?.ToLowerInvariant()) switch
        {
            var t when t != null && t.Contains("low") => 0,
            var t when t != null && t.Contains("high") => 2,
            _ => 1 // Medium (0.7)
        };

        // Select Preferred Model
        CmbModel.SelectedIndex = (Profile.PreferredModel?.ToLowerInvariant()) switch
        {
            "gemini-2.5-pro" => 1,
            "gemini-3.1-pro-high" => 2,
            "gemini-3.8-flash-high" => 3,
            "claude-3.7-sonnet" => 4,
            "claude-opus-4-6-thinking" => 5,
            "gpt-oss-120b-medium" => 6,
            _ => 0 // gemini-2.5-flash
        };

        // Load Color Tag
        string colorTag = string.IsNullOrWhiteSpace(Profile.ColorTag) ? "#3B82F6" : Profile.ColorTag;
        TxtCustomHex.Text = colorTag;
        SyncColorComboFromHex(colorTag);
        UpdateColorPreview(colorTag);
    }

    private async void OnBrowseWorkspaceClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Default Project Workspace Directory",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                TxtWorkspace.Text = folders[0].Path.LocalPath;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to open workspace folder picker: {ex.Message}");
        }
    }

    private async void OnBrowseCustomPathClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Custom Isolated Profile Directory",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                TxtCustomPath.Text = folders[0].Path.LocalPath;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to open custom path folder picker: {ex.Message}");
        }
    }

    private void OnModelFamilySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CmbModelFamily == null || CmbModel == null) return;

        if (CmbModelFamily.SelectedIndex == 1) // Claude & GPT
        {
            if (CmbModel.SelectedIndex < 4)
            {
                CmbModel.SelectedIndex = 4; // claude-3.7-sonnet
            }
        }
        else // Gemini
        {
            if (CmbModel.SelectedIndex >= 4)
            {
                CmbModel.SelectedIndex = 0; // gemini-2.5-flash
            }
        }
    }

    private void OnColorPresetSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingColorInternally || CmbColor == null || TxtCustomHex == null) return;

        string? selectedHex = CmbColor.SelectedIndex switch
        {
            0 => "#3B82F6",
            1 => "#10B981",
            2 => "#8B5CF6",
            3 => "#F59E0B",
            4 => "#EC4899",
            5 => "#06B6D4",
            6 => "#EF4444",
            7 => "#64748B",
            _ => null
        };

        if (!string.IsNullOrEmpty(selectedHex))
        {
            _isUpdatingColorInternally = true;
            TxtCustomHex.Text = selectedHex;
            _isUpdatingColorInternally = false;
            UpdateColorPreview(selectedHex);
        }
    }

    private void OnCustomHexTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isUpdatingColorInternally || TxtCustomHex == null) return;

        string hex = TxtCustomHex.Text?.Trim() ?? string.Empty;
        if (!hex.StartsWith("#") && hex.Length > 0)
        {
            hex = "#" + hex;
        }

        UpdateColorPreview(hex);
        SyncColorComboFromHex(hex);
    }

    private void SyncColorComboFromHex(string hex)
    {
        if (CmbColor == null) return;
        _isUpdatingColorInternally = true;
        CmbColor.SelectedIndex = hex.ToUpperInvariant() switch
        {
            "#3B82F6" => 0,
            "#10B981" => 1,
            "#8B5CF6" => 2,
            "#F59E0B" => 3,
            "#EC4899" => 4,
            "#06B6D4" => 5,
            "#EF4444" => 6,
            "#64748B" => 7,
            _ => 8 // Custom HEX
        };
        _isUpdatingColorInternally = false;
    }

    private void UpdateColorPreview(string hex)
    {
        if (ColorPreviewBorder == null) return;
        try
        {
            if (Color.TryParse(hex, out var color))
            {
                ColorPreviewBorder.Background = new SolidColorBrush(color);
            }
        }
        catch
        {
            // Silently ignore incomplete hex input
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtName.Text))
        {
            return;
        }

        Profile.Name = TxtName.Text.Trim();
        Profile.Description = TxtDescription.Text?.Trim() ?? string.Empty;
        Profile.DefaultWorkspace = string.IsNullOrWhiteSpace(TxtWorkspace.Text) ? null : TxtWorkspace.Text.Trim();
        Profile.CustomProfilePath = string.IsNullOrWhiteSpace(TxtCustomPath.Text) ? null : TxtCustomPath.Text.Trim();
        Profile.DangerouslySkipPermissions = ChkSkipPermissions.IsChecked ?? false;
        Profile.IsSelectedForSwarm = ChkIncludeInSwarm.IsChecked ?? true;

        Profile.AccountRole = CmbRole.SelectedIndex switch
        {
            0 => "Lead Architect",
            1 => "Backend Specialist",
            2 => "Frontend Specialist",
            3 => "Security Auditor",
            4 => "QA Automation Engineer",
            5 => "DevOps & Infrastructure",
            _ => "Generalist Worker"
        };

        Profile.ModelFamily = CmbModelFamily.SelectedIndex switch
        {
            1 => "Claude & GPT",
            _ => "Gemini"
        };

        Profile.ModelTemperature = CmbTemperature.SelectedIndex switch
        {
            0 => "Low (0.2)",
            2 => "High (1.0)",
            _ => "Medium (0.7)"
        };

        Profile.PreferredModel = CmbModel.SelectedIndex switch
        {
            1 => "gemini-2.5-pro",
            2 => "gemini-3.1-pro-high",
            3 => "gemini-3.8-flash-high",
            4 => "claude-3.7-sonnet",
            5 => "claude-opus-4-6-thinking",
            6 => "gpt-oss-120b-medium",
            _ => "gemini-2.5-flash"
        };

        string hex = TxtCustomHex.Text?.Trim() ?? "#3B82F6";
        if (!hex.StartsWith("#") && hex.Length > 0) hex = "#" + hex;
        Profile.ColorTag = Color.TryParse(hex, out _) ? hex : "#3B82F6";

        IsConfirmed = true;
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        IsConfirmed = false;
        Close(false);
    }
}
