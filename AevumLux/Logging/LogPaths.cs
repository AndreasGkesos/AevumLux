namespace AevumLux.Logging;

/// <summary>Resolves the app's log folder location. Single source of truth for the path.</summary>
public static class LogPaths
{
    public static string LogFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AevumLux",
        "Logs");

    public static string CurrentLogFile => Path.Combine(LogFolder, $"aevumlux-{DateTime.Now:yyyyMMdd}.log");
}
