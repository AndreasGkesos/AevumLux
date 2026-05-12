using AevumLux.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace AevumLux.Views.Discovery;

/// <summary>
/// Code-behind for the Discovery Explorer page.
/// Contains only UI-specific interactions that cannot be expressed in XAML bindings.
/// </summary>
public sealed partial class DiscoveryPage : Page
{
    public DiscoveryViewModel ViewModel { get; }

    public DiscoveryPage()
    {
        ViewModel = App.Services.GetRequiredService<DiscoveryViewModel>();
        InitializeComponent();
    }

    private void CopyRawJson_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ViewModel.RawJson)) return;

        var data = new DataPackage();
        data.SetText(ViewModel.RawJson);
        Clipboard.SetContent(data);
    }
}
