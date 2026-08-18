namespace AevumLux.Core.Services.Interfaces;

/// <summary>Persists small, app-wide user preferences.</summary>
public interface IAppSettingsService
{
    /// <summary>
    /// Gets or sets whether the Flow Explanations reference page is shown in navigation, and
    /// whether Flow Simulator shows its scenario/provider picker and per-step teaching text
    /// (explanations, deprecation warnings). Off by default — Flow Simulator itself is always
    /// visible as a debugging tool; this setting only controls the "teaching" overhead layered
    /// on top of it for people learning OAuth/OIDC rather than actively debugging.
    /// </summary>
    bool ShowFlowExplanations { get; set; }

    /// <summary>Raised whenever <see cref="ShowFlowExplanations"/> changes, so the shell can update live.</summary>
    event EventHandler<bool>? ShowFlowExplanationsChanged;
}
