using AevumLux.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AevumLux.Views.ClaimsInspector;

/// <summary>Code-behind for the Claims Inspector page. Contains no logic.</summary>
public sealed partial class ClaimsInspectorPage : Page
{
    public ClaimsInspectorViewModel ViewModel { get; }

    public ClaimsInspectorPage()
    {
        ViewModel = App.Services.GetRequiredService<ClaimsInspectorViewModel>();
        InitializeComponent();
    }
}
