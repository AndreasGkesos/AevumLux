namespace AevumLux.Core.Security;

/// <summary>Provides guard clause helpers for early input validation.</summary>
internal static class Guard
{
    /// <summary>Throws <see cref="ArgumentException"/> if <paramref name="value"/> is null or whitespace.</summary>
    public static void AgainstNullOrWhiteSpace(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value must not be null or whitespace.", paramName);
    }
}
