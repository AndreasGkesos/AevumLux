using System.Text.Json;
using AevumLux.Core.Services.Interfaces;

namespace AevumLux.Core.Services.Implementations;

/// <summary>
/// Persists app settings as a small JSON file under %LOCALAPPDATA%\AevumLux.
///
/// ApplicationData.Current (the usual WinUI settings API) requires MSIX package
/// identity and throws for unpackaged apps, which this app is (WindowsPackageType=None).
/// A plain JSON file next to the LiteDB database avoids that requirement entirely.
/// </summary>
public sealed class AppSettingsService : IAppSettingsService
{
    private readonly string _settingsPath;
    private readonly object _lock = new();
    private SettingsData _data;

    public AppSettingsService(string settingsDirectory)
    {
        Directory.CreateDirectory(settingsDirectory);
        _settingsPath = Path.Combine(settingsDirectory, "settings.json");
        _data = Load();
    }

    /// <inheritdoc/>
    public bool ShowFlowExplanations
    {
        get { lock (_lock) return _data.ShowFlowExplanations; }
        set
        {
            lock (_lock)
            {
                if (_data.ShowFlowExplanations == value)
                    return;

                _data.ShowFlowExplanations = value;
                Save();
            }

            ShowFlowExplanationsChanged?.Invoke(this, value);
        }
    }

    /// <inheritdoc/>
    public event EventHandler<bool>? ShowFlowExplanationsChanged;

    private SettingsData Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new SettingsData();

            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
        }
        catch
        {
            return new SettingsData();
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_data);
        File.WriteAllText(_settingsPath, json);
    }

    private sealed class SettingsData
    {
        public bool ShowFlowExplanations { get; set; }
    }
}
