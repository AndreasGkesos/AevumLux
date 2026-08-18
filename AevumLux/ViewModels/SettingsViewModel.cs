using System.Collections.ObjectModel;
using AevumLux.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;

namespace AevumLux.ViewModels;

/// <summary>ViewModel for the Settings page.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private const int MaxLogLines = 500;

    private readonly IAppSettingsService _appSettings;
    private readonly ITestIdpProcessService _testIdp;
    private readonly DispatcherQueue _dispatcherQueue;

    [ObservableProperty]
    private bool _showFlowExplanations;

    [ObservableProperty]
    private TestIdpStatus _testIdpStatus;

    public ObservableCollection<string> TestIdpLog { get; } = [];

    public string TestIdpExecutablePath => _testIdp.ExecutablePath;

    public string TestIdpLocalUrl => _testIdp.LocalUrl;

    public SettingsViewModel(IAppSettingsService appSettings, ITestIdpProcessService testIdp)
    {
        _appSettings = appSettings;
        _testIdp = testIdp;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _showFlowExplanations = appSettings.ShowFlowExplanations;
        _testIdpStatus = testIdp.Status;

        _testIdp.StatusChanged += HandleTestIdpStatusChanged;
        _testIdp.LogLineReceived += HandleTestIdpLogLineReceived;
    }

    partial void OnShowFlowExplanationsChanged(bool value) => _appSettings.ShowFlowExplanations = value;

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
