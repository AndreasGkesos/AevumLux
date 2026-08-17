using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace AevumLux.Converters;

/// <summary>Converts a bool to <see cref="Visibility.Visible"/> (true) or <see cref="Visibility.Collapsed"/> (false).</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility.Visible;
}

/// <summary>Converts a bool to <see cref="Visibility.Collapsed"/> (true) or <see cref="Visibility.Visible"/> (false).</summary>
public sealed class BoolToInverseVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility.Collapsed;
}

/// <summary>Negates a boolean value.</summary>
public sealed class BoolNegationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is bool b && !b;
}

/// <summary>Returns true if the string is non-null and non-empty, false otherwise.</summary>
public sealed class StringToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        !string.IsNullOrEmpty(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>
/// Returns the accent foreground brush when true, or the default body text brush when false.
/// Used to highlight key endpoints in the discovery document.
/// </summary>
public sealed class BoolToAccentBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is true)
        {
            if (Application.Current.Resources.TryGetValue("SystemAccentColor", out var accentColor))
                return new SolidColorBrush((Windows.UI.Color)accentColor);
        }

        if (Application.Current.Resources.TryGetValue("TextFillColorPrimaryBrush", out var defaultBrush))
            return defaultBrush;

        return new SolidColorBrush(Colors.White);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>
/// Returns the accent foreground brush when true, or a transparent brush when false.
/// Used to highlight a matching card without drawing a visible border on non-matches.
/// </summary>
public sealed class MatchBorderBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is true && Application.Current.Resources.TryGetValue("SystemAccentColor", out var accentColor))
            return new SolidColorBrush((Windows.UI.Color)accentColor);

        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
