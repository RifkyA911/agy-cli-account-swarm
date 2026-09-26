using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using AgyAccountSwarm.Models;
using AgyAccountSwarm.ViewModels;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

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
                Text = "Agy Account Swarm",
                Visible = true
            };

            _notifyIcon.DoubleClick += (s, e) => RestoreFromTray();

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Open Dashboard", null, (s, e) => RestoreFromTray());
            contextMenu.Items.Add("⚡ Launch Swarm", null, async (s, e) => await _viewModel.LaunchSwarmAsync());
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
        catch
        {
            // Silently handle tray setup error if running in non-interactive environment
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
            _notifyIcon?.ShowBalloonTip(1500, "Agy Account Swarm", "Running in the background tray.", ToolTipIcon.Info);
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExplicitExit && _viewModel.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            _notifyIcon?.ShowBalloonTip(1500, "Agy Account Swarm", "Minimized to tray. Double-click cat icon to reopen.", ToolTipIcon.Info);
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
        var editVm = profileToEdit != null
            ? new ProfileEditViewModel(profileToEdit)
            : new ProfileEditViewModel();

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
}
