using AevumLux.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AevumLux.Views.SessionHistory;

/// <summary>Code-behind for the Session History page. Contains no logic.</summary>
public sealed partial class SessionHistoryPage : Page
{
    public SessionHistoryViewModel ViewModel { get; }

    public SessionHistoryPage()
    {
        ViewModel = App.Services.GetRequiredService<SessionHistoryViewModel>();
        InitializeComponent();
    }
}
