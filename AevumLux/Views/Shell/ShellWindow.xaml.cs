using AevumLux.Core.Services.Interfaces;
using AevumLux.Views.Discovery;
using AevumLux.Views.JwtDecoder;
using AevumLux.Views.TokenValidator;
using AevumLux.Views.FlowSimulator;
using AevumLux.Views.ClaimsInspector;
using AevumLux.Views.JwksExplorer;
using AevumLux.Views.ScopeAnalyser;
using AevumLux.Views.TokenDiff;
using AevumLux.Views.ProviderManager;
using AevumLux.Views.SessionHistory;
using AevumLux.Views.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AevumLux.Views.Shell;

/// <summary>
/// The root window containing the NavigationView shell.
/// Code-behind is strictly limited to navigation routing and Mica backdrop setup.
/// </summary>
public sealed partial class ShellWindow : Window
{
    private readonly Dictionary<string, Type> _pageMap = new()
    {
        ["Discovery"] = typeof(DiscoveryPage),
        ["JwtDecoder"] = typeof(JwtDecoderPage),
        ["TokenValidator"] = typeof(TokenValidatorPage),
        ["FlowSimulator"] = typeof(FlowSimulatorPage),
        ["ClaimsInspector"] = typeof(ClaimsInspectorPage),
        ["JwksExplorer"] = typeof(JwksExplorerPage),
        ["ScopeAnalyser"] = typeof(ScopeAnalyserPage),
        ["TokenDiff"] = typeof(TokenDiffPage),
        ["ProviderManager"] = typeof(ProviderManagerPage),
        ["SessionHistory"] = typeof(SessionHistoryPage),
        ["Settings"] = typeof(SettingsPage)
    };

    private readonly IAppSettingsService _appSettings = App.Services.GetRequiredService<IAppSettingsService>();
    private NavigationViewItem? _flowSimulatorItem;

    public ShellWindow()
    {
        InitializeComponent();
        TrySetMicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        _appSettings.ShowFlowSimulatorChanged += (_, show) => RefreshFlowSimulatorVisibility(show);
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshFlowSimulatorVisibility(_appSettings.ShowFlowSimulator);
        NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().First();
        NavigateTo("Discovery");
    }

    /// <summary>Adds or removes the Flow Simulator nav item to match the current setting.</summary>
    private void RefreshFlowSimulatorVisibility(bool show)
    {
        if (show)
        {
            if (_flowSimulatorItem is null)
            {
                _flowSimulatorItem = new NavigationViewItem
                {
                    Tag = "FlowSimulator",
                    Content = "Flow Simulator",
                    Icon = new FontIcon { Glyph = "\uE768" }
                };
                ToolTipService.SetToolTip(_flowSimulatorItem, "Simulate OIDC and OAuth 2.0 flows step by step");
                NavView.MenuItems.Add(_flowSimulatorItem);
            }
        }
        else if (_flowSimulatorItem is not null)
        {
            NavView.MenuItems.Remove(_flowSimulatorItem);
            _flowSimulatorItem = null;
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            NavigateTo(tag);
    }

    private void NavigateTo(string tag)
    {
        if (_pageMap.TryGetValue(tag, out var pageType))
            ContentFrame.Navigate(pageType);
    }

    private void TrySetMicaBackdrop()
    {
        if (MicaController.IsSupported())
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
    }
}
