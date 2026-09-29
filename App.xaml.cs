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

        // 1.1 Allocate hidden console so background CLI telemetry queries (agy -p /usage) never flash visible console/cmd windows
        try
        {
            if (AllocConsole())
            {
                var handle = GetConsoleWindow();
                if (handle != IntPtr.Zero)
                {
                    ShowWindow(handle, SW_HIDE);
                }
            }
        }
        catch { }

        Logger.Info("Agy CLI Account Swarm starting up...");

        try
        {
            // 2. Setup Services
            IProfileStorageService storageService = new ProfileStorageService();
            ITerminalLauncherService launcherService = new TerminalLauncherService();
            IAuthDetectorService authDetector = new AuthDetectorService();
            IAudioService audioService = new AudioService();
            ILocalizationService localizationService = new LocalizationService();
            IMcpService mcpService = new McpService();
            ITelemetryService telemetryService = new TelemetryService();
            IAgyModelService modelService = new AgyModelService();

            // Pre-initialize persisted theme before MainWindow is constructed
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var settingsPath = System.IO.Path.Combine(appData, "AgyAccountSwarm", "settings.json");
                if (System.IO.File.Exists(settingsPath))
                {
                    var json = System.IO.File.ReadAllText(settingsPath);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if ((doc.RootElement.TryGetProperty("Theme", out var themeProp) || doc.RootElement.TryGetProperty("theme", out themeProp)) && themeProp.GetString() is { Length: > 0 } theme)
                    {
                        ThemeManager.ApplyTheme(theme);
                    }
                    else
                    {
                        ThemeManager.ApplyTheme("System");
                    }
                }
                else
                {
                    ThemeManager.ApplyTheme("System");
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"[App] Could not pre-apply theme: {ex.Message}");
                ThemeManager.ApplyTheme("Dark");
            }

            // 3. Setup MainViewModel
            var mainViewModel = new MainViewModel(
                storageService,
                launcherService,
                authDetector,
                audioService,
                localizationService,
                mcpService,
                telemetryService,
                modelService);

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
                "Agy CLI Account Swarm Error",
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
                "Agy CLI Account Swarm Error",
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

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_HIDE = 0;
}
