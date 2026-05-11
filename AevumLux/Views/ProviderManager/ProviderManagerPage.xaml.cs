using AevumLux.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AevumLux.Views.ProviderManager;

/// <summary>Code-behind for the Provider Manager page. Contains no logic.</summary>
public sealed partial class ProviderManagerPage : Page
{
    public ProviderManagerViewModel ViewModel { get; }

    public ProviderManagerPage()
    {
        ViewModel = App.Services.GetRequiredService<ProviderManagerViewModel>();
        InitializeComponent();
    }
}
