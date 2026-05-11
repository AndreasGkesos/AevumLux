using AevumLux.Core.Repositories;
using AevumLux.Core.Repositories.Implementations;
using AevumLux.Core.Repositories.Interfaces;
using AevumLux.Core.Security;
using AevumLux.Core.Services.Implementations;
using AevumLux.Core.Services.Interfaces;
using AevumLux.ViewModels;
using AevumLux.Views.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using System.IO;

namespace AevumLux;

/// <summary>
/// Application entry point. Bootstraps the DI container and launches the shell window.
/// No logic lives here beyond service registration — all behaviour is in services and ViewModels.
/// </summary>
public partial class App : Application
{
    /// <summary>The application-wide service provider. Available after <see cref="OnLaunched"/>.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Services = BuildServiceProvider();

        var window = new ShellWindow();
        window.Activate();
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        RegisterInfrastructure(services);
        RegisterRepositories(services);
        RegisterServices(services);
        RegisterViewModels(services);

        return services.BuildServiceProvider();
    }

    private static void RegisterInfrastructure(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AevumLux",
            "aevumlux.db");

        services.AddSingleton(_ => new LiteDbContext(dbPath));
        services.AddSingleton<ICryptoService, DpapiCryptoService>();
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddSingleton<IProviderRepository, ProviderRepository>();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        // Service implementations are registered as each phase is built.
        services.AddSingleton<ISessionHistoryService, SessionHistoryService>();
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddTransient<ShellViewModel>();
        services.AddTransient<DiscoveryViewModel>();
        services.AddTransient<JwtDecoderViewModel>();
        services.AddTransient<TokenValidatorViewModel>();
        services.AddTransient<FlowSimulatorViewModel>();
        services.AddTransient<ClaimsInspectorViewModel>();
        services.AddTransient<JwksExplorerViewModel>();
        services.AddTransient<ScopeAnalyserViewModel>();
        services.AddTransient<TokenDiffViewModel>();
        services.AddTransient<TokenExpiryMonitorViewModel>();
        services.AddTransient<ProviderManagerViewModel>();
        services.AddTransient<SessionHistoryViewModel>();
        services.AddTransient<SettingsViewModel>();
    }
}
