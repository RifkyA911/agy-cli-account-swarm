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
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}