using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AgyAccountSwarm.Avalonia.ViewModels;
using AgyAccountSwarm.Avalonia.Views;
using AgyAccountSwarm.Models;
using AgyAccountSwarm.Services;
using AgyAccountSwarm.ViewModels;

namespace AgyAccountSwarm.Avalonia;

public partial class MainWindow : Window
{
    private readonly AvaloniaMainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new AvaloniaMainViewModel();
        DataContext = _viewModel;

        _viewModel.BrowseFolderRequested += async () =>
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Git Repository Directory",
                AllowMultiple = false
            });
            return folders.Count > 0 ? folders[0].Path.LocalPath : null;
        };

        _viewModel.SavePdfFileRequested += async (defaultFileName) =>
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Swarm Telemetry PDF / HTML",
                DefaultExtension = "pdf",
                SuggestedFileName = defaultFileName,
                FileTypeChoices =
                [
                    new FilePickerFileType("PDF Document (*.pdf)") { Patterns = ["*.pdf"] },
                    new FilePickerFileType("HTML Document (*.html)") { Patterns = ["*.html"] }
                ]
            });
            return file?.Path.LocalPath;
        };

        _viewModel.ShowAddProfileRequested += async () =>
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
            return result && dialog.IsConfirmed ? dialog.Profile : null;
        };

        _viewModel.ShowEditDialogRequested += async (profile) =>
        {
            if (profile == null) return null;
            var dialog = new ProfileEditWindow(profile);
            var result = await dialog.ShowDialog<bool>(this);
            return result && dialog.IsConfirmed ? dialog.Profile : null;
        };

        _viewModel.ImportChatRequested += async (itemVm) =>
        {
            if (itemVm == null) return;
            var transferService = new ConversationTransferService();
            var audioService = new AudioService();
            var importVm = new ImportChatViewModel(
                itemVm.Profile,
                _viewModel.Profiles.Select(p => p.Profile),
                transferService,
                audioService);

            var dialog = new ImportChatWindow(importVm);
            await dialog.ShowDialog<bool>(this);
            await itemVm.RefreshAuthStatusAsync();
        };
    }

    private async void OnAddAccountClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.AddProfileAsync();
    }

    private async void OnEditAccountClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: AvaloniaProfileItemViewModel item })
        {
            await _viewModel.EditProfileAsync(item);
        }
    }
}