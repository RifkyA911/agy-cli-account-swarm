using System.Windows;
using System.Windows.Media;
using WpfApp = System.Windows.Application;
using MediaColor = System.Windows.Media.Color;

namespace AgyAccountSwarm.Services;

public static class ThemeManager
{
    public static string CurrentTheme { get; private set; } = "Dark";

    public static void ApplyTheme(string theme)
    {
        var app = WpfApp.Current;
        if (app == null) return;

        bool isLight = theme.Equals("Light", System.StringComparison.OrdinalIgnoreCase);
        CurrentTheme = isLight ? "Light" : "Dark";

        if (isLight)
        {
            app.Resources["BrushBg"] = new SolidColorBrush(MediaColor.FromRgb(248, 250, 252));
            app.Resources["BrushSurface"] = new SolidColorBrush(MediaColor.FromRgb(255, 255, 255));
            app.Resources["BrushSurfaceHover"] = new SolidColorBrush(MediaColor.FromRgb(241, 245, 249));
            app.Resources["BrushCard"] = new SolidColorBrush(MediaColor.FromRgb(255, 255, 255));
            app.Resources["BrushCardBorder"] = new SolidColorBrush(MediaColor.FromRgb(226, 232, 240));
            app.Resources["BrushTextPrimary"] = new SolidColorBrush(MediaColor.FromRgb(15, 23, 42));
            app.Resources["BrushTextSecondary"] = new SolidColorBrush(MediaColor.FromRgb(71, 85, 105));
            app.Resources["BrushTextMuted"] = new SolidColorBrush(MediaColor.FromRgb(148, 163, 184));
            app.Resources["BrushPopupBg"] = new SolidColorBrush(MediaColor.FromRgb(255, 255, 255));
        }
        else
        {
            app.Resources["BrushBg"] = new SolidColorBrush(MediaColor.FromRgb(15, 17, 21));
            app.Resources["BrushSurface"] = new SolidColorBrush(MediaColor.FromRgb(23, 25, 30));
            app.Resources["BrushSurfaceHover"] = new SolidColorBrush(MediaColor.FromRgb(31, 34, 42));
            app.Resources["BrushCard"] = new SolidColorBrush(MediaColor.FromRgb(19, 21, 26));
            app.Resources["BrushCardBorder"] = new SolidColorBrush(MediaColor.FromRgb(37, 40, 50));
            app.Resources["BrushTextPrimary"] = new SolidColorBrush(MediaColor.FromRgb(243, 244, 246));
            app.Resources["BrushTextSecondary"] = new SolidColorBrush(MediaColor.FromRgb(156, 163, 175));
            app.Resources["BrushTextMuted"] = new SolidColorBrush(MediaColor.FromRgb(107, 114, 128));
            app.Resources["BrushPopupBg"] = new SolidColorBrush(MediaColor.FromRgb(23, 25, 30));
        }
    }
}
