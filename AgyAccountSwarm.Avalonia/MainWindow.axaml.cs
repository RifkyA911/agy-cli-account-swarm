using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AgyAccountSwarm.Avalonia.ViewModels;
using AgyAccountSwarm.Avalonia.Views;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Avalonia;

public partial class MainWindow : Window
{
    private readonly AvaloniaMainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new AvaloniaMainViewModel();
        DataContext = _viewModel;

        PropertyChanged += (s, e) =>
        {
            if (e.Property == WindowStateProperty && BtnMaximize != null)
            {
                BtnMaximize.Content = WindowState == WindowState.Maximized ? "🗗" : "🗖";
            }
        };

        _viewModel.BrowseFolderRequested += async () =>
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new global::Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Select Git Repository Directory",
                AllowMultiple = false
            });
            return folders.Count > 0 ? folders[0].Path.LocalPath : null;
        };
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            if (e.ClickCount == 2)
            {
                OnMaximizeRestoreClick(sender, e);
            }
            else
            {
                BeginMoveDrag(e);
            }
        }
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnAddAccountClick(object? sender, RoutedEventArgs e)
    {
        var newProfile = new AccountProfile
        {
            Name = $"Worker {_viewModel.Profiles.Count + 1}",
            Tier = "Basic",
            QuotaLimit = 100,
            PreferredModel = "gemini-2.5-flash",
            DangerouslySkipPermissions = false,
            ColorTag = "#10B981",
            IsSelectedForSwarm = true
        };

        var dialog = new ProfileEditWindow(newProfile);
        var result = await dialog.ShowDialog<bool>(this);
        if (result && dialog.IsConfirmed)
        {
            _viewModel.Profiles.Add(dialog.Profile);
            _viewModel.ApplyFilters();
            await _viewModel.StorageService.SaveProfilesAsync(_viewModel.Profiles);
            _viewModel.ShowNotification($"Profile '{dialog.Profile.Name}' added successfully.");
        }
    }

    private async void OnEditAccountClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: AccountProfile profile })
        {
            var dialog = new ProfileEditWindow(profile);
            var result = await dialog.ShowDialog<bool>(this);
            if (result && dialog.IsConfirmed)
            {
                _viewModel.ApplyFilters();
                await _viewModel.StorageService.SaveProfilesAsync(_viewModel.Profiles);
                _viewModel.ShowNotification($"Profile '{profile.Name}' updated successfully.");
            }
        }
    }
}