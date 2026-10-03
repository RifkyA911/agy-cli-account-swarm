using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AgyAccountSwarm.Services;
using WpfApp = System.Windows.Application;
using WpfBrush = System.Windows.Media.Brush;

namespace AgyAccountSwarm.Converters;

public class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
            }
            catch
            {
                // Fallback
            }
        }
        return new SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return false;
    }
}

public class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool hasContent = value != null && (value is not string s || !string.IsNullOrWhiteSpace(s));
        if (value is System.Collections.ICollection col)
        {
            hasContent = col.Count > 0;
        }
        else if (value is int count)
        {
            hasContent = count > 0;
        }

        if (Invert) hasContent = !hasContent;
        return hasContent ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BooleanToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool b = value is true;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class EqualityToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool matches = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
        return matches ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class PositiveIntToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int count = 0;
        if (value is int i) count = i;
        else if (value is long l) count = (int)l;
        else if (int.TryParse(value?.ToString(), out var parsed)) count = parsed;

        bool isInverse = string.Equals(parameter?.ToString(), "Inverse", StringComparison.OrdinalIgnoreCase);
        bool hasItems = count > 0;
        bool visible = isInverse ? !hasItems : hasItems;

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class PageToActiveBgConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool matches = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
        if (matches)
        {
            if (WpfApp.Current?.Resources["BrushNavActiveBg"] is WpfBrush brush) return brush;
            if (string.Equals(ThemeManager.CurrentTheme, "Light", StringComparison.OrdinalIgnoreCase))
                return (SolidColorBrush)new BrushConverter().ConvertFromString("#E0E7FF")!;
            return (SolidColorBrush)new BrushConverter().ConvertFromString("#1E293B")!;
        }
        return System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class PageToActiveBorderConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool matches = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
        if (matches)
        {
            if (WpfApp.Current?.Resources["BrushNavActiveBorder"] is WpfBrush brush) return brush;
            return (SolidColorBrush)new BrushConverter().ConvertFromString("#3B82F6")!;
        }
        return System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class PageToActiveTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool matches = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
        if (matches)
        {
            if (WpfApp.Current?.Resources["BrushNavActiveText"] is WpfBrush brush) return brush;
            if (string.Equals(ThemeManager.CurrentTheme, "Light", StringComparison.OrdinalIgnoreCase))
                return (SolidColorBrush)new BrushConverter().ConvertFromString("#1E3A8A")!;
            return System.Windows.Media.Brushes.White;
        }
        if (WpfApp.Current?.Resources["BrushNavInactiveText"] is WpfBrush inact) return inact;
        if (string.Equals(ThemeManager.CurrentTheme, "Light", StringComparison.OrdinalIgnoreCase))
            return (SolidColorBrush)new BrushConverter().ConvertFromString("#475569")!;
        return (SolidColorBrush)new BrushConverter().ConvertFromString("#9CA3AF")!;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class PageToFontWeightConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool matches = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
        return matches ? FontWeights.Bold : FontWeights.SemiBold;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BooleanToChevronConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "▼" : "▶";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToFilterAccentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
        {
            return (SolidColorBrush)new BrushConverter().ConvertFromString("#3B82F6")!;
        }
        if (WpfApp.Current?.Resources["BrushTextSecondary"] is WpfBrush brush) return brush;
        return (SolidColorBrush)new BrushConverter().ConvertFromString("#9CA3AF")!;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class PageToActiveBarConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool matches = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
        return matches ? (SolidColorBrush)new BrushConverter().ConvertFromString("#3B82F6")! : System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class SwarmMessageTypeToBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Models.SwarmMessageType type)
        {
            return type switch
            {
                Models.SwarmMessageType.UserBroadcast => (SolidColorBrush)new BrushConverter().ConvertFromString("#0F1E36")!, // Dark royal navy
                Models.SwarmMessageType.AgentAction => (SolidColorBrush)new BrushConverter().ConvertFromString("#062E25")!,   // Dark forest emerald
                Models.SwarmMessageType.SystemEvent => (SolidColorBrush)new BrushConverter().ConvertFromString("#131927")!,   // Sleek dark slate
                Models.SwarmMessageType.Handoff => (SolidColorBrush)new BrushConverter().ConvertFromString("#2E1065")!,       // Deep regal purple
                _ => (SolidColorBrush)new BrushConverter().ConvertFromString("#111827")!                                      // Dark background
            };
        }
        return (SolidColorBrush)new BrushConverter().ConvertFromString("#111827")!;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class SwarmMessageTypeToBorderConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Models.SwarmMessageType type)
        {
            return type switch
            {
                Models.SwarmMessageType.UserBroadcast => (SolidColorBrush)new BrushConverter().ConvertFromString("#2563EB")!, // Electric Blue
                Models.SwarmMessageType.AgentAction => (SolidColorBrush)new BrushConverter().ConvertFromString("#059669")!,   // Emerald
                Models.SwarmMessageType.SystemEvent => (SolidColorBrush)new BrushConverter().ConvertFromString("#4F46E5")!,   // Indigo
                Models.SwarmMessageType.Handoff => (SolidColorBrush)new BrushConverter().ConvertFromString("#9333EA")!,       // Violet
                _ => (SolidColorBrush)new BrushConverter().ConvertFromString("#1F2937")!
            };
        }
        return (SolidColorBrush)new BrushConverter().ConvertFromString("#1F2937")!;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class TabToActiveForegroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool matches = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
        return matches 
            ? (SolidColorBrush)new BrushConverter().ConvertFromString("#38BDF8")! 
            : (SolidColorBrush)new BrushConverter().ConvertFromString("#94A3B8")!;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class TabToActiveBorderConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool matches = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
        return matches 
            ? (SolidColorBrush)new BrushConverter().ConvertFromString("#3B82F6")! 
            : System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}




