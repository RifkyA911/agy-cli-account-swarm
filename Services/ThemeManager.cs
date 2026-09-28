using System;
using System.Windows;
using System.Windows.Media;
using WpfApp = System.Windows.Application;
using MediaColor = System.Windows.Media.Color;

namespace AgyAccountSwarm.Services;

public static class ThemeManager
{
    public static string CurrentTheme { get; private set; } = "Dark";

    public static readonly string[] AvailableThemes = ["System", "Dark", "Light", "Cyberpunk", "Matrix"];

    public static string DetectWindowsTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            if (val is int intVal && intVal == 1)
            {
                return "Light";
            }
        }
        catch { }
        return "Dark";
    }

    public static void ApplyTheme(string theme)
    {
        var app = WpfApp.Current;
        if (app == null) return;

        CurrentTheme = theme;

        if (string.Equals(theme, "System", StringComparison.OrdinalIgnoreCase))
        {
            string osTheme = DetectWindowsTheme();
            if (osTheme == "Light")
                ApplyLightTheme(app);
            else
                ApplyDarkTheme(app);
            return;
        }

        switch (theme)
        {
            case "Light":
                ApplyLightTheme(app);
                break;
            case "Cyberpunk":
                ApplyCyberpunkTheme(app);
                break;
            case "Matrix":
                ApplyMatrixTheme(app);
                break;
            case "Dark":
            default:
                ApplyDarkTheme(app);
                break;
        }
    }

    private static void ApplyDarkTheme(WpfApp app)
    {
        // Surfaces & Text
        app.Resources["BrushBg"] = new SolidColorBrush(MediaColor.FromRgb(15, 17, 21));
        app.Resources["BrushSurface"] = new SolidColorBrush(MediaColor.FromRgb(23, 25, 30));
        app.Resources["BrushSurfaceHover"] = new SolidColorBrush(MediaColor.FromRgb(31, 34, 42));
        app.Resources["BrushSurfacePressed"] = new SolidColorBrush(MediaColor.FromRgb(37, 41, 54));
        app.Resources["BrushDangerHoverBg"] = new SolidColorBrush(MediaColor.FromRgb(51, 20, 24));
        app.Resources["BrushCard"] = new SolidColorBrush(MediaColor.FromRgb(19, 21, 26));
        app.Resources["BrushCardBorder"] = new SolidColorBrush(MediaColor.FromRgb(37, 40, 50));
        app.Resources["BrushTextPrimary"] = new SolidColorBrush(MediaColor.FromRgb(243, 244, 246));
        app.Resources["BrushTextSecondary"] = new SolidColorBrush(MediaColor.FromRgb(156, 163, 175));
        app.Resources["BrushTextMuted"] = new SolidColorBrush(MediaColor.FromRgb(107, 114, 128));
        app.Resources["BrushPopupBg"] = new SolidColorBrush(MediaColor.FromRgb(23, 25, 30));

        // Primary Accent
        app.Resources["BrushPrimary"] = new SolidColorBrush(MediaColor.FromRgb(37, 99, 235));
        app.Resources["BrushPrimaryHover"] = new SolidColorBrush(MediaColor.FromRgb(29, 78, 216));

        // Nav Sidebar
        app.Resources["BrushNavActiveBg"] = new SolidColorBrush(MediaColor.FromRgb(30, 41, 59));
        app.Resources["BrushNavActiveBorder"] = new SolidColorBrush(MediaColor.FromRgb(59, 130, 246));
        app.Resources["BrushNavActiveText"] = new SolidColorBrush(MediaColor.FromRgb(248, 250, 252));
        app.Resources["BrushNavHoverBg"] = new SolidColorBrush(MediaColor.FromRgb(31, 34, 42));
        app.Resources["BrushNavInactiveText"] = new SolidColorBrush(MediaColor.FromRgb(156, 163, 175));

        // Alert Banner
        app.Resources["BrushAlertBg"] = new SolidColorBrush(MediaColor.FromRgb(30, 41, 59));
        app.Resources["BrushAlertBorder"] = new SolidColorBrush(MediaColor.FromRgb(59, 130, 246));
        app.Resources["BrushAlertText"] = new SolidColorBrush(MediaColor.FromRgb(147, 197, 253));

        // Badges
        app.Resources["BrushBadgeBg"] = new SolidColorBrush(MediaColor.FromRgb(30, 41, 59));
        app.Resources["BrushBadgeBorder"] = new SolidColorBrush(MediaColor.FromRgb(51, 65, 85));
        app.Resources["BrushBadgeText"] = new SolidColorBrush(MediaColor.FromRgb(147, 197, 253));

        app.Resources["BrushModelBadgeBg"] = new SolidColorBrush(MediaColor.FromRgb(30, 41, 59));
        app.Resources["BrushModelBadgeBorder"] = new SolidColorBrush(MediaColor.FromRgb(51, 65, 85));
        app.Resources["BrushModelBadgeText"] = new SolidColorBrush(MediaColor.FromRgb(147, 197, 253));

        // Progress & Mono
        app.Resources["BrushProgressTrack"] = new SolidColorBrush(MediaColor.FromRgb(31, 36, 48));
        app.Resources["BrushMonoPath"] = new SolidColorBrush(MediaColor.FromRgb(96, 165, 250));

        // Quota Alert
        app.Resources["BrushQuotaExhaustedBg"] = new SolidColorBrush(MediaColor.FromRgb(49, 17, 24));
        app.Resources["BrushQuotaExhaustedBorder"] = new SolidColorBrush(MediaColor.FromRgb(220, 38, 38));
        app.Resources["BrushQuotaExhaustedText"] = new SolidColorBrush(MediaColor.FromRgb(252, 165, 165));
    }

    private static void ApplyLightTheme(WpfApp app)
    {
        // Surfaces & Text
        app.Resources["BrushBg"] = new SolidColorBrush(MediaColor.FromRgb(248, 250, 252));       // Slate 50
        app.Resources["BrushSurface"] = new SolidColorBrush(MediaColor.FromRgb(255, 255, 255));  // White
        app.Resources["BrushSurfaceHover"] = new SolidColorBrush(MediaColor.FromRgb(241, 245, 249)); // Slate 100
        app.Resources["BrushSurfacePressed"] = new SolidColorBrush(MediaColor.FromRgb(226, 232, 240)); // Slate 200
        app.Resources["BrushDangerHoverBg"] = new SolidColorBrush(MediaColor.FromRgb(254, 226, 226)); // Red 100
        app.Resources["BrushCard"] = new SolidColorBrush(MediaColor.FromRgb(255, 255, 255));
        app.Resources["BrushCardBorder"] = new SolidColorBrush(MediaColor.FromRgb(226, 232, 240));  // Slate 200
        app.Resources["BrushTextPrimary"] = new SolidColorBrush(MediaColor.FromRgb(15, 23, 42));     // Slate 900
        app.Resources["BrushTextSecondary"] = new SolidColorBrush(MediaColor.FromRgb(71, 85, 105));  // Slate 600
        app.Resources["BrushTextMuted"] = new SolidColorBrush(MediaColor.FromRgb(148, 163, 184));    // Slate 400
        app.Resources["BrushPopupBg"] = new SolidColorBrush(MediaColor.FromRgb(255, 255, 255));

        // Primary Accent
        app.Resources["BrushPrimary"] = new SolidColorBrush(MediaColor.FromRgb(37, 99, 235));
        app.Resources["BrushPrimaryHover"] = new SolidColorBrush(MediaColor.FromRgb(29, 78, 216));

        // Nav Sidebar (Crisp High-Contrast)
        app.Resources["BrushNavActiveBg"] = new SolidColorBrush(MediaColor.FromRgb(224, 231, 255));    // Indigo 100
        app.Resources["BrushNavActiveBorder"] = new SolidColorBrush(MediaColor.FromRgb(59, 130, 246)); // Blue 500
        app.Resources["BrushNavActiveText"] = new SolidColorBrush(MediaColor.FromRgb(30, 58, 138));    // Blue 900
        app.Resources["BrushNavHoverBg"] = new SolidColorBrush(MediaColor.FromRgb(241, 245, 249));    // Slate 100
        app.Resources["BrushNavInactiveText"] = new SolidColorBrush(MediaColor.FromRgb(71, 85, 105)); // Slate 600

        // Alert Banner
        app.Resources["BrushAlertBg"] = new SolidColorBrush(MediaColor.FromRgb(239, 246, 255));
        app.Resources["BrushAlertBorder"] = new SolidColorBrush(MediaColor.FromRgb(191, 219, 254));
        app.Resources["BrushAlertText"] = new SolidColorBrush(MediaColor.FromRgb(29, 78, 216));

        // Badges
        app.Resources["BrushBadgeBg"] = new SolidColorBrush(MediaColor.FromRgb(241, 245, 249));
        app.Resources["BrushBadgeBorder"] = new SolidColorBrush(MediaColor.FromRgb(203, 213, 225));
        app.Resources["BrushBadgeText"] = new SolidColorBrush(MediaColor.FromRgb(37, 99, 235));

        app.Resources["BrushModelBadgeBg"] = new SolidColorBrush(MediaColor.FromRgb(240, 253, 250));
        app.Resources["BrushModelBadgeBorder"] = new SolidColorBrush(MediaColor.FromRgb(153, 246, 228));
        app.Resources["BrushModelBadgeText"] = new SolidColorBrush(MediaColor.FromRgb(15, 118, 110));

        // Progress & Mono
        app.Resources["BrushProgressTrack"] = new SolidColorBrush(MediaColor.FromRgb(226, 232, 240));
        app.Resources["BrushMonoPath"] = new SolidColorBrush(MediaColor.FromRgb(37, 99, 235));

        // Quota Alert
        app.Resources["BrushQuotaExhaustedBg"] = new SolidColorBrush(MediaColor.FromRgb(254, 242, 242));
        app.Resources["BrushQuotaExhaustedBorder"] = new SolidColorBrush(MediaColor.FromRgb(248, 113, 113));
        app.Resources["BrushQuotaExhaustedText"] = new SolidColorBrush(MediaColor.FromRgb(185, 28, 28));
    }

    private static void ApplyCyberpunkTheme(WpfApp app)
    {
        // Surfaces & Text (Neon Purple / Violet)
        app.Resources["BrushBg"] = new SolidColorBrush(MediaColor.FromRgb(13, 10, 24));          // #0D0A18
        app.Resources["BrushSurface"] = new SolidColorBrush(MediaColor.FromRgb(21, 16, 38));     // #151026
        app.Resources["BrushSurfaceHover"] = new SolidColorBrush(MediaColor.FromRgb(34, 26, 62));
        app.Resources["BrushSurfacePressed"] = new SolidColorBrush(MediaColor.FromRgb(45, 18, 77));
        app.Resources["BrushDangerHoverBg"] = new SolidColorBrush(MediaColor.FromRgb(60, 15, 30));
        app.Resources["BrushCard"] = new SolidColorBrush(MediaColor.FromRgb(25, 19, 44));        // #19132C
        app.Resources["BrushCardBorder"] = new SolidColorBrush(MediaColor.FromRgb(65, 45, 102));
        app.Resources["BrushTextPrimary"] = new SolidColorBrush(MediaColor.FromRgb(245, 243, 255));
        app.Resources["BrushTextSecondary"] = new SolidColorBrush(MediaColor.FromRgb(196, 181, 253));
        app.Resources["BrushTextMuted"] = new SolidColorBrush(MediaColor.FromRgb(139, 92, 246));
        app.Resources["BrushPopupBg"] = new SolidColorBrush(MediaColor.FromRgb(21, 16, 38));

        // Primary Accent
        app.Resources["BrushPrimary"] = new SolidColorBrush(MediaColor.FromRgb(168, 85, 247));   // Purple 500
        app.Resources["BrushPrimaryHover"] = new SolidColorBrush(MediaColor.FromRgb(147, 51, 234));

        // Nav Sidebar
        app.Resources["BrushNavActiveBg"] = new SolidColorBrush(MediaColor.FromRgb(55, 30, 85));
        app.Resources["BrushNavActiveBorder"] = new SolidColorBrush(MediaColor.FromRgb(168, 85, 247));
        app.Resources["BrushNavActiveText"] = new SolidColorBrush(MediaColor.FromRgb(255, 255, 255));
        app.Resources["BrushNavHoverBg"] = new SolidColorBrush(MediaColor.FromRgb(34, 26, 62));
        app.Resources["BrushNavInactiveText"] = new SolidColorBrush(MediaColor.FromRgb(196, 181, 253));

        // Alert Banner
        app.Resources["BrushAlertBg"] = new SolidColorBrush(MediaColor.FromRgb(46, 26, 71));
        app.Resources["BrushAlertBorder"] = new SolidColorBrush(MediaColor.FromRgb(168, 85, 247));
        app.Resources["BrushAlertText"] = new SolidColorBrush(MediaColor.FromRgb(233, 213, 255));

        // Badges
        app.Resources["BrushBadgeBg"] = new SolidColorBrush(MediaColor.FromRgb(46, 26, 71));
        app.Resources["BrushBadgeBorder"] = new SolidColorBrush(MediaColor.FromRgb(88, 50, 138));
        app.Resources["BrushBadgeText"] = new SolidColorBrush(MediaColor.FromRgb(216, 180, 254));

        app.Resources["BrushModelBadgeBg"] = new SolidColorBrush(MediaColor.FromRgb(46, 26, 71));
        app.Resources["BrushModelBadgeBorder"] = new SolidColorBrush(MediaColor.FromRgb(88, 50, 138));
        app.Resources["BrushModelBadgeText"] = new SolidColorBrush(MediaColor.FromRgb(216, 180, 254));

        // Progress & Mono
        app.Resources["BrushProgressTrack"] = new SolidColorBrush(MediaColor.FromRgb(34, 26, 62));
        app.Resources["BrushMonoPath"] = new SolidColorBrush(MediaColor.FromRgb(192, 132, 252));

        // Quota Alert
        app.Resources["BrushQuotaExhaustedBg"] = new SolidColorBrush(MediaColor.FromRgb(58, 14, 30));
        app.Resources["BrushQuotaExhaustedBorder"] = new SolidColorBrush(MediaColor.FromRgb(244, 63, 94));
        app.Resources["BrushQuotaExhaustedText"] = new SolidColorBrush(MediaColor.FromRgb(254, 205, 211));
    }

    private static void ApplyMatrixTheme(WpfApp app)
    {
        // Surfaces & Text (Hacker Terminal Emerald)
        app.Resources["BrushBg"] = new SolidColorBrush(MediaColor.FromRgb(7, 16, 12));           // #07100C
        app.Resources["BrushSurface"] = new SolidColorBrush(MediaColor.FromRgb(11, 26, 19));     // #0B1A13
        app.Resources["BrushSurfaceHover"] = new SolidColorBrush(MediaColor.FromRgb(18, 41, 30));
        app.Resources["BrushSurfacePressed"] = new SolidColorBrush(MediaColor.FromRgb(10, 41, 10));
        app.Resources["BrushDangerHoverBg"] = new SolidColorBrush(MediaColor.FromRgb(40, 20, 10));
        app.Resources["BrushCard"] = new SolidColorBrush(MediaColor.FromRgb(13, 31, 23));        // #0D1F17
        app.Resources["BrushCardBorder"] = new SolidColorBrush(MediaColor.FromRgb(26, 64, 46));
        app.Resources["BrushTextPrimary"] = new SolidColorBrush(MediaColor.FromRgb(236, 253, 245));
        app.Resources["BrushTextSecondary"] = new SolidColorBrush(MediaColor.FromRgb(110, 231, 183));
        app.Resources["BrushTextMuted"] = new SolidColorBrush(MediaColor.FromRgb(52, 211, 153));
        app.Resources["BrushPopupBg"] = new SolidColorBrush(MediaColor.FromRgb(11, 26, 19));

        // Primary Accent
        app.Resources["BrushPrimary"] = new SolidColorBrush(MediaColor.FromRgb(16, 185, 129));   // Emerald 500
        app.Resources["BrushPrimaryHover"] = new SolidColorBrush(MediaColor.FromRgb(5, 150, 105));

        // Nav Sidebar
        app.Resources["BrushNavActiveBg"] = new SolidColorBrush(MediaColor.FromRgb(17, 48, 35));
        app.Resources["BrushNavActiveBorder"] = new SolidColorBrush(MediaColor.FromRgb(16, 185, 129));
        app.Resources["BrushNavActiveText"] = new SolidColorBrush(MediaColor.FromRgb(255, 255, 255));
        app.Resources["BrushNavHoverBg"] = new SolidColorBrush(MediaColor.FromRgb(18, 41, 30));
        app.Resources["BrushNavInactiveText"] = new SolidColorBrush(MediaColor.FromRgb(110, 231, 183));

        // Alert Banner
        app.Resources["BrushAlertBg"] = new SolidColorBrush(MediaColor.FromRgb(17, 48, 35));
        app.Resources["BrushAlertBorder"] = new SolidColorBrush(MediaColor.FromRgb(16, 185, 129));
        app.Resources["BrushAlertText"] = new SolidColorBrush(MediaColor.FromRgb(167, 243, 208));

        // Badges
        app.Resources["BrushBadgeBg"] = new SolidColorBrush(MediaColor.FromRgb(17, 48, 35));
        app.Resources["BrushBadgeBorder"] = new SolidColorBrush(MediaColor.FromRgb(26, 64, 46));
        app.Resources["BrushBadgeText"] = new SolidColorBrush(MediaColor.FromRgb(110, 231, 183));

        app.Resources["BrushModelBadgeBg"] = new SolidColorBrush(MediaColor.FromRgb(17, 48, 35));
        app.Resources["BrushModelBadgeBorder"] = new SolidColorBrush(MediaColor.FromRgb(26, 64, 46));
        app.Resources["BrushModelBadgeText"] = new SolidColorBrush(MediaColor.FromRgb(110, 231, 183));

        // Progress & Mono
        app.Resources["BrushProgressTrack"] = new SolidColorBrush(MediaColor.FromRgb(18, 41, 30));
        app.Resources["BrushMonoPath"] = new SolidColorBrush(MediaColor.FromRgb(52, 211, 153));

        // Quota Alert
        app.Resources["BrushQuotaExhaustedBg"] = new SolidColorBrush(MediaColor.FromRgb(49, 17, 24));
        app.Resources["BrushQuotaExhaustedBorder"] = new SolidColorBrush(MediaColor.FromRgb(239, 68, 68));
        app.Resources["BrushQuotaExhaustedText"] = new SolidColorBrush(MediaColor.FromRgb(254, 202, 202));
    }
}
