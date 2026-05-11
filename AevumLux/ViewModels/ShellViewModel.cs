using CommunityToolkit.Mvvm.ComponentModel;

namespace AevumLux.ViewModels;

/// <summary>ViewModel for the shell window. Holds app-level state such as the current page title.</summary>
public sealed partial class ShellViewModel : ObservableObject
{
    [ObservableProperty]
    private string _currentPageTitle = "Discovery Explorer";
}
