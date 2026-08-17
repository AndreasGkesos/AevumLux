namespace AevumLux.Core.Services.Interfaces;

/// <summary>Persists small, app-wide user preferences.</summary>
public interface IAppSettingsService
{
    /// <summary>
    /// Gets or sets whether the Flow Simulator page is shown in navigation.
    /// Off by default — this app is primarily a debugging tool; Flow Simulator
    /// is a study-mode feature for learning OAuth/OIDC flows step by step.
    /// </summary>
    bool ShowFlowSimulator { get; set; }

    /// <summary>Raised whenever <see cref="ShowFlowSimulator"/> changes, so the shell can update live.</summary>
    event EventHandler<bool>? ShowFlowSimulatorChanged;
}
