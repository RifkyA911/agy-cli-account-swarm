using System;
using System.Threading.Tasks;
using System.Windows;
using AgyAccountSwarm.Services;
using AgyAccountSwarm.ViewModels;
using AgyAccountSwarm.Views;

namespace AgyAccountSwarm;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Setup Global Exception Handlers
        SetupExceptionHandling();

        Logger.Info("Agy Account Swarm starting up...");

        try
        {
            // 2. Setup Services
            IProfileStorageService storageService = new ProfileStorageService();
            ITerminalLauncherService launcherService = new TerminalLauncherService();
            IAuthDetectorService authDetector = new AuthDetectorService();

            // 3. Setup MainViewModel
            var mainViewModel = new MainViewModel(storageService, launcherService, authDetector);

            // 4. Show MainWindow
            var mainWindow = new MainWindow(mainViewModel);
            mainWindow.Show();

            Logger.Info("MainWindow displayed successfully.");
        }
        catch (Exception ex)
        {
            Logger.Error("Fatal error during application startup", ex);
            System.Windows.MessageBox.Show(
                $"Fatal startup error:\n{ex.Message}\n\nCheck logs at:\n{Logger.LogPath}",
                "Agy Account Swarm Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void SetupExceptionHandling()
    {
        // UI thread unhandled exceptions
        DispatcherUnhandledException += (s, e) =>
        {
            Logger.Error("Unhandled Dispatcher Exception", e.Exception);
            System.Windows.MessageBox.Show(
                $"An unexpected error occurred:\n{e.Exception.Message}\n\nDetails saved to:\n{Logger.LogPath}",
                "Agy Account Swarm Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true; // Prevent abrupt app crash
        };

        // Background / AppDomain unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                Logger.Error("Unhandled AppDomain Exception", ex);
            }
            else
            {
                Logger.Error($"Unhandled AppDomain Exception (non-exception object): {e.ExceptionObject}");
            }
        };

        // Async task exceptions
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            Logger.Error("Unobserved Task Exception", e.Exception);
            e.SetObserved(); // Prevent task crash
        };
    }
}
