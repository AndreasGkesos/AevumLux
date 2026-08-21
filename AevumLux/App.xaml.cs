using System.Diagnostics;
using AevumLux.Core.Repositories;
using AevumLux.Core.Repositories.Implementations;
using AevumLux.Core.Repositories.Interfaces;
using AevumLux.Core.Services.Implementations;
using AevumLux.Core.Services.Interfaces;
using AevumLux.Logging;
using AevumLux.Services;
using AevumLux.ViewModels;
using AevumLux.Views.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AevumLux;

/// <summary>
/// Application entry point. Bootstraps the DI container and launches the shell window.
/// No logic lives here beyond service registration — all behaviour is in services and ViewModels.
/// </summary>
public partial class App : Application
{
    /// <summary>The application-wide service provider. Available after <see cref="OnLaunched"/>.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    private ShellWindow? _shellWindow;

    public App()
    {
        InitializeComponent();

        UnhandledException += OnAppUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Services = BuildServiceProvider();

        var providerRepository = Services.GetRequiredService<IProviderRepository>();
        _ = providerRepository.SeedIfMissingAsync(ScenarioProviderSeeds.GetAll());

        var testIdpProcessService = Services.GetRequiredService<ITestIdpProcessService>();
        _ = testIdpProcessService.EnsurePublishedAsync();

        _shellWindow = new ShellWindow();
        _shellWindow.Closed += (_, _) => (Services as IDisposable)?.Dispose();
        _shellWindow.Activate();
    }

    private void OnAppUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        HandleUnhandledException(e.Exception);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        HandleUnhandledException(e.Exception);
    }

    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            HandleUnhandledException(ex);
    }

    /// <summary>
    /// Logs the exception to disk first, then attempts to show the crash dialog. Never allowed
    /// to throw itself — a failure here must not take down the process any harder than it
    /// already is.
    /// </summary>
    private void HandleUnhandledException(Exception exception)
    {
        try
        {
            WriteCrashLog(exception);
        }
        catch
        {
            // Logging must never be the reason the crash handler itself crashes.
        }

        try
        {
            if (_shellWindow?.Content?.XamlRoot is { } xamlRoot)
                _ = ShowCrashDialogAsync(xamlRoot);
        }
        catch
        {
            // No window/XamlRoot available yet (e.g. crash during startup) — logging above is
            // the best we can do.
        }
    }

    private static void WriteCrashLog(Exception exception)
    {
        Directory.CreateDirectory(LogPaths.LogFolder);
        var entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [FATAL] Unhandled exception{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}";
        File.AppendAllText(LogPaths.CurrentLogFile, entry);
    }

    private static async Task ShowCrashDialogAsync(XamlRoot xamlRoot)
    {
        var dialog = new ContentDialog
        {
            Title = "Something went wrong",
            Content = "AevumLux encountered an unexpected error. The details have been saved to the log file.",
            PrimaryButtonText = "Open Log Folder",
            CloseButtonText = "Close",
            XamlRoot = xamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            Directory.CreateDirectory(LogPaths.LogFolder);
            Process.Start("explorer.exe", LogPaths.LogFolder);
        }
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
        services.AddSingleton<ITestIdpProcessService, TestIdpProcessService>();
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
