using AevumLux.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AevumLux.Views.FlowSimulator;

/// <summary>Code-behind for the Flow Simulator page. Contains no logic.</summary>
public sealed partial class FlowSimulatorPage : Page
{
    public FlowSimulatorViewModel ViewModel { get; }

    public FlowSimulatorPage()
    {
        ViewModel = App.Services.GetRequiredService<FlowSimulatorViewModel>();
        InitializeComponent();
    }
}
