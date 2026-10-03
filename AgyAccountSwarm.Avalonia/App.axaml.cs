using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace AgyAccountSwarm.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            var appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            var settingsPath = System.IO.Path.Combine(appData, "AgyAccountSwarm", "settings.json");
            if (System.IO.File.Exists(settingsPath))
            {
                var json = System.IO.File.ReadAllText(settingsPath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if ((doc.RootElement.TryGetProperty("Theme", out var themeProp) || doc.RootElement.TryGetProperty("theme", out themeProp)) && themeProp.GetString() is { Length: > 0 } theme)
                {
                    AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.ApplyTheme(theme);
                }
                else
                {
                    AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.ApplyTheme("System");
                }
            }
            else
            {
                AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.ApplyTheme("System");
            }
        }
        catch
        {
            AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.ApplyTheme("Dark");
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var win = new MainWindow();
            desktop.MainWindow = win;

            if (desktop.Args != null && desktop.Args.Contains("--export-screenshots"))
            {
                var outIndex = Array.IndexOf(desktop.Args, "--export-screenshots");
                var targetDir = outIndex + 1 < desktop.Args.Length && !desktop.Args[outIndex + 1].StartsWith("--")
                    ? desktop.Args[outIndex + 1]
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "docs", "assets", "screenshots");

                _ = Task.Run(async () =>
                {
                    await Task.Delay(2500);
                    await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        try
                        {
                            await ExportScreenshotsAsync(win, targetDir);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Screenshot export failed: {ex.Message}");
                        }
                        finally
                        {
                            desktop.Shutdown();
                        }
                    });
                });
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task ExportScreenshotsAsync(MainWindow win, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        if (win.DataContext is not ViewModels.AvaloniaMainViewModel vm) return;

        var views = new (string page, int subTab, string filename)[]
        {
            ("Dashboard", 0, "01_dashboard.png"),
            ("Accounts", 0, "02_accounts.png"),
            ("Swarm", 0, "03_swarm_workers.png"),
            ("Dispatcher", 1, "04_fleet_dispatcher.png"),
            ("Dispatcher", 0, "05_live_swarm_chat.png"),
            ("Dispatcher", 3, "06_workflow_topology.png"),
            ("Dispatcher", 2, "07_worktree_tree.png"),
            ("Analytics", 0, "08_analytics.png"),
            ("Mcp", 0, "09_mcp_servers.png"),
            ("Logs", 0, "10_execution_logs.png"),
            ("Settings", 0, "11_settings.png"),
        };

        foreach (var (page, subTab, filename) in views)
        {
            vm.CurrentPage = page;
            vm.SelectedProjectTabIndex = subTab;
            await Task.Delay(400);

            var pixelSize = new PixelSize(
                Math.Max(1366, (int)win.Bounds.Width),
                Math.Max(840, (int)win.Bounds.Height));
            using var rtb = new global::Avalonia.Media.Imaging.RenderTargetBitmap(pixelSize, new Vector(96, 96));
            rtb.Render(win);
#pragma warning disable CS0618
            var savePath = Path.Combine(outputDir, filename);
            rtb.Save(savePath);
            Console.WriteLine($"[Screenshot] Saved: {savePath}");
#pragma warning restore CS0618
        }
    }

    public void OnTrayIconClicked(object? sender, System.EventArgs e)
    {
        RestoreMainWindow();
    }

    public void OnTrayOpenClicked(object? sender, System.EventArgs e)
    {
        RestoreMainWindow();
    }

    public void OnTrayExitClicked(object? sender, System.EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private void RestoreMainWindow()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is MainWindow win)
        {
            win.Show();
            win.WindowState = global::Avalonia.Controls.WindowState.Normal;
            win.Activate();
        }
    }
}