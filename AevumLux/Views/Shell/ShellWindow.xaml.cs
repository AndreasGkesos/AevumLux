using AevumLux.Core.Services.Interfaces;
using AevumLux.Views.Discovery;
using AevumLux.Views.JwtDecoder;
using AevumLux.Views.TokenValidator;
using AevumLux.Views.FlowSimulator;
using AevumLux.Views.FlowExplanations;
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
        ["FlowExplanations"] = typeof(FlowExplanationsPage),
        ["ClaimsInspector"] = typeof(ClaimsInspectorPage),
        ["JwksExplorer"] = typeof(JwksExplorerPage),
        ["ScopeAnalyser"] = typeof(ScopeAnalyserPage),
        ["TokenDiff"] = typeof(TokenDiffPage),
        ["ProviderManager"] = typeof(ProviderManagerPage),
        ["SessionHistory"] = typeof(SessionHistoryPage),
        ["Settings"] = typeof(SettingsPage)
    };

    private readonly IAppSettingsService _appSettings = App.Services.GetRequiredService<IAppSettingsService>();
    private NavigationViewItem? _flowExplanationsItem;

    public ShellWindow()
    {
        InitializeComponent();
        TrySetMicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetWindowIcon();
        _appSettings.ShowFlowExplanationsChanged += (_, show) => RefreshFlowExplanationsVisibility(show);
    }

    private void SetWindowIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshFlowExplanationsVisibility(_appSettings.ShowFlowExplanations);
        NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().First();
        NavigateTo("Discovery");
    }

    /// <summary>Adds or removes the Flow Explanations nav item to match the current setting.</summary>
    private void RefreshFlowExplanationsVisibility(bool show)
    {
        if (show)
        {
            if (_flowExplanationsItem is null)
            {
                _flowExplanationsItem = new NavigationViewItem
                {
                    Tag = "FlowExplanations",
                    Content = "Flow Explanations",
                    Icon = new FontIcon { Glyph = "\uE82D" }
                };
                ToolTipService.SetToolTip(_flowExplanationsItem, "Reference: what each OAuth 2.0 / OIDC flow is and how it works");
                NavView.MenuItems.Add(_flowExplanationsItem);
            }
        }
        else if (_flowExplanationsItem is not null)
        {
            NavView.MenuItems.Remove(_flowExplanationsItem);
            _flowExplanationsItem = null;
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
