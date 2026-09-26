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
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // Setup Services
        IProfileStorageService storageService = new ProfileStorageService();
        ITerminalLauncherService launcherService = new TerminalLauncherService();
        IAuthDetectorService authDetector = new AuthDetectorService();

        // Setup MainViewModel
        var mainViewModel = new MainViewModel(storageService, launcherService, authDetector);

        // Show MainWindow
        var mainWindow = new MainWindow(mainViewModel);
        mainWindow.Show();
    }
}
