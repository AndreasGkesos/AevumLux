namespace AevumLux.Logging;

/// <summary>Resolves the shared log folder/file paths used by both the crash handler and Settings.</summary>
internal static class LogPaths
{
    public static string LogFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AevumLux",
        "Logs");

    public static string CurrentLogFile => Path.Combine(LogFolder, $"aevumlux-{DateTime.Now:yyyyMMdd}.log");
}
