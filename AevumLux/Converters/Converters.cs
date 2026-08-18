using AevumLux.Core.Models;
using AevumLux.Core.Services.Interfaces;
using AevumLux.ViewModels;
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

/// <summary>Converts a <see cref="SimulatedFlowType"/> enum value to its zero-based ComboBox index and back.</summary>
public sealed class EnumToIndexConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is SimulatedFlowType flow ? (int)flow : 0;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is int index ? (SimulatedFlowType)index : SimulatedFlowType.ClientCredentials;
}

/// <summary>
/// Returns Visible when the bound <see cref="SimulatedFlowType"/> matches the string
/// ConverterParameter (the enum member name), Collapsed otherwise.
/// </summary>
public sealed class FlowTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is SimulatedFlowType flow && parameter is string target && flow.ToString() == target
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Returns Visible when the bound <see cref="SimulatedFlowType"/> uses a browser redirect (Authorization Code + PKCE or Implicit), Collapsed otherwise.</summary>
public sealed class FlowUsesRedirectUriToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is SimulatedFlowType.AuthorizationCodePkce or SimulatedFlowType.Implicit
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Returns Visible when the bound <see cref="FlowStepStatus"/> is Success, Collapsed otherwise.</summary>
public sealed class FlowStepStatusToSuccessVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is FlowStepStatus.Success ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Returns Visible when the bound <see cref="FlowStepStatus"/> is Failed, Collapsed otherwise.</summary>
public sealed class FlowStepStatusToFailedVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is FlowStepStatus.Failed ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Displays an <see cref="OidcProvider"/>'s Name, or a placeholder for the null "type your own" entry.</summary>
public sealed class ProviderToNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is OidcProvider provider ? provider.Name : "(Type your own — no scenario provider)";

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Converts a <see cref="TestIdpStatus"/> to a colored dot brush: grey (not found), amber (transitioning), green (running), red (failed).</summary>
public sealed class TestIdpStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value switch
        {
            TestIdpStatus.Running => new SolidColorBrush(Colors.LimeGreen),
            TestIdpStatus.Starting or TestIdpStatus.Stopping or TestIdpStatus.Publishing => new SolidColorBrush(Colors.Orange),
            TestIdpStatus.Failed or TestIdpStatus.PublishFailed => new SolidColorBrush(Colors.Red),
            _ => new SolidColorBrush(Colors.Gray),
        };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Converts a <see cref="TestIdpStatus"/> to a short human-readable label.</summary>
public sealed class TestIdpStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value switch
        {
            TestIdpStatus.NotFound => "Not found — not yet published",
            TestIdpStatus.Publishing => "Publishing (first run)…",
            TestIdpStatus.PublishFailed => "Publish failed",
            TestIdpStatus.Stopped => "Stopped",
            TestIdpStatus.Starting => "Starting…",
            TestIdpStatus.Running => "Running",
            TestIdpStatus.Stopping => "Stopping…",
            TestIdpStatus.Failed => "Stopped unexpectedly",
            _ => "Unknown",
        };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Returns Visible when the bound <see cref="TestIdpStatus"/> is Running, Collapsed otherwise.</summary>
public sealed class TestIdpRunningToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is TestIdpStatus.Running ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Returns Visible when the bound value is non-null (and, for strings, non-empty), Collapsed otherwise.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value switch
        {
            null => Visibility.Collapsed,
            string s => string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible,
            _ => Visibility.Visible,
        };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
