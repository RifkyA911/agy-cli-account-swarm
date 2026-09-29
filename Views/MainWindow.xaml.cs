using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using AgyAccountSwarm.Models;
using AgyAccountSwarm.Services;
using AgyAccountSwarm.ViewModels;

using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using VisualTreeHelper = System.Windows.Media.VisualTreeHelper;

namespace AgyAccountSwarm.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private NotifyIcon? _notifyIcon;
    private bool _isExplicitExit = false;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.ShowEditDialogRequested += OnShowEditDialogAsync;
        _viewModel.ConfirmDeleteRequested += OnConfirmDeleteAsync;

        Loaded += async (s, e) =>
        {
            SetupTrayIcon();
            await _viewModel.InitializeAsync();
        };

        StateChanged += MainWindow_StateChanged;
        Closing += MainWindow_Closing;
    }

    private void SetupTrayIcon()
    {
        try
        {
            var faviconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "favicon.ico");
            var catIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "cat.ico");
            var iconPath = File.Exists(faviconPath) ? faviconPath : catIconPath;
            System.Drawing.Icon? appIcon = null;

            if (File.Exists(iconPath))
            {
                appIcon = new System.Drawing.Icon(iconPath);
            }
            else
            {
                // Fallback to extraction or system icon
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    appIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                }
            }

            _notifyIcon = new NotifyIcon
            {
                Icon = appIcon ?? System.Drawing.SystemIcons.Application,
                Text = "Agy CLI Account Swarm",
                Visible = true
            };

            _notifyIcon.DoubleClick += (s, e) => RestoreFromTray();

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Open Dashboard", null, (s, e) => RestoreFromTray());
            contextMenu.Items.Add("⚡ Launch Swarm", null, (s, e) =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _viewModel.LaunchSwarmAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("[MainWindow] Failed launching swarm from tray context menu", ex);
                    }
                });
            });
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Exit", null, (s, e) =>
            {
                _isExplicitExit = true;
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                Close();
                System.Windows.Application.Current.Shutdown();
            });

            _notifyIcon.ContextMenuStrip = contextMenu;
        }
        catch (Exception ex)
        {
            Logger.Warn($"[MainWindow] Non-fatal tray icon setup error: {ex.Message}");
        }
    }


    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _viewModel.MinimizeToTray)
        {
            Hide();
            _notifyIcon?.ShowBalloonTip(1500, "Agy CLI Account Swarm", "Running in the background tray.", ToolTipIcon.Info);
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExplicitExit && _viewModel.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            _notifyIcon?.ShowBalloonTip(1500, "Agy CLI Account Swarm", "Minimized to tray. Double-click cat icon to reopen.", ToolTipIcon.Info);
        }
        else
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
        }
    }

    private Task<AccountProfile?> OnShowEditDialogAsync(AccountProfile? profileToEdit)
    {
        var availableModels = _viewModel.ModelFilterOptions.Where(m => m != "All Models").ToList();
        var editVm = profileToEdit != null
            ? new ProfileEditViewModel(profileToEdit, availableModels)
            : new ProfileEditViewModel(availableModels);

        var dialog = new ProfileEditDialog(editVm)
        {
            Owner = this
        };

        var result = dialog.ShowDialog();
        if (result == true)
        {
            return Task.FromResult<AccountProfile?>(editVm.ResultProfile);
        }

        return Task.FromResult<AccountProfile?>(null);
    }

    private Task<bool> OnConfirmDeleteAsync(string title, string message)
    {
        var result = MessageBox.Show(this, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }

    private void ChartScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (!e.Handled)
        {
            e.Handled = true;
            var eventArg = new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };

            DependencyObject? current = VisualTreeHelper.GetParent((DependencyObject)sender);
            while (current != null && current is not ScrollViewer)
            {
                current = VisualTreeHelper.GetParent(current);
            }

            if (current is ScrollViewer parentScrollViewer)
            {
                parentScrollViewer.RaiseEvent(eventArg);
            }
        }
    }

    private void GlobalScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scv)
        {
            // If the mouse wheel event originated inside a ComboBox, ComboBoxItem, Popup, or child ScrollViewer, let that child control handle its own scrolling!
            if (e.OriginalSource is DependencyObject source)
            {
                DependencyObject? parent = source;
                while (parent != null && parent != scv)
                {
                    if (parent is System.Windows.Controls.Primitives.Popup ||
                        parent is System.Windows.Controls.ComboBox ||
                        parent is System.Windows.Controls.ComboBoxItem ||
                        (parent is ScrollViewer childScv && childScv != scv))
                    {
                        return; // Let child control handle its own mouse wheel
                    }
                    parent = VisualTreeHelper.GetParent(parent);
                }
            }

            // Standard WPF ScrollViewer jumps violently across large cards (~144px+ per notch).
            // A dampened delta (50px per standard mouse wheel click of 120 units) provides smooth, controlled scrolling.
            double delta = e.Delta / 120.0;
            double targetOffset = scv.VerticalOffset - (delta * 50.0);
            scv.ScrollToVerticalOffset(Math.Clamp(targetOffset, 0, scv.ScrollableHeight));
            e.Handled = true;
        }
    }

    private void AccountsScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) =>
        GlobalScrollViewer_PreviewMouseWheel(sender, e);
}

