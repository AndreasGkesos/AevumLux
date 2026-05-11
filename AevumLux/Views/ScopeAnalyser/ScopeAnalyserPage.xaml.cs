using AevumLux.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AevumLux.Views.ScopeAnalyser;

/// <summary>Code-behind for the Scope Analyser page. Contains no logic.</summary>
public sealed partial class ScopeAnalyserPage : Page
{
    public ScopeAnalyserViewModel ViewModel { get; }

    public ScopeAnalyserPage()
    {
        ViewModel = App.Services.GetRequiredService<ScopeAnalyserViewModel>();
        InitializeComponent();
    }
}
