using AevumLux.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AevumLux.ViewModels;

/// <summary>ViewModel for the Settings page.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsService _appSettings;

    [ObservableProperty]
    private bool _showFlowExplanations;

    public SettingsViewModel(IAppSettingsService appSettings)
    {
        _appSettings = appSettings;
        _showFlowExplanations = appSettings.ShowFlowExplanations;
    }

    partial void OnShowFlowExplanationsChanged(bool value) => _appSettings.ShowFlowExplanations = value;
}
