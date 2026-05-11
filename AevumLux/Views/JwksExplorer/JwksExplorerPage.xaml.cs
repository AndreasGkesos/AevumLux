using AevumLux.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AevumLux.Views.JwksExplorer;

/// <summary>Code-behind for the JWKS Explorer page. Contains no logic.</summary>
public sealed partial class JwksExplorerPage : Page
{
    public JwksExplorerViewModel ViewModel { get; }

    public JwksExplorerPage()
    {
        ViewModel = App.Services.GetRequiredService<JwksExplorerViewModel>();
        InitializeComponent();
    }
}
