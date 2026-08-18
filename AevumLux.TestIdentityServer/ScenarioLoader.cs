using System.Text.Json;

namespace AevumLux.TestIdentityServer;

/// <summary>
/// Loads every scenario JSON file under Scenarios/ — all of them, not just one selected by
/// name — since every scenario's clients are registered together at startup so the server can
/// stay running while AevumLux's own scenario picker selects what to test.
/// </summary>
public static class ScenarioLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IReadOnlyList<ScenarioOptions> LoadAll(string scenariosDirectory)
    {
        if (!Directory.Exists(scenariosDirectory))
            throw new DirectoryNotFoundException($"Scenarios directory not found: {scenariosDirectory}");

        var scenarios = new List<ScenarioOptions>();
        foreach (var path in Directory.EnumerateFiles(scenariosDirectory, "*.json").OrderBy(p => p))
        {
            var json = File.ReadAllText(path);
            var scenario = JsonSerializer.Deserialize<ScenarioOptions>(json, JsonOptions)
                ?? throw new InvalidOperationException($"Scenario file '{path}' is empty or invalid.");
            scenarios.Add(scenario);
        }

        if (scenarios.Count == 0)
            throw new InvalidOperationException($"No scenario files found in {scenariosDirectory}.");

        return scenarios;
    }
}
