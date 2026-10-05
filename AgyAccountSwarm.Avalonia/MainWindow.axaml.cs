using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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

        Loaded += (s, e) =>
        {
            var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary ?? Screens.All.FirstOrDefault();
            if (screen != null)
            {
                double scaling = screen.Scaling > 0 ? screen.Scaling : 1.0;
                double usableHeightDips = screen.WorkingArea.Height / scaling;
                double usableWidthDips = screen.WorkingArea.Width / scaling;

                // Ensure window never overflows display working area (accounting for taskbar)
                double maxAllowedHeight = usableHeightDips - 48;
                if (Height > maxAllowedHeight && maxAllowedHeight >= 460)
                {
                    Height = Math.Round(maxAllowedHeight);
                }

                double maxAllowedWidth = usableWidthDips - 48;
                if (Width > maxAllowedWidth && maxAllowedWidth >= 900)
                {
                    Width = Math.Round(maxAllowedWidth);
                }
            }
        };

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

        _viewModel.SaveExcelFileRequested += async (defaultFileName) =>
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Swarm Execution Logs to Excel",
                DefaultExtension = "xlsx",
                SuggestedFileName = defaultFileName,
                FileTypeChoices =
                [
                    new FilePickerFileType("Excel Spreadsheet (*.xlsx)") { Patterns = ["*.xlsx"] },
                    new FilePickerFileType("All Files (*.*)") { Patterns = ["*"] }
                ]
            });
            return file?.Path.LocalPath;
        };

        _viewModel.BrowseCliBinaryFileRequested += async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Antigravity CLI Executable (agy.exe)",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Executable Files (*.exe;*.*)") { Patterns = ["*.exe", "*"] },
                    new FilePickerFileType("All Files (*.*)") { Patterns = ["*"] }
                ]
            });
            return files.Count > 0 ? files[0].Path.LocalPath : null;
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

            var dialog = new ProfileEditWindow(newProfile, _viewModel.Strings);
            var result = await dialog.ShowDialog<bool>(this);
            return result && dialog.IsConfirmed ? dialog.Profile : null;
        };

        _viewModel.ShowEditDialogRequested += async (profile) =>
        {
            if (profile == null) return null;
            var dialog = new ProfileEditWindow(profile, _viewModel.Strings);
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

    private Point _workflowDragStart;
    private Vector _workflowStartOffset;
    private bool _isWorkflowDragging;

    private void OnWorkflowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var scrollViewer = this.FindControl<ScrollViewer>("WorkflowScrollViewer");
        if (scrollViewer == null) return;

        var currentPoint = e.GetCurrentPoint(scrollViewer);
        if (currentPoint.Properties.IsLeftButtonPressed)
        {
            _isWorkflowDragging = true;
            _workflowDragStart = e.GetPosition(scrollViewer);
            _workflowStartOffset = scrollViewer.Offset;
            e.Pointer.Capture(scrollViewer);
            scrollViewer.Cursor = new Cursor(StandardCursorType.SizeAll);
        }
    }

    private void OnWorkflowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isWorkflowDragging) return;
        var scrollViewer = this.FindControl<ScrollViewer>("WorkflowScrollViewer");
        if (scrollViewer == null) return;

        var currentPos = e.GetPosition(scrollViewer);
        var delta = _workflowDragStart - currentPos;

        var maxX = Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width);
        var maxY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);

        scrollViewer.Offset = new Vector(
            Math.Clamp(_workflowStartOffset.X + delta.X, 0, maxX),
            Math.Clamp(_workflowStartOffset.Y + delta.Y, 0, maxY));
    }

    private void OnWorkflowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isWorkflowDragging)
        {
            _isWorkflowDragging = false;
            var scrollViewer = this.FindControl<ScrollViewer>("WorkflowScrollViewer");
            if (scrollViewer != null)
            {
                e.Pointer.Capture(null);
                scrollViewer.Cursor = new Cursor(StandardCursorType.Hand);
            }
        }
    }

    private void OnWorkflowPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var scrollViewer = this.FindControl<ScrollViewer>("WorkflowScrollViewer");
        if (scrollViewer == null) return;

        var scrollDelta = e.Delta.Y != 0 ? -e.Delta.Y * 60 : -e.Delta.X * 60;
        var maxX = Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width);
        scrollViewer.Offset = new Vector(
            Math.Clamp(scrollViewer.Offset.X + scrollDelta, 0, maxX),
            scrollViewer.Offset.Y);
        e.Handled = true;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_viewModel.CloseToTray)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            base.OnClosing(e);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var isCtrl = (e.KeyModifiers & KeyModifiers.Control) == KeyModifiers.Control;
        var isShift = (e.KeyModifiers & KeyModifiers.Shift) == KeyModifiers.Shift;

        // Ctrl+Shift+C: Jump directly to Chat
        if (isCtrl && isShift && e.Key == Key.C)
        {
            _viewModel.Navigate("Chat");
            e.Handled = true;
            return;
        }

        // Ctrl+N: Add Profile
        if (isCtrl && !isShift && e.Key == Key.N)
        {
            _ = _viewModel.AddProfileAsync();
            e.Handled = true;
            return;
        }

        // Ctrl+F: Focus Accounts Search
        if (isCtrl && !isShift && e.Key == Key.F)
        {
            _viewModel.Navigate("Accounts");
            var searchBox = this.FindControl<TextBox>("AccountSearchBox");
            if (searchBox != null)
            {
                searchBox.Focus();
                searchBox.SelectAll();
                e.Handled = true;
                return;
            }
        }

        // Ctrl+R: Refresh / Sync All Accounts
        if (isCtrl && !isShift && e.Key == Key.R)
        {
            _viewModel.SyncSwarmCommand.Execute(null);
            e.Handled = true;
            return;
        }
    }
}