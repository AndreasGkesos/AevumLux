using Microsoft.UI.Xaml.Controls;

namespace AevumLux.Views.FlowExplanations;

/// <summary>
/// Static reference page documenting the six OAuth 2.0 / OIDC flows Flow Simulator can run.
/// No live functionality, no ViewModel — the content never changes at runtime.
/// </summary>
public sealed partial class FlowExplanationsPage : Page
{
    public FlowExplanationsPage()
    {
        InitializeComponent();
    }
}
