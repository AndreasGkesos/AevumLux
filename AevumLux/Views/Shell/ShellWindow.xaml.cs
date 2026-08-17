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

    public ShellWindow()
    {
        InitializeComponent();
        TrySetMicaBackdrop();
        ExtendsContentIntoTitleBar = true;
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().First();
        NavigateTo("Discovery");
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
