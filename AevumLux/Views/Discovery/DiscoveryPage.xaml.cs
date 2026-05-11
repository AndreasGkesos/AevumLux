using AevumLux.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AevumLux.Views.Discovery;

/// <summary>Code-behind for the Discovery Explorer page. Contains no logic.</summary>
public sealed partial class DiscoveryPage : Page
{
    public DiscoveryViewModel ViewModel { get; }

    public DiscoveryPage()
    {
        ViewModel = App.Services.GetRequiredService<DiscoveryViewModel>();
        InitializeComponent();
    }
}
