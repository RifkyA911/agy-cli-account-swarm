using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AgyAccountSwarm.Avalonia.Converters;

/// <summary>
/// Compares a value against a parameter string and returns true if equal.
/// </summary>
public class EqualityToBooleanConverter : IValueConverter
{
    public static readonly EqualityToBooleanConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null && parameter == null) return true;
        if (value == null || parameter == null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Maps a subscription tier name (Basic, Plus, Pro, Ultra) to a themed SolidColorBrush.
/// </summary>
public class TierToBrushConverter : IValueConverter
{
    public static readonly TierToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var tier = value?.ToString()?.ToUpperInvariant() ?? "";
        return tier switch
        {
            "PRO" => new SolidColorBrush(Color.Parse("#38BDF8")),    // Sky Blue
            "ULTRA" => new SolidColorBrush(Color.Parse("#A855F7")),  // Violet
            "PLUS" => new SolidColorBrush(Color.Parse("#10B981")),   // Emerald
            "BASIC" => new SolidColorBrush(Color.Parse("#6B7280")),  // Slate Gray
            _ => new SolidColorBrush(Color.Parse("#9CA3AF"))
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Maps a quota percentage value to an alert brush (Emerald for &lt;70%, Amber for 70-90%, Crimson for &gt;90%).
/// </summary>
public class QuotaToBrushConverter : IValueConverter
{
    public static readonly QuotaToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? new SolidColorBrush(Color.Parse("#10B981")) : new SolidColorBrush(Color.Parse("#EF4444"));
        }

        double percent = 0;
        if (value is double d) percent = d;
        else if (value is int i) percent = i;
        else if (double.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            percent = parsed;

        if (percent >= 90) return new SolidColorBrush(Color.Parse("#EF4444")); // Crimson
        if (percent >= 70) return new SolidColorBrush(Color.Parse("#F59E0B")); // Amber
        return new SolidColorBrush(Color.Parse("#10B981"));                     // Emerald
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Formats a hex color string (e.g. #3B82F6) into a SolidColorBrush.
/// </summary>
public class HexToBrushConverter : IValueConverter
{
    public static readonly HexToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                return new SolidColorBrush(Color.Parse(hex));
            }
            catch
            {
                // Fallback
            }
        }
        return new SolidColorBrush(Color.Parse("#3B82F6"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// <summary>
/// Compares CurrentPage with parameter. Returns theme-aware background brush if active, otherwise Transparent.
/// </summary>
public class ActiveNavBackgroundConverter : IValueConverter
{
    public static readonly ActiveNavBackgroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isActive = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
        if (!isActive) return Brushes.Transparent;

        if (global::Avalonia.Application.Current?.Resources.TryGetResource("BrushNavActiveBg", null, out var res) == true && res is IBrush brush)
            return brush;

        bool isLight = string.Equals(AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.CurrentTheme, "Light", StringComparison.OrdinalIgnoreCase)
                       || (string.Equals(AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.CurrentTheme, "System", StringComparison.OrdinalIgnoreCase) && AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.DetectOsTheme() == "Light");

        return isLight ? new SolidColorBrush(Color.Parse("#EEF2FF")) : new SolidColorBrush(Color.Parse("#1E293B"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Compares CurrentPage with parameter. Returns theme-aware border brush if active, otherwise Transparent.
/// </summary>
public class ActiveNavBorderConverter : IValueConverter
{
    public static readonly ActiveNavBorderConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isActive = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
        if (!isActive) return Brushes.Transparent;

        if (global::Avalonia.Application.Current?.Resources.TryGetResource("BrushNavActiveBorder", null, out var res) == true && res is IBrush brush)
            return brush;

        bool isLight = string.Equals(AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.CurrentTheme, "Light", StringComparison.OrdinalIgnoreCase)
                       || (string.Equals(AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.CurrentTheme, "System", StringComparison.OrdinalIgnoreCase) && AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.DetectOsTheme() == "Light");

        return isLight ? new SolidColorBrush(Color.Parse("#C7D2FE")) : new SolidColorBrush(Color.Parse("#3B82F6"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Compares CurrentPage with parameter. Returns theme-aware foreground brush.
/// </summary>
public class ActiveNavForegroundConverter : IValueConverter
{
    public static readonly ActiveNavForegroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isActive = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
        if (isActive)
        {
            if (global::Avalonia.Application.Current?.Resources.TryGetResource("BrushNavActiveText", null, out var res) == true && res is IBrush brush)
                return brush;

            bool isLight = string.Equals(AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.CurrentTheme, "Light", StringComparison.OrdinalIgnoreCase)
                           || (string.Equals(AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.CurrentTheme, "System", StringComparison.OrdinalIgnoreCase) && AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.DetectOsTheme() == "Light");

            return isLight ? new SolidColorBrush(Color.Parse("#1E3A8A")) : new SolidColorBrush(Color.Parse("#38BDF8"));
        }

        if (global::Avalonia.Application.Current?.Resources.TryGetResource("BrushNavInactiveText", null, out var inact) == true && inact is IBrush inactBrush)
            return inactBrush;

        bool isLightInact = string.Equals(AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.CurrentTheme, "Light", StringComparison.OrdinalIgnoreCase)
                            || (string.Equals(AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.CurrentTheme, "System", StringComparison.OrdinalIgnoreCase) && AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.DetectOsTheme() == "Light");

        return isLightInact ? new SolidColorBrush(Color.Parse("#475569")) : new SolidColorBrush(Color.Parse("#94A3B8"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Compares CurrentPage with parameter. Returns FontWeight.Bold if active, otherwise FontWeight.Normal.
/// </summary>
public class PageToFontWeightConverter : IValueConverter
{
    public static readonly PageToFontWeightConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isActive = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
        return isActive ? FontWeight.Bold : FontWeight.Normal;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Converts a local image path string into an Avalonia Bitmap.
/// </summary>
public class PathToBitmapConverter : IValueConverter
{
    public static readonly PathToBitmapConverter Instance = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, global::Avalonia.Media.Imaging.Bitmap> _bitmapCache = new(StringComparer.OrdinalIgnoreCase);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is global::Avalonia.Media.Imaging.Bitmap directBmp)
            return directBmp;

        if (value is string path && !string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
        {
            try
            {
                return _bitmapCache.GetOrAdd(path, p => new global::Avalonia.Media.Imaging.Bitmap(p));
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Maps a SwarmMessageType enum value to a themed background brush.
/// </summary>
public class SwarmMessageTypeToBackgroundConverter : IValueConverter
{
    public static readonly SwarmMessageTypeToBackgroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isLight = string.Equals(AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.CurrentTheme, "Light", StringComparison.OrdinalIgnoreCase)
                       || (string.Equals(AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.CurrentTheme, "System", StringComparison.OrdinalIgnoreCase) && AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.DetectOsTheme() == "Light");

        if (value is AgyAccountSwarm.Models.SwarmMessageType type)
        {
            if (isLight)
            {
                return type switch
                {
                    AgyAccountSwarm.Models.SwarmMessageType.UserBroadcast => new SolidColorBrush(Color.Parse("#EFF6FF")), // Soft Blue
                    AgyAccountSwarm.Models.SwarmMessageType.AgentAction => new SolidColorBrush(Color.Parse("#ECFDF5")),   // Soft Emerald
                    AgyAccountSwarm.Models.SwarmMessageType.SystemEvent => new SolidColorBrush(Color.Parse("#F8FAFC")),   // Slate 50
                    AgyAccountSwarm.Models.SwarmMessageType.Handoff => new SolidColorBrush(Color.Parse("#FAF5FF")),       // Soft Purple
                    _ => new SolidColorBrush(Color.Parse("#F8FAFC"))
                };
            }

            return type switch
            {
                AgyAccountSwarm.Models.SwarmMessageType.UserBroadcast => new SolidColorBrush(Color.Parse("#0F1E36")),
                AgyAccountSwarm.Models.SwarmMessageType.AgentAction => new SolidColorBrush(Color.Parse("#062E25")),
                AgyAccountSwarm.Models.SwarmMessageType.SystemEvent => new SolidColorBrush(Color.Parse("#131927")),
                AgyAccountSwarm.Models.SwarmMessageType.Handoff => new SolidColorBrush(Color.Parse("#2E1065")),
                _ => new SolidColorBrush(Color.Parse("#111827"))
            };
        }
        return isLight ? new SolidColorBrush(Color.Parse("#FFFFFF")) : new SolidColorBrush(Color.Parse("#111827"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Maps a SwarmMessageType enum value to a themed border brush.
/// </summary>
public class SwarmMessageTypeToBorderConverter : IValueConverter
{
    public static readonly SwarmMessageTypeToBorderConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isLight = string.Equals(AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.CurrentTheme, "Light", StringComparison.OrdinalIgnoreCase)
                       || (string.Equals(AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.CurrentTheme, "System", StringComparison.OrdinalIgnoreCase) && AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.DetectOsTheme() == "Light");

        if (value is AgyAccountSwarm.Models.SwarmMessageType type)
        {
            if (isLight)
            {
                return type switch
                {
                    AgyAccountSwarm.Models.SwarmMessageType.UserBroadcast => new SolidColorBrush(Color.Parse("#BFDBFE")), // Blue 200
                    AgyAccountSwarm.Models.SwarmMessageType.AgentAction => new SolidColorBrush(Color.Parse("#A7F3D0")),   // Emerald 200
                    AgyAccountSwarm.Models.SwarmMessageType.SystemEvent => new SolidColorBrush(Color.Parse("#E2E8F0")),   // Slate 200
                    AgyAccountSwarm.Models.SwarmMessageType.Handoff => new SolidColorBrush(Color.Parse("#E9D5FF")),       // Purple 200
                    _ => new SolidColorBrush(Color.Parse("#E2E8F0"))
                };
            }

            return type switch
            {
                AgyAccountSwarm.Models.SwarmMessageType.UserBroadcast => new SolidColorBrush(Color.Parse("#2563EB")),
                AgyAccountSwarm.Models.SwarmMessageType.AgentAction => new SolidColorBrush(Color.Parse("#059669")),
                AgyAccountSwarm.Models.SwarmMessageType.SystemEvent => new SolidColorBrush(Color.Parse("#4F46E5")),
                AgyAccountSwarm.Models.SwarmMessageType.Handoff => new SolidColorBrush(Color.Parse("#9333EA")),
                _ => new SolidColorBrush(Color.Parse("#1F2937"))
            };
        }
        return isLight ? new SolidColorBrush(Color.Parse("#E2E8F0")) : new SolidColorBrush(Color.Parse("#1F2937"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Compares SelectedProjectTabIndex with parameter. Returns themed background if active, otherwise Transparent.
/// </summary>
public class ActiveTabBackgroundConverter : IValueConverter
{
    public static readonly ActiveTabBackgroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isActive = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
        if (!isActive) return Brushes.Transparent;

        if (global::Avalonia.Application.Current?.Resources.TryGetResource("BrushSurfaceHover", null, out var res) == true && res is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Color.Parse("#1E293B"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Compares SelectedProjectTabIndex with parameter. Returns BrushAccent if active, otherwise BrushTextSecondary.
/// </summary>
public class ActiveTabForegroundConverter : IValueConverter
{
    public static readonly ActiveTabForegroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isActive = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
        if (isActive)
        {
            if (global::Avalonia.Application.Current?.Resources.TryGetResource("BrushAccent", null, out var res) == true && res is IBrush b)
                return b;
            return new SolidColorBrush(Color.Parse("#38BDF8"));
        }

        if (global::Avalonia.Application.Current?.Resources.TryGetResource("BrushTextSecondary", null, out var sec) == true && sec is IBrush bSec)
            return bSec;
        return new SolidColorBrush(Color.Parse("#9CA3AF"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Converts an integer count into a boolean (true if count &gt; 0, inverted if parameter is 'Inverse').
/// </summary>
public class CountToBooleanConverter : IValueConverter
{
    public static readonly CountToBooleanConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int count = 0;
        if (value is int i) count = i;
        else if (int.TryParse(value?.ToString(), out var parsed)) count = parsed;

        bool isInverse = string.Equals(parameter?.ToString(), "Inverse", StringComparison.OrdinalIgnoreCase);
        bool hasItems = count > 0;
        return isInverse ? !hasItems : hasItems;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Converts a boolean into a chevron string (▴ for true, ▾ for false).
/// </summary>
public class BoolToChevronConverter : IValueConverter
{
    public static readonly BoolToChevronConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? "▴" : "▾";
        }
        return "▾";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Maps a boolean hasActiveFilters to an accent brush or muted text brush.
/// </summary>
public class BoolToFilterAccentConverter : IValueConverter
{
    public static readonly BoolToFilterAccentConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b)
        {
            return new SolidColorBrush(Color.Parse("#3B82F6"));
        }
        if (global::Avalonia.Application.Current?.Resources.TryGetResource("BrushTextSecondary", null, out var res) == true && res is IBrush brush)
            return brush;
        return new SolidColorBrush(Color.Parse("#94A3B8"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Checks whether a string or object is null or empty.
/// </summary>
public class NullOrEmptyToBooleanConverter : IValueConverter
{
    public static readonly NullOrEmptyToBooleanConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool hasValue = !string.IsNullOrWhiteSpace(value?.ToString());
        if (string.Equals(parameter?.ToString(), "Inverse", StringComparison.OrdinalIgnoreCase))
            return !hasValue;
        return hasValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Inverts a boolean value.
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public static readonly InverseBooleanConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Converts an integer count into a boolean (true if count > 0).
/// </summary>
public class IntToBooleanConverter : IValueConverter
{
    public static readonly IntToBooleanConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int count = 0;
        if (value is int i) count = i;
        else if (int.TryParse(value?.ToString(), out var p)) count = p;

        bool hasCount = count > 0;
        if (string.Equals(parameter?.ToString(), "Inverse", StringComparison.OrdinalIgnoreCase))
            return !hasCount;
        return hasCount;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Maps a role name (Architect, Implementer, Reviewer, Commander, System, etc.) to a themed badge background brush.
/// </summary>
public class RoleToBackgroundConverter : IValueConverter
{
    public static readonly RoleToBackgroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var role = value?.ToString()?.Trim() ?? string.Empty;
        bool isLight = string.Equals(AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.CurrentTheme, "Light", StringComparison.OrdinalIgnoreCase)
                       || (string.Equals(AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.CurrentTheme, "System", StringComparison.OrdinalIgnoreCase) && AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.DetectOsTheme() == "Light");

        if (role.Contains("Architect", StringComparison.OrdinalIgnoreCase))
            return isLight ? new SolidColorBrush(Color.Parse("#F5F3FF")) : new SolidColorBrush(Color.Parse("#241846"));
        if (role.Contains("Implementer", StringComparison.OrdinalIgnoreCase) || role.Contains("Worker", StringComparison.OrdinalIgnoreCase))
            return isLight ? new SolidColorBrush(Color.Parse("#ECFDF5")) : new SolidColorBrush(Color.Parse("#063327"));
        if (role.Contains("Reviewer", StringComparison.OrdinalIgnoreCase) || role.Contains("Tester", StringComparison.OrdinalIgnoreCase) || role.Contains("QA", StringComparison.OrdinalIgnoreCase))
            return isLight ? new SolidColorBrush(Color.Parse("#ECFEFF")) : new SolidColorBrush(Color.Parse("#082C36"));
        if (role.Contains("Commander", StringComparison.OrdinalIgnoreCase) || role.Contains("User", StringComparison.OrdinalIgnoreCase))
            return isLight ? new SolidColorBrush(Color.Parse("#EFF6FF")) : new SolidColorBrush(Color.Parse("#0C2548"));
        if (role.Contains("System", StringComparison.OrdinalIgnoreCase) || role.Contains("Orchestrator", StringComparison.OrdinalIgnoreCase))
            return isLight ? new SolidColorBrush(Color.Parse("#FEF3C7")) : new SolidColorBrush(Color.Parse("#2D1E05"));
        if (role.Contains("Security", StringComparison.OrdinalIgnoreCase))
            return isLight ? new SolidColorBrush(Color.Parse("#FFF1F2")) : new SolidColorBrush(Color.Parse("#380B13"));

        return isLight ? new SolidColorBrush(Color.Parse("#F1F5F9")) : new SolidColorBrush(Color.Parse("#1E293B"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Maps a role name to a themed badge border brush.
/// </summary>
public class RoleToBorderConverter : IValueConverter
{
    public static readonly RoleToBorderConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var role = value?.ToString()?.Trim() ?? string.Empty;
        if (role.Contains("Architect", StringComparison.OrdinalIgnoreCase)) return new SolidColorBrush(Color.Parse("#8B5CF6"));
        if (role.Contains("Implementer", StringComparison.OrdinalIgnoreCase) || role.Contains("Worker", StringComparison.OrdinalIgnoreCase)) return new SolidColorBrush(Color.Parse("#10B981"));
        if (role.Contains("Reviewer", StringComparison.OrdinalIgnoreCase) || role.Contains("Tester", StringComparison.OrdinalIgnoreCase) || role.Contains("QA", StringComparison.OrdinalIgnoreCase)) return new SolidColorBrush(Color.Parse("#06B6D4"));
        if (role.Contains("Commander", StringComparison.OrdinalIgnoreCase) || role.Contains("User", StringComparison.OrdinalIgnoreCase)) return new SolidColorBrush(Color.Parse("#3B82F6"));
        if (role.Contains("System", StringComparison.OrdinalIgnoreCase) || role.Contains("Orchestrator", StringComparison.OrdinalIgnoreCase)) return new SolidColorBrush(Color.Parse("#F59E0B"));
        if (role.Contains("Security", StringComparison.OrdinalIgnoreCase)) return new SolidColorBrush(Color.Parse("#F43F5E"));

        return new SolidColorBrush(Color.Parse("#94A3B8"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Maps a role name to a themed badge foreground brush.
/// </summary>
public class RoleToForegroundConverter : IValueConverter
{
    public static readonly RoleToForegroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var role = value?.ToString()?.Trim() ?? string.Empty;
        bool isLight = string.Equals(AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.CurrentTheme, "Light", StringComparison.OrdinalIgnoreCase)
                       || (string.Equals(AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.CurrentTheme, "System", StringComparison.OrdinalIgnoreCase) && AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.DetectOsTheme() == "Light");

        if (role.Contains("Architect", StringComparison.OrdinalIgnoreCase))
            return isLight ? new SolidColorBrush(Color.Parse("#7C3AED")) : new SolidColorBrush(Color.Parse("#C4B5FD"));
        if (role.Contains("Implementer", StringComparison.OrdinalIgnoreCase) || role.Contains("Worker", StringComparison.OrdinalIgnoreCase))
            return isLight ? new SolidColorBrush(Color.Parse("#059669")) : new SolidColorBrush(Color.Parse("#34D399"));
        if (role.Contains("Reviewer", StringComparison.OrdinalIgnoreCase) || role.Contains("Tester", StringComparison.OrdinalIgnoreCase) || role.Contains("QA", StringComparison.OrdinalIgnoreCase))
            return isLight ? new SolidColorBrush(Color.Parse("#0891B2")) : new SolidColorBrush(Color.Parse("#67E8F9"));
        if (role.Contains("Commander", StringComparison.OrdinalIgnoreCase) || role.Contains("User", StringComparison.OrdinalIgnoreCase))
            return isLight ? new SolidColorBrush(Color.Parse("#2563EB")) : new SolidColorBrush(Color.Parse("#60A5FA"));
        if (role.Contains("System", StringComparison.OrdinalIgnoreCase) || role.Contains("Orchestrator", StringComparison.OrdinalIgnoreCase))
            return isLight ? new SolidColorBrush(Color.Parse("#B45309")) : new SolidColorBrush(Color.Parse("#FDE68A"));
        if (role.Contains("Security", StringComparison.OrdinalIgnoreCase))
            return isLight ? new SolidColorBrush(Color.Parse("#E11D48")) : new SolidColorBrush(Color.Parse("#FDA4AF"));

        return isLight ? new SolidColorBrush(Color.Parse("#475569")) : new SolidColorBrush(Color.Parse("#CBD5E1"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Maps a log level (INFO, DEBUG, WARN, ERROR, SUCCESS) to a themed badge background brush.
/// </summary>
public class LogLevelToBackgroundConverter : IValueConverter
{
    public static readonly LogLevelToBackgroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var level = value?.ToString()?.Trim().ToUpperInvariant() ?? "INFO";
        bool isLight = string.Equals(AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.CurrentTheme, "Light", StringComparison.OrdinalIgnoreCase)
                       || (string.Equals(AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.CurrentTheme, "System", StringComparison.OrdinalIgnoreCase) && AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.DetectOsTheme() == "Light");

        return level switch
        {
            "ERROR" => isLight ? new SolidColorBrush(Color.Parse("#FEE2E2")) : new SolidColorBrush(Color.Parse("#380B13")),
            "WARN" or "WARNING" => isLight ? new SolidColorBrush(Color.Parse("#FEF3C7")) : new SolidColorBrush(Color.Parse("#2D1E05")),
            "SUCCESS" => isLight ? new SolidColorBrush(Color.Parse("#ECFDF5")) : new SolidColorBrush(Color.Parse("#063327")),
            "DEBUG" => isLight ? new SolidColorBrush(Color.Parse("#F1F5F9")) : new SolidColorBrush(Color.Parse("#1E222A")),
            _ => isLight ? new SolidColorBrush(Color.Parse("#EFF6FF")) : new SolidColorBrush(Color.Parse("#0C2548")) // INFO
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Maps a log level to a themed badge border brush.
/// </summary>
public class LogLevelToBorderConverter : IValueConverter
{
    public static readonly LogLevelToBorderConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var level = value?.ToString()?.Trim().ToUpperInvariant() ?? "INFO";
        return level switch
        {
            "ERROR" => new SolidColorBrush(Color.Parse("#EF4444")),
            "WARN" or "WARNING" => new SolidColorBrush(Color.Parse("#F59E0B")),
            "SUCCESS" => new SolidColorBrush(Color.Parse("#10B981")),
            "DEBUG" => new SolidColorBrush(Color.Parse("#64748B")),
            _ => new SolidColorBrush(Color.Parse("#3B82F6")) // INFO
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Maps a log level to a themed badge foreground brush.
/// </summary>
public class LogLevelToForegroundConverter : IValueConverter
{
    public static readonly LogLevelToForegroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var level = value?.ToString()?.Trim().ToUpperInvariant() ?? "INFO";
        bool isLight = string.Equals(AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.CurrentTheme, "Light", StringComparison.OrdinalIgnoreCase)
                       || (string.Equals(AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.CurrentTheme, "System", StringComparison.OrdinalIgnoreCase) && AgyAccountSwarm.Avalonia.Services.AvaloniaThemeManager.DetectOsTheme() == "Light");

        return level switch
        {
            "ERROR" => isLight ? new SolidColorBrush(Color.Parse("#DC2626")) : new SolidColorBrush(Color.Parse("#FCA5A5")),
            "WARN" or "WARNING" => isLight ? new SolidColorBrush(Color.Parse("#B45309")) : new SolidColorBrush(Color.Parse("#FDE68A")),
            "SUCCESS" => isLight ? new SolidColorBrush(Color.Parse("#059669")) : new SolidColorBrush(Color.Parse("#34D399")),
            "DEBUG" => isLight ? new SolidColorBrush(Color.Parse("#64748B")) : new SolidColorBrush(Color.Parse("#94A3B8")),
            _ => isLight ? new SolidColorBrush(Color.Parse("#2563EB")) : new SolidColorBrush(Color.Parse("#60A5FA")) // INFO
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

