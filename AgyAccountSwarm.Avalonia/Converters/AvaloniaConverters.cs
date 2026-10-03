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
            return b ? new SolidColorBrush(Color.Parse("#EF4444")) : new SolidColorBrush(Color.Parse("#10B981"));
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
/// Compares CurrentPage with parameter. Returns #1E293B if active, otherwise Transparent.
/// </summary>
public class ActiveNavBackgroundConverter : IValueConverter
{
    public static readonly ActiveNavBackgroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isActive = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
        return isActive ? new SolidColorBrush(Color.Parse("#1E293B")) : Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Compares CurrentPage with parameter. Returns #38BDF8 if active, otherwise #E2E8F0.
/// </summary>
public class ActiveNavForegroundConverter : IValueConverter
{
    public static readonly ActiveNavForegroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isActive = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
        return isActive ? new SolidColorBrush(Color.Parse("#38BDF8")) : new SolidColorBrush(Color.Parse("#E2E8F0"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Converts a local image path string into an Avalonia Bitmap.
/// </summary>
public class PathToBitmapConverter : IValueConverter
{
    public static readonly PathToBitmapConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
        {
            try
            {
                return new global::Avalonia.Media.Imaging.Bitmap(path);
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
        if (value is AgyAccountSwarm.Models.SwarmMessageType type)
        {
            return type switch
            {
                AgyAccountSwarm.Models.SwarmMessageType.UserBroadcast => new SolidColorBrush(Color.Parse("#0F1E36")),
                AgyAccountSwarm.Models.SwarmMessageType.AgentAction => new SolidColorBrush(Color.Parse("#062E25")),
                AgyAccountSwarm.Models.SwarmMessageType.SystemEvent => new SolidColorBrush(Color.Parse("#131927")),
                AgyAccountSwarm.Models.SwarmMessageType.Handoff => new SolidColorBrush(Color.Parse("#2E1065")),
                _ => new SolidColorBrush(Color.Parse("#111827"))
            };
        }
        return new SolidColorBrush(Color.Parse("#111827"));
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
        if (value is AgyAccountSwarm.Models.SwarmMessageType type)
        {
            return type switch
            {
                AgyAccountSwarm.Models.SwarmMessageType.UserBroadcast => new SolidColorBrush(Color.Parse("#2563EB")),
                AgyAccountSwarm.Models.SwarmMessageType.AgentAction => new SolidColorBrush(Color.Parse("#059669")),
                AgyAccountSwarm.Models.SwarmMessageType.SystemEvent => new SolidColorBrush(Color.Parse("#4F46E5")),
                AgyAccountSwarm.Models.SwarmMessageType.Handoff => new SolidColorBrush(Color.Parse("#9333EA")),
                _ => new SolidColorBrush(Color.Parse("#1F2937"))
            };
        }
        return new SolidColorBrush(Color.Parse("#1F2937"));
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

