using System.Text.Json;

namespace AevumLux.TestIdentityServer;

/// <summary>Loads a <see cref="ScenarioOptions"/> from Scenarios/{name}.json.</summary>
public static class ScenarioLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static ScenarioOptions Load(string scenariosDirectory, string scenarioName)
    {
        var path = Path.Combine(scenariosDirectory, $"{scenarioName}.json");

        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"No scenario file found for '{scenarioName}'. Expected: {path}");

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ScenarioOptions>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Scenario file '{path}' is empty or invalid.");
    }
}
