using System.Collections.ObjectModel;
using System.Diagnostics;
using AevumLux.Core.Services.Interfaces;
using AevumLux.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Serilog.Events;

namespace AevumLux.ViewModels;

/// <summary>ViewModel for the Settings page.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private const int MaxLogLines = 500;

    private readonly IAppSettingsService _appSettings;
    private readonly ITestIdpProcessService _testIdp;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly bool _initialized;

    [ObservableProperty]
    private bool _showFlowExplanations;

    [ObservableProperty]
    private TestIdpStatus _testIdpStatus;

    public ObservableCollection<string> TestIdpLog { get; } = [];

    public string TestIdpExecutablePath => _testIdp.ExecutablePath;

    public string TestIdpLocalUrl => _testIdp.LocalUrl;

    public string LogFolderPath => LogPaths.LogFolder;

    public ObservableCollection<string> LogLevels { get; } = ["Debug", "Information", "Warning"];

    [ObservableProperty]
    private string _selectedLogLevel;

    public SettingsViewModel(IAppSettingsService appSettings, ITestIdpProcessService testIdp, ILogger<SettingsViewModel> logger)
    {
        _appSettings = appSettings;
        _testIdp = testIdp;
        _logger = logger;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _showFlowExplanations = appSettings.ShowFlowExplanations;
        _testIdpStatus = testIdp.Status;
        _selectedLogLevel = ToDisplayName(App.LevelSwitch.MinimumLevel);

        _testIdp.StatusChanged += HandleTestIdpStatusChanged;
        _testIdp.LogLineReceived += HandleTestIdpLogLineReceived;

        _initialized = true;
    }

    partial void OnSelectedLogLevelChanged(string value)
    {
        // Logged before applying the new level — a switch to Warning or above would otherwise
        // filter out its own confirmation message the instant the level is raised.
        if (_initialized)
            _logger.LogInformation("Setting changed. Setting={Setting} NewValue={NewValue}", nameof(SelectedLogLevel), value);
        App.LevelSwitch.MinimumLevel = ToLogEventLevel(value);
    }

    private static string ToDisplayName(LogEventLevel level) => level switch
    {
        LogEventLevel.Debug => "Debug",
        LogEventLevel.Warning => "Warning",
        _ => "Information",
    };

    private static LogEventLevel ToLogEventLevel(string displayName) => displayName switch
    {
        "Debug" => LogEventLevel.Debug,
        "Warning" => LogEventLevel.Warning,
        _ => LogEventLevel.Information,
    };

    partial void OnShowFlowExplanationsChanged(bool value)
    {
        _appSettings.ShowFlowExplanations = value;
        if (_initialized)
            _logger.LogInformation("Setting changed. Setting={Setting} NewValue={NewValue}", nameof(ShowFlowExplanations), value);
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        Directory.CreateDirectory(LogPaths.LogFolder);
        Process.Start("explorer.exe", LogPaths.LogFolder);
    }

    [RelayCommand(CanExecute = nameof(CanStartTestIdp))]
    private Task StartTestIdp() => _testIdp.StartAsync();

    private bool CanStartTestIdp() => TestIdpStatus is TestIdpStatus.Stopped or TestIdpStatus.Failed;

    [RelayCommand(CanExecute = nameof(CanStopTestIdp))]
    private void StopTestIdp() => _testIdp.Stop();

    private bool CanStopTestIdp() => TestIdpStatus is TestIdpStatus.Running or TestIdpStatus.Starting;

    partial void OnTestIdpStatusChanged(TestIdpStatus value)
    {
        StartTestIdpCommand.NotifyCanExecuteChanged();
        StopTestIdpCommand.NotifyCanExecuteChanged();
    }

    private void HandleTestIdpStatusChanged(object? sender, TestIdpStatus status) =>
        _dispatcherQueue.TryEnqueue(() => TestIdpStatus = status);

    private void HandleTestIdpLogLineReceived(object? sender, string line) =>
        _dispatcherQueue.TryEnqueue(() =>
        {
            TestIdpLog.Add(line);
            while (TestIdpLog.Count > MaxLogLines)
                TestIdpLog.RemoveAt(0);
        });
}
