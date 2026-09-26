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
            // Surfaces & Text
            app.Resources["BrushBg"] = new SolidColorBrush(MediaColor.FromRgb(248, 250, 252));
            app.Resources["BrushSurface"] = new SolidColorBrush(MediaColor.FromRgb(255, 255, 255));
            app.Resources["BrushSurfaceHover"] = new SolidColorBrush(MediaColor.FromRgb(241, 245, 249));
            app.Resources["BrushCard"] = new SolidColorBrush(MediaColor.FromRgb(255, 255, 255));
            app.Resources["BrushCardBorder"] = new SolidColorBrush(MediaColor.FromRgb(226, 232, 240));
            app.Resources["BrushTextPrimary"] = new SolidColorBrush(MediaColor.FromRgb(15, 23, 42));
            app.Resources["BrushTextSecondary"] = new SolidColorBrush(MediaColor.FromRgb(71, 85, 105));
            app.Resources["BrushTextMuted"] = new SolidColorBrush(MediaColor.FromRgb(148, 163, 184));
            app.Resources["BrushPopupBg"] = new SolidColorBrush(MediaColor.FromRgb(255, 255, 255));

            // Alert Banner (Light Mode)
            app.Resources["BrushAlertBg"] = new SolidColorBrush(MediaColor.FromRgb(239, 246, 255));      // Soft blue
            app.Resources["BrushAlertBorder"] = new SolidColorBrush(MediaColor.FromRgb(191, 219, 254));  // Light blue border
            app.Resources["BrushAlertText"] = new SolidColorBrush(MediaColor.FromRgb(29, 78, 216));      // Deep accessible blue

            // Version Tag & Neutral Badges (Light Mode)
            app.Resources["BrushBadgeBg"] = new SolidColorBrush(MediaColor.FromRgb(241, 245, 249));      // Slate 100
            app.Resources["BrushBadgeBorder"] = new SolidColorBrush(MediaColor.FromRgb(203, 213, 225));  // Slate 300
            app.Resources["BrushBadgeText"] = new SolidColorBrush(MediaColor.FromRgb(37, 99, 235));      // Blue 600

            // Model Tag (Light Mode)
            app.Resources["BrushModelBadgeBg"] = new SolidColorBrush(MediaColor.FromRgb(240, 253, 250)); // Soft teal/mint
            app.Resources["BrushModelBadgeBorder"] = new SolidColorBrush(MediaColor.FromRgb(153, 246, 228));
            app.Resources["BrushModelBadgeText"] = new SolidColorBrush(MediaColor.FromRgb(15, 118, 110));

            // Progress & Mono
            app.Resources["BrushProgressTrack"] = new SolidColorBrush(MediaColor.FromRgb(226, 232, 240));
            app.Resources["BrushMonoPath"] = new SolidColorBrush(MediaColor.FromRgb(37, 99, 235));

            // Quota Exhausted Alert (Light Mode)
            app.Resources["BrushQuotaExhaustedBg"] = new SolidColorBrush(MediaColor.FromRgb(254, 242, 242));
            app.Resources["BrushQuotaExhaustedBorder"] = new SolidColorBrush(MediaColor.FromRgb(248, 113, 113));
            app.Resources["BrushQuotaExhaustedText"] = new SolidColorBrush(MediaColor.FromRgb(185, 28, 28));
        }
        else
        {
            // Surfaces & Text
            app.Resources["BrushBg"] = new SolidColorBrush(MediaColor.FromRgb(15, 17, 21));
            app.Resources["BrushSurface"] = new SolidColorBrush(MediaColor.FromRgb(23, 25, 30));
            app.Resources["BrushSurfaceHover"] = new SolidColorBrush(MediaColor.FromRgb(31, 34, 42));
            app.Resources["BrushCard"] = new SolidColorBrush(MediaColor.FromRgb(19, 21, 26));
            app.Resources["BrushCardBorder"] = new SolidColorBrush(MediaColor.FromRgb(37, 40, 50));
            app.Resources["BrushTextPrimary"] = new SolidColorBrush(MediaColor.FromRgb(243, 244, 246));
            app.Resources["BrushTextSecondary"] = new SolidColorBrush(MediaColor.FromRgb(156, 163, 175));
            app.Resources["BrushTextMuted"] = new SolidColorBrush(MediaColor.FromRgb(107, 114, 128));
            app.Resources["BrushPopupBg"] = new SolidColorBrush(MediaColor.FromRgb(23, 25, 30));

            // Alert Banner (Dark Mode)
            app.Resources["BrushAlertBg"] = new SolidColorBrush(MediaColor.FromRgb(30, 41, 59));
            app.Resources["BrushAlertBorder"] = new SolidColorBrush(MediaColor.FromRgb(59, 130, 246));
            app.Resources["BrushAlertText"] = new SolidColorBrush(MediaColor.FromRgb(147, 197, 253));

            // Version Tag & Neutral Badges (Dark Mode)
            app.Resources["BrushBadgeBg"] = new SolidColorBrush(MediaColor.FromRgb(30, 41, 59));
            app.Resources["BrushBadgeBorder"] = new SolidColorBrush(MediaColor.FromRgb(51, 65, 85));
            app.Resources["BrushBadgeText"] = new SolidColorBrush(MediaColor.FromRgb(147, 197, 253));

            // Model Tag (Dark Mode)
            app.Resources["BrushModelBadgeBg"] = new SolidColorBrush(MediaColor.FromRgb(30, 41, 59));
            app.Resources["BrushModelBadgeBorder"] = new SolidColorBrush(MediaColor.FromRgb(51, 65, 85));
            app.Resources["BrushModelBadgeText"] = new SolidColorBrush(MediaColor.FromRgb(147, 197, 253));

            // Progress & Mono
            app.Resources["BrushProgressTrack"] = new SolidColorBrush(MediaColor.FromRgb(31, 36, 48));
            app.Resources["BrushMonoPath"] = new SolidColorBrush(MediaColor.FromRgb(96, 165, 250));

            // Quota Exhausted Alert (Dark Mode)
            app.Resources["BrushQuotaExhaustedBg"] = new SolidColorBrush(MediaColor.FromRgb(49, 17, 24));
            app.Resources["BrushQuotaExhaustedBorder"] = new SolidColorBrush(MediaColor.FromRgb(220, 38, 38));
            app.Resources["BrushQuotaExhaustedText"] = new SolidColorBrush(MediaColor.FromRgb(252, 165, 165));
        }
    }
}
