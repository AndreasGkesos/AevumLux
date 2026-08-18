using AevumLux.Core.Repositories;
using AevumLux.Core.Repositories.Implementations;
using AevumLux.Core.Repositories.Interfaces;
using AevumLux.Core.Security;
using AevumLux.Core.Services.Implementations;
using AevumLux.Core.Services.Interfaces;
using AevumLux.Services;
using AevumLux.ViewModels;
using AevumLux.Views.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

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

        var providerRepository = Services.GetRequiredService<IProviderRepository>();
        _ = providerRepository.SeedIfMissingAsync(ScenarioProviderSeeds.GetAll());

        var window = new ShellWindow();
        window.Closed += (_, _) => (Services as IDisposable)?.Dispose();
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

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AevumLux");
        var dbPath = Path.Combine(appDataDir, "aevumlux.db");

        services.AddSingleton(_ => new LiteDbContext(dbPath));
        services.AddSingleton<ICryptoService, DpapiCryptoService>();
        services.AddSingleton<IAppSettingsService>(_ => new AppSettingsService(appDataDir));
        services.AddHttpClient<IDiscoveryService, DiscoveryService>();
        services.AddHttpClient<ITokenValidationService, TokenValidationService>();
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddSingleton<IProviderRepository, ProviderRepository>();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<ISessionHistoryService, SessionHistoryService>();
        services.AddTransient<IJwtService, JwtService>();
        services.AddSingleton<IAuthorizationRedirectHandler>(_ =>
            new WebView2AuthorizationRedirectHandler(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()));
        services.AddHttpClient<IFlowSimulatorService, FlowSimulatorService>();
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
        services.AddTransient<ProviderManagerViewModel>();
        services.AddTransient<SessionHistoryViewModel>();
        services.AddTransient<SettingsViewModel>();
    }
}
