using AevumLux.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AevumLux.Views.TokenValidator;

/// <summary>Code-behind for the Token Validator page. Contains no logic.</summary>
public sealed partial class TokenValidatorPage : Page
{
    public TokenValidatorViewModel ViewModel { get; }

    public TokenValidatorPage()
    {
        ViewModel = App.Services.GetRequiredService<TokenValidatorViewModel>();
        InitializeComponent();
    }
}
