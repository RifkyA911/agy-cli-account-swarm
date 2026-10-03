using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace AgyAccountSwarm.Avalonia.Services;

/// <summary>
/// Manages dynamic theming for the Avalonia desktop application across Linux, macOS, and Windows.
/// Mirrors WPF ThemeManager color palettes 1:1 (Dark, Light, Cyberpunk, Matrix).
/// </summary>
public static class AvaloniaThemeManager
{
    public static string CurrentTheme { get; private set; } = "Dark";

    public static readonly string[] AvailableThemes = ["System", "Dark", "Light", "Cyberpunk", "Matrix"];

    public static event Action<string>? ThemeChanged;

    public static string DetectOsTheme()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var val = key?.GetValue("AppsUseLightTheme");
                if (val is int intVal && intVal == 1) return "Light";
            }
            else if (OperatingSystem.IsMacOS())
            {
                // In macOS, AppleInterfaceStyle == "Dark"
                var psi = new System.Diagnostics.ProcessStartInfo("defaults", "read -g AppleInterfaceStyle")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc != null)
                {
                    var output = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit();
                    return output.Equals("Dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                // Linux GNOME/Freedesktop color-scheme check
                var psi = new System.Diagnostics.ProcessStartInfo("gsettings", "get org.gnome.desktop.interface color-scheme")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc != null)
                {
                    var output = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit();
                    if (output.Contains("dark", StringComparison.OrdinalIgnoreCase)) return "Dark";
                    if (output.Contains("light", StringComparison.OrdinalIgnoreCase)) return "Light";
                }
            }
        }
        catch { }

        return "Dark";
    }

    public static void ApplyTheme(string theme)
    {
        var app = Application.Current;
        if (app == null) return;

        CurrentTheme = theme;

        if (string.Equals(theme, "System", StringComparison.OrdinalIgnoreCase))
        {
            string osTheme = DetectOsTheme();
            if (osTheme == "Light")
                ApplyLightTheme(app);
            else
                ApplyDarkTheme(app);
            ThemeChanged?.Invoke(CurrentTheme);
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

        ThemeChanged?.Invoke(CurrentTheme);
    }

    private static void SetBrush(Application app, string key, Color color)
    {
        app.Resources[key] = new SolidColorBrush(color);
    }

    private static void ApplyDarkTheme(Application app)
    {
        app.RequestedThemeVariant = ThemeVariant.Dark;

        // Surfaces & Text (Obsidian Dark)
        SetBrush(app, "BrushBg", Color.FromRgb(15, 17, 21));
        SetBrush(app, "BrushSidebar", Color.FromRgb(15, 23, 42));
        SetBrush(app, "BrushSurface", Color.FromRgb(23, 25, 30));
        SetBrush(app, "BrushSurfaceHover", Color.FromRgb(31, 34, 42));
        SetBrush(app, "BrushSurfacePressed", Color.FromRgb(37, 41, 54));
        SetBrush(app, "BrushDangerHoverBg", Color.FromRgb(51, 20, 24));
        SetBrush(app, "BrushCard", Color.FromRgb(19, 21, 26));
        SetBrush(app, "BrushCardBorder", Color.FromRgb(37, 40, 50));
        SetBrush(app, "BrushTextPrimary", Color.FromRgb(243, 244, 246));
        SetBrush(app, "BrushTextSecondary", Color.FromRgb(156, 163, 175));
        SetBrush(app, "BrushTextMuted", Color.FromRgb(107, 114, 128));
        SetBrush(app, "BrushPopupBg", Color.FromRgb(23, 25, 30));

        // Primary Accent
        SetBrush(app, "BrushPrimary", Color.FromRgb(37, 99, 235));
        SetBrush(app, "BrushPrimaryHover", Color.FromRgb(29, 78, 216));
        SetBrush(app, "BrushAccent", Color.FromRgb(56, 189, 248));   // Sky blue #38BDF8
        SetBrush(app, "BrushEmerald", Color.FromRgb(16, 185, 129));
        SetBrush(app, "BrushAmber", Color.FromRgb(245, 158, 11));
        SetBrush(app, "BrushCrimson", Color.FromRgb(239, 68, 68));
        SetBrush(app, "BrushViolet", Color.FromRgb(168, 85, 247));

        // Nav Sidebar
        SetBrush(app, "BrushNavActiveBg", Color.FromRgb(30, 41, 59));
        SetBrush(app, "BrushNavActiveBorder", Color.FromRgb(59, 130, 246));
        SetBrush(app, "BrushNavActiveText", Color.FromRgb(248, 250, 252));
        SetBrush(app, "BrushNavHoverBg", Color.FromRgb(31, 34, 42));
        SetBrush(app, "BrushNavInactiveText", Color.FromRgb(156, 163, 175));

        // Alert Banner
        SetBrush(app, "BrushAlertBg", Color.FromRgb(30, 41, 59));
        SetBrush(app, "BrushAlertBorder", Color.FromRgb(59, 130, 246));
        SetBrush(app, "BrushAlertText", Color.FromRgb(147, 197, 253));

        // Badges
        SetBrush(app, "BrushBadgeBg", Color.FromRgb(30, 41, 59));
        SetBrush(app, "BrushBadgeBorder", Color.FromRgb(51, 65, 85));
        SetBrush(app, "BrushBadgeText", Color.FromRgb(147, 197, 253));

        SetBrush(app, "BrushModelBadgeBg", Color.FromRgb(30, 41, 59));
        SetBrush(app, "BrushModelBadgeBorder", Color.FromRgb(51, 65, 85));
        SetBrush(app, "BrushModelBadgeText", Color.FromRgb(147, 197, 253));

        // Progress & Mono
        SetBrush(app, "BrushProgressTrack", Color.FromRgb(31, 36, 48));
        SetBrush(app, "BrushMonoPath", Color.FromRgb(96, 165, 250));

        // Quota Alert
        SetBrush(app, "BrushQuotaExhaustedBg", Color.FromRgb(49, 17, 24));
        SetBrush(app, "BrushQuotaExhaustedBorder", Color.FromRgb(220, 38, 38));
        SetBrush(app, "BrushQuotaExhaustedText", Color.FromRgb(252, 165, 165));

        // Warning / Error Alerts
        SetBrush(app, "BrushWarningAlertBg", Color.FromRgb(36, 26, 6));
        SetBrush(app, "BrushWarningAlertBorder", Color.FromRgb(217, 119, 6));
        SetBrush(app, "BrushWarningAlertText", Color.FromRgb(253, 230, 138));

        SetBrush(app, "BrushErrorAlertBg", Color.FromRgb(42, 14, 19));
        SetBrush(app, "BrushErrorAlertBorder", Color.FromRgb(220, 38, 38));
        SetBrush(app, "BrushErrorAlertText", Color.FromRgb(252, 165, 165));
    }

    private static void ApplyLightTheme(Application app)
    {
        app.RequestedThemeVariant = ThemeVariant.Light;

        // Surfaces & Text (Daylight Clean - Pure White Sidebar and Cards)
        SetBrush(app, "BrushBg", Color.FromRgb(248, 250, 252));        // Slate 50 (#F8FAFC)
        SetBrush(app, "BrushSidebar", Color.FromRgb(255, 255, 255));   // Pure White (#FFFFFF)
        SetBrush(app, "BrushSurface", Color.FromRgb(255, 255, 255));   // Pure White (#FFFFFF)
        SetBrush(app, "BrushSurfaceHover", Color.FromRgb(241, 245, 249)); // Slate 100 (#F1F5F9)
        SetBrush(app, "BrushSurfacePressed", Color.FromRgb(226, 232, 240)); // Slate 200 (#E2E8F0)
        SetBrush(app, "BrushDangerHoverBg", Color.FromRgb(254, 226, 226)); // Red 100 (#FEE2E2)
        SetBrush(app, "BrushCard", Color.FromRgb(255, 255, 255));      // White Card (#FFFFFF)
        SetBrush(app, "BrushCardBorder", Color.FromRgb(226, 232, 240)); // Slate 200 (#E2E8F0)
        SetBrush(app, "BrushTextPrimary", Color.FromRgb(15, 23, 42));  // Slate 900 (#0F172A)
        SetBrush(app, "BrushTextSecondary", Color.FromRgb(71, 85, 105)); // Slate 600 (#475569)
        SetBrush(app, "BrushTextMuted", Color.FromRgb(148, 163, 184)); // Slate 400 (#94A3B8)
        SetBrush(app, "BrushPopupBg", Color.FromRgb(255, 255, 255));

        // Primary Accent
        SetBrush(app, "BrushPrimary", Color.FromRgb(37, 99, 235));     // Blue 600 (#2563EB)
        SetBrush(app, "BrushPrimaryHover", Color.FromRgb(29, 78, 216));
        SetBrush(app, "BrushAccent", Color.FromRgb(2, 132, 199));     // Sky 600
        SetBrush(app, "BrushEmerald", Color.FromRgb(5, 150, 105));    // Emerald 600
        SetBrush(app, "BrushAmber", Color.FromRgb(217, 119, 6));      // Amber 600
        SetBrush(app, "BrushCrimson", Color.FromRgb(220, 38, 38));    // Red 600
        SetBrush(app, "BrushViolet", Color.FromRgb(124, 58, 237));    // Violet 600

        // Nav Sidebar (Crisp High-Contrast layout.png parity)
        SetBrush(app, "BrushNavActiveBg", Color.FromRgb(238, 242, 255));   // Indigo 50 (#EEF2FF)
        SetBrush(app, "BrushNavActiveBorder", Color.FromRgb(199, 210, 254)); // Indigo 200 (#C7D2FE)
        SetBrush(app, "BrushNavActiveText", Color.FromRgb(30, 58, 138));   // Indigo 900 (#1E3A8A)
        SetBrush(app, "BrushNavHoverBg", Color.FromRgb(241, 245, 249));    // Slate 100
        SetBrush(app, "BrushNavInactiveText", Color.FromRgb(71, 85, 105)); // Slate 600

        // Alert Banner
        SetBrush(app, "BrushAlertBg", Color.FromRgb(239, 246, 255));       // Blue 50
        SetBrush(app, "BrushAlertBorder", Color.FromRgb(191, 219, 254));   // Blue 200
        SetBrush(app, "BrushAlertText", Color.FromRgb(29, 78, 216));       // Blue 700

        // Badges (Crisp pastel badges)
        SetBrush(app, "BrushBadgeBg", Color.FromRgb(239, 246, 255));       // Soft light blue #EFF6FF
        SetBrush(app, "BrushBadgeBorder", Color.FromRgb(219, 234, 254));   // Blue 100 #DBEAFE
        SetBrush(app, "BrushBadgeText", Color.FromRgb(37, 99, 235));       // Blue 600 #2563EB

        SetBrush(app, "BrushModelBadgeBg", Color.FromRgb(240, 253, 250));  // Teal 50
        SetBrush(app, "BrushModelBadgeBorder", Color.FromRgb(153, 246, 228));
        SetBrush(app, "BrushModelBadgeText", Color.FromRgb(15, 118, 110));

        // Progress & Mono
        SetBrush(app, "BrushProgressTrack", Color.FromRgb(226, 232, 240));
        SetBrush(app, "BrushMonoPath", Color.FromRgb(37, 99, 235));

        // Quota Alert
        SetBrush(app, "BrushQuotaExhaustedBg", Color.FromRgb(254, 242, 242));
        SetBrush(app, "BrushQuotaExhaustedBorder", Color.FromRgb(248, 113, 113));
        SetBrush(app, "BrushQuotaExhaustedText", Color.FromRgb(185, 28, 28));

        // Preflight & General Warning / Error Alerts (Soft Light Tint, High Contrast Text)
        SetBrush(app, "BrushWarningAlertBg", Color.FromRgb(254, 243, 199));   // Amber 100 (#FEF3C7)
        SetBrush(app, "BrushWarningAlertBorder", Color.FromRgb(245, 158, 11)); // Amber 500 (#F59E0B)
        SetBrush(app, "BrushWarningAlertText", Color.FromRgb(180, 83, 9));     // Amber 700 (#B45309)

        SetBrush(app, "BrushErrorAlertBg", Color.FromRgb(254, 226, 226));     // Red 100 (#FEE2E2)
        SetBrush(app, "BrushErrorAlertBorder", Color.FromRgb(239, 68, 68));     // Red 500 (#EF4444)
        SetBrush(app, "BrushErrorAlertText", Color.FromRgb(153, 27, 27));     // Red 800 (#991B1B)
    }

    private static void ApplyCyberpunkTheme(Application app)
    {
        app.RequestedThemeVariant = ThemeVariant.Dark;

        // Surfaces & Text (Neon Purple / Violet)
        SetBrush(app, "BrushBg", Color.FromRgb(13, 10, 24));           // #0D0A18
        SetBrush(app, "BrushSidebar", Color.FromRgb(21, 16, 38));      // #151026
        SetBrush(app, "BrushSurface", Color.FromRgb(21, 16, 38));
        SetBrush(app, "BrushSurfaceHover", Color.FromRgb(34, 26, 62));
        SetBrush(app, "BrushSurfacePressed", Color.FromRgb(45, 18, 77));
        SetBrush(app, "BrushDangerHoverBg", Color.FromRgb(60, 15, 30));
        SetBrush(app, "BrushCard", Color.FromRgb(25, 19, 44));         // #19132C
        SetBrush(app, "BrushCardBorder", Color.FromRgb(65, 45, 102));
        SetBrush(app, "BrushTextPrimary", Color.FromRgb(245, 243, 255));
        SetBrush(app, "BrushTextSecondary", Color.FromRgb(196, 181, 253));
        SetBrush(app, "BrushTextMuted", Color.FromRgb(139, 92, 246));
        SetBrush(app, "BrushPopupBg", Color.FromRgb(21, 16, 38));

        // Primary Accent
        SetBrush(app, "BrushPrimary", Color.FromRgb(168, 85, 247));
        SetBrush(app, "BrushPrimaryHover", Color.FromRgb(147, 51, 234));
        SetBrush(app, "BrushAccent", Color.FromRgb(192, 132, 252));
        SetBrush(app, "BrushEmerald", Color.FromRgb(16, 185, 129));
        SetBrush(app, "BrushAmber", Color.FromRgb(245, 158, 11));
        SetBrush(app, "BrushCrimson", Color.FromRgb(244, 63, 94));
        SetBrush(app, "BrushViolet", Color.FromRgb(168, 85, 247));

        // Nav Sidebar
        SetBrush(app, "BrushNavActiveBg", Color.FromRgb(55, 30, 85));
        SetBrush(app, "BrushNavActiveBorder", Color.FromRgb(168, 85, 247));
        SetBrush(app, "BrushNavActiveText", Color.FromRgb(255, 255, 255));
        SetBrush(app, "BrushNavHoverBg", Color.FromRgb(34, 26, 62));
        SetBrush(app, "BrushNavInactiveText", Color.FromRgb(196, 181, 253));

        // Alert Banner
        SetBrush(app, "BrushAlertBg", Color.FromRgb(46, 26, 71));
        SetBrush(app, "BrushAlertBorder", Color.FromRgb(168, 85, 247));
        SetBrush(app, "BrushAlertText", Color.FromRgb(233, 213, 255));

        // Badges
        SetBrush(app, "BrushBadgeBg", Color.FromRgb(46, 26, 71));
        SetBrush(app, "BrushBadgeBorder", Color.FromRgb(88, 50, 138));
        SetBrush(app, "BrushBadgeText", Color.FromRgb(216, 180, 254));

        SetBrush(app, "BrushModelBadgeBg", Color.FromRgb(46, 26, 71));
        SetBrush(app, "BrushModelBadgeBorder", Color.FromRgb(88, 50, 138));
        SetBrush(app, "BrushModelBadgeText", Color.FromRgb(216, 180, 254));

        // Progress & Mono
        SetBrush(app, "BrushProgressTrack", Color.FromRgb(34, 26, 62));
        SetBrush(app, "BrushMonoPath", Color.FromRgb(192, 132, 252));

        // Quota Alert
        SetBrush(app, "BrushQuotaExhaustedBg", Color.FromRgb(58, 14, 30));
        SetBrush(app, "BrushQuotaExhaustedBorder", Color.FromRgb(244, 63, 94));
        SetBrush(app, "BrushQuotaExhaustedText", Color.FromRgb(254, 205, 211));

        SetBrush(app, "BrushWarningAlertBg", Color.FromRgb(46, 28, 10));
        SetBrush(app, "BrushWarningAlertBorder", Color.FromRgb(245, 158, 11));
        SetBrush(app, "BrushWarningAlertText", Color.FromRgb(253, 230, 138));

        SetBrush(app, "BrushErrorAlertBg", Color.FromRgb(60, 15, 30));
        SetBrush(app, "BrushErrorAlertBorder", Color.FromRgb(239, 68, 68));
        SetBrush(app, "BrushErrorAlertText", Color.FromRgb(252, 165, 165));
    }

    private static void ApplyMatrixTheme(Application app)
    {
        app.RequestedThemeVariant = ThemeVariant.Dark;

        // Surfaces & Text (Hacker Terminal Emerald)
        SetBrush(app, "BrushBg", Color.FromRgb(7, 16, 12));             // #07100C
        SetBrush(app, "BrushSidebar", Color.FromRgb(11, 26, 19));        // #0B1A13
        SetBrush(app, "BrushSurface", Color.FromRgb(11, 26, 19));
        SetBrush(app, "BrushSurfaceHover", Color.FromRgb(18, 41, 30));
        SetBrush(app, "BrushSurfacePressed", Color.FromRgb(10, 41, 10));
        SetBrush(app, "BrushDangerHoverBg", Color.FromRgb(40, 20, 10));
        SetBrush(app, "BrushCard", Color.FromRgb(13, 31, 23));           // #0D1F17
        SetBrush(app, "BrushCardBorder", Color.FromRgb(26, 64, 46));
        SetBrush(app, "BrushTextPrimary", Color.FromRgb(236, 253, 245));
        SetBrush(app, "BrushTextSecondary", Color.FromRgb(110, 231, 183));
        SetBrush(app, "BrushTextMuted", Color.FromRgb(52, 211, 153));
        SetBrush(app, "BrushPopupBg", Color.FromRgb(11, 26, 19));

        // Primary Accent
        SetBrush(app, "BrushPrimary", Color.FromRgb(16, 185, 129));
        SetBrush(app, "BrushPrimaryHover", Color.FromRgb(5, 150, 105));
        SetBrush(app, "BrushAccent", Color.FromRgb(52, 211, 153));
        SetBrush(app, "BrushEmerald", Color.FromRgb(16, 185, 129));
        SetBrush(app, "BrushAmber", Color.FromRgb(245, 158, 11));
        SetBrush(app, "BrushCrimson", Color.FromRgb(239, 68, 68));
        SetBrush(app, "BrushViolet", Color.FromRgb(16, 185, 129));

        // Nav Sidebar
        SetBrush(app, "BrushNavActiveBg", Color.FromRgb(17, 48, 35));
        SetBrush(app, "BrushNavActiveBorder", Color.FromRgb(16, 185, 129));
        SetBrush(app, "BrushNavActiveText", Color.FromRgb(255, 255, 255));
        SetBrush(app, "BrushNavHoverBg", Color.FromRgb(18, 41, 30));
        SetBrush(app, "BrushNavInactiveText", Color.FromRgb(110, 231, 183));

        // Alert Banner
        SetBrush(app, "BrushAlertBg", Color.FromRgb(17, 48, 35));
        SetBrush(app, "BrushAlertBorder", Color.FromRgb(16, 185, 129));
        SetBrush(app, "BrushAlertText", Color.FromRgb(167, 243, 208));

        // Badges
        SetBrush(app, "BrushBadgeBg", Color.FromRgb(17, 48, 35));
        SetBrush(app, "BrushBadgeBorder", Color.FromRgb(26, 64, 46));
        SetBrush(app, "BrushBadgeText", Color.FromRgb(110, 231, 183));

        SetBrush(app, "BrushModelBadgeBg", Color.FromRgb(17, 48, 35));
        SetBrush(app, "BrushModelBadgeBorder", Color.FromRgb(26, 64, 46));
        SetBrush(app, "BrushModelBadgeText", Color.FromRgb(110, 231, 183));

        // Progress & Mono
        SetBrush(app, "BrushProgressTrack", Color.FromRgb(18, 41, 30));
        SetBrush(app, "BrushMonoPath", Color.FromRgb(52, 211, 153));

        // Quota Alert
        SetBrush(app, "BrushQuotaExhaustedBg", Color.FromRgb(49, 17, 24));
        SetBrush(app, "BrushQuotaExhaustedBorder", Color.FromRgb(239, 68, 68));
        SetBrush(app, "BrushQuotaExhaustedText", Color.FromRgb(254, 202, 202));

        SetBrush(app, "BrushWarningAlertBg", Color.FromRgb(20, 34, 10));
        SetBrush(app, "BrushWarningAlertBorder", Color.FromRgb(245, 158, 11));
        SetBrush(app, "BrushWarningAlertText", Color.FromRgb(253, 230, 138));

        SetBrush(app, "BrushErrorAlertBg", Color.FromRgb(40, 10, 15));
        SetBrush(app, "BrushErrorAlertBorder", Color.FromRgb(239, 68, 68));
        SetBrush(app, "BrushErrorAlertText", Color.FromRgb(252, 165, 165));
    }
}
