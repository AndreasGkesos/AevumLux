using AevumLux.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AevumLux.Views.JwtDecoder;

/// <summary>Code-behind for the JWT Decoder page. Contains no logic.</summary>
public sealed partial class JwtDecoderPage : Page
{
    public JwtDecoderViewModel ViewModel { get; }

    public JwtDecoderPage()
    {
        ViewModel = App.Services.GetRequiredService<JwtDecoderViewModel>();
        InitializeComponent();
    }
}
