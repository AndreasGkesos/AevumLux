using AevumLux.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AevumLux.ViewModels;

/// <summary>ViewModel for the Settings page.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsService _appSettings;

    [ObservableProperty]
    private bool _showFlowSimulator;

    public SettingsViewModel(IAppSettingsService appSettings)
    {
        _appSettings = appSettings;
        _showFlowSimulator = appSettings.ShowFlowSimulator;
    }

    partial void OnShowFlowSimulatorChanged(bool value) => _appSettings.ShowFlowSimulator = value;
}
