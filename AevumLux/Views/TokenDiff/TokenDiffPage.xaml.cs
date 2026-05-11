using AevumLux.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AevumLux.Views.TokenDiff;

/// <summary>Code-behind for the Token Diff page. Contains no logic.</summary>
public sealed partial class TokenDiffPage : Page
{
    public TokenDiffViewModel ViewModel { get; }

    public TokenDiffPage()
    {
        ViewModel = App.Services.GetRequiredService<TokenDiffViewModel>();
        InitializeComponent();
    }
}
