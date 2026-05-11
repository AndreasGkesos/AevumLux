namespace AevumLux.Core.Helpers;

/// <summary>Provides guard clause helpers for early input validation across the Core library.</summary>
public static class Guard
{
    /// <summary>Throws <see cref="ArgumentNullException"/> if <paramref name="value"/> is null.</summary>
    public static T AgainstNull<T>(T? value, string paramName) where T : class
    {
        if (value is null)
            throw new ArgumentNullException(paramName);
        return value;
    }

    /// <summary>Throws <see cref="ArgumentException"/> if <paramref name="value"/> is null or whitespace.</summary>
    public static string AgainstNullOrWhiteSpace(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value must not be null or whitespace.", paramName);
        return value;
    }

    /// <summary>Throws <see cref="ArgumentException"/> if <paramref name="value"/> is not a well-formed absolute URI.</summary>
    public static string AgainstInvalidUri(string? value, string paramName)
    {
        AgainstNullOrWhiteSpace(value, paramName);
        if (!Uri.TryCreate(value, UriKind.Absolute, out _))
            throw new ArgumentException($"'{value}' is not a valid absolute URI.", paramName);
        return value!;
    }
}
