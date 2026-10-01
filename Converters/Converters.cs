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
        bool isEmpty = value == null || (value is string s && string.IsNullOrWhiteSpace(s));
        if (Invert) isEmpty = !isEmpty;
        return isEmpty ? Visibility.Collapsed : Visibility.Visible;
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
        if (value is int i && i > 0) return Visibility.Visible;
        if (value is long l && l > 0) return Visibility.Visible;
        return Visibility.Collapsed;
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


