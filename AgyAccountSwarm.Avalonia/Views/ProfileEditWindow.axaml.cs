using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Avalonia.Views;

public partial class ProfileEditWindow : Window
{
    public AccountProfile Profile { get; private set; }
    public bool IsConfirmed { get; private set; }

    public ProfileEditWindow() : this(new AccountProfile())
    {
    }

    public ProfileEditWindow(AccountProfile profile)
    {
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

        // Select tier index
        CmbTier.SelectedIndex = Profile.Tier.ToUpperInvariant() switch
        {
            "PLUS" => 1,
            "PRO" => 2,
            "ULTRA" => 3,
            _ => 0
        };

        // Select model index
        CmbModel.SelectedIndex = Profile.PreferredModel.ToLowerInvariant() switch
        {
            "gemini-2.5-pro" => 1,
            "gemini-3.8-flash-high" => 2,
            "claude-3.7-sonnet" => 3,
            _ => 0
        };

        // Select color index
        CmbColor.SelectedIndex = Profile.ColorTag.ToUpperInvariant() switch
        {
            "#10B981" => 1,
            "#A855F7" => 2,
            "#F59E0B" => 3,
            "#EC4899" => 4,
            _ => 0
        };
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

        Profile.Tier = CmbTier.SelectedIndex switch
        {
            1 => "Plus",
            2 => "Pro",
            3 => "Ultra",
            _ => "Basic"
        };

        Profile.PreferredModel = CmbModel.SelectedIndex switch
        {
            1 => "gemini-2.5-pro",
            2 => "gemini-3.8-flash-high",
            3 => "claude-3.7-sonnet",
            _ => "gemini-2.5-flash"
        };

        Profile.ColorTag = CmbColor.SelectedIndex switch
        {
            1 => "#10B981",
            2 => "#A855F7",
            3 => "#F59E0B",
            4 => "#EC4899",
            _ => "#3B82F6"
        };

        IsConfirmed = true;
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        IsConfirmed = false;
        Close(false);
    }
}
