using AevumLux.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AevumLux.Views.TokenExpiryMonitor;

/// <summary>Code-behind for the Expiry Monitor page. Contains no logic.</summary>
public sealed partial class TokenExpiryMonitorPage : Page
{
    public TokenExpiryMonitorViewModel ViewModel { get; }

    public TokenExpiryMonitorPage()
    {
        ViewModel = App.Services.GetRequiredService<TokenExpiryMonitorViewModel>();
        InitializeComponent();
    }
}
