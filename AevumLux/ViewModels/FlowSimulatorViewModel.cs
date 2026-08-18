using System.Collections.ObjectModel;
using System.Text.Json;
using AevumLux.Core.Models;
using AevumLux.Core.Repositories.Interfaces;
using AevumLux.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AevumLux.ViewModels;

/// <summary>Which OAuth 2.0 / OIDC flow the Flow Simulator page is currently set to run.</summary>
public enum SimulatedFlowType
{
    ClientCredentials,
    AuthorizationCodePkce,
    RefreshToken,
    DeviceCode,
    Implicit,
    ResourceOwnerPassword,
}

/// <summary>
/// ViewModel for the Flow Simulator page. Drives real, live HTTP calls (and, for Authorization
/// Code + PKCE, a real browser redirect) against whatever OIDC provider is selected — nothing
/// here is scripted or simulated in the "fake data" sense; "simulate" means "walk through the
/// flow step by step", not "fake the network calls".
/// </summary>
public sealed partial class FlowSimulatorViewModel : ObservableObject
{
    private readonly IFlowSimulatorService _flowSimulatorService;
    private readonly IDiscoveryService _discoveryService;
    private readonly IProviderRepository _providerRepository;
    private readonly IAppSettingsService _appSettings;

    /// <summary>
    /// Whether to show the scenario/provider picker and per-step teaching text (explanations,
    /// deprecation warnings) alongside the real request/response data. Mirrors
    /// <see cref="IAppSettingsService.ShowFlowExplanations"/> and updates live if changed on
    /// the Settings page while this page is open. The manual Issuer URL/Client ID/etc. fields
    /// and the request/response data are the actual debugging tool and are never hidden.
    /// </summary>
    [ObservableProperty]
    private bool _showExplanations;

    [ObservableProperty]
    private SimulatedFlowType _selectedFlow = SimulatedFlowType.ClientCredentials;

    /// <summary>
    /// Scenario providers only (seeded to pair with a AevumLux.TestIdentityServer scenario).
    /// Manual providers from Provider Manager are intentionally excluded here — selecting one
    /// is a convenience that autofills the fields below, never the only way to fill them in.
    /// The first entry is always the "type your own" placeholder.
    /// </summary>
    public ObservableCollection<OidcProvider?> Providers { get; } = [];

    [ObservableProperty]
    private OidcProvider? _selectedProvider;

    /// <summary>Issuer URL — autofilled when a scenario provider is picked, always editable.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    private string _issuerUrl = string.Empty;

    /// <summary>Client ID — autofilled when a scenario provider is picked, always editable.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    private string _clientId = string.Empty;

    /// <summary>Redirect URI — autofilled when a scenario provider is picked, always editable.</summary>
    [ObservableProperty]
    private string _redirectUri = "http://localhost:7890/callback";

    /// <summary>Space-delimited scopes — autofilled when a scenario provider is picked, always editable.</summary>
    [ObservableProperty]
    private string _scope = string.Empty;

    /// <summary>
    /// Client secret entered by the user for this run. Not persisted — scenario providers
    /// don't store secrets (see SCENARIOS.md for the values to type in), and real providers'
    /// stored secret is encrypted at rest and only ever decrypted for an actual request.
    /// </summary>
    [ObservableProperty]
    private string _clientSecret = string.Empty;

    /// <summary>Refresh token entered by the user, used only when SelectedFlow is RefreshToken.</summary>
    [ObservableProperty]
    private string _refreshTokenInput = string.Empty;

    /// <summary>Username entered by the user, used only when SelectedFlow is ResourceOwnerPassword.</summary>
    [ObservableProperty]
    private string _username = string.Empty;

    /// <summary>Password entered by the user, used only when SelectedFlow is ResourceOwnerPassword.
    /// Not persisted, same as ClientSecret — see SCENARIOS.md for the ropc- scenario's fixed test credentials.</summary>
    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _hasResult;

    public bool ShowEmptyState => !HasResult && !IsBusy && Steps.Count == 0;

    public ObservableCollection<FlowStep> Steps { get; } = [];

    /// <summary>The final access token from the last successful step, if any — feeds the
    /// "Decode this token" shortcut and the Refresh Token flow's input field.</summary>
    [ObservableProperty]
    private string _lastAccessToken = string.Empty;

    [ObservableProperty]
    private string _lastRefreshToken = string.Empty;

    [ObservableProperty]
    private bool _hasAccessToken;

    public FlowSimulatorViewModel(
        IFlowSimulatorService flowSimulatorService,
        IDiscoveryService discoveryService,
        IProviderRepository providerRepository,
        IAppSettingsService appSettings)
    {
        _flowSimulatorService = flowSimulatorService;
        _discoveryService = discoveryService;
        _providerRepository = providerRepository;
        _appSettings = appSettings;
        _showExplanations = appSettings.ShowFlowExplanations;
        _appSettings.ShowFlowExplanationsChanged += (_, show) => ShowExplanations = show;
        _ = LoadProvidersAsync();
    }

    private async Task LoadProvidersAsync()
    {
        var providers = await _providerRepository.GetAllAsync();
        Providers.Clear();
        Providers.Add(null); // "Type your own" — clears/leaves the fields as manual entry.
        foreach (var provider in providers.Where(p => p.Source == ProviderSource.Scenario))
            Providers.Add(provider);
    }

    partial void OnSelectedFlowChanged(SimulatedFlowType value)
    {
        RunCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedProviderChanged(OidcProvider? value)
    {
        if (value is not null)
        {
            IssuerUrl = value.IssuerUrl;
            ClientId = value.ClientId;
            RedirectUri = value.RedirectUri;
            Scope = value.Scopes;
        }

        RunCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(IssuerUrl) || string.IsNullOrWhiteSpace(ClientId))
            return;

        ErrorMessage = null;
        HasResult = false;
        HasAccessToken = false;
        LastAccessToken = string.Empty;
        LastRefreshToken = string.Empty;
        Steps.Clear();
        IsBusy = true;

        try
        {
            // Built fresh from the editable fields every run — whether they came from picking
            // a scenario provider (autofill) or were hand-typed against any other IdP, real or
            // local. Flow Simulator never depends on a saved OidcProvider to function.
            var runProvider = new OidcProvider
            {
                IssuerUrl = IssuerUrl.Trim(),
                ClientId = ClientId.Trim(),
                RedirectUri = RedirectUri.Trim(),
                Scopes = Scope.Trim(),
            };

            var discovery = await _discoveryService.FetchDiscoveryDocumentAsync(runProvider.IssuerUrl, cancellationToken);

            IAsyncEnumerable<FlowStep> stepStream = SelectedFlow switch
            {
                SimulatedFlowType.ClientCredentials =>
                    _flowSimulatorService.SimulateClientCredentialsAsync(runProvider, discovery, ClientSecret.Trim(), cancellationToken),
                SimulatedFlowType.AuthorizationCodePkce =>
                    _flowSimulatorService.SimulateAuthorizationCodePkceAsync(runProvider, discovery, cancellationToken),
                SimulatedFlowType.RefreshToken =>
                    _flowSimulatorService.SimulateRefreshTokenAsync(runProvider, discovery, RefreshTokenInput.Trim(), ClientSecret.Trim(), cancellationToken),
                SimulatedFlowType.DeviceCode =>
                    _flowSimulatorService.SimulateDeviceCodeAsync(runProvider, discovery, cancellationToken),
                SimulatedFlowType.Implicit =>
                    _flowSimulatorService.SimulateImplicitAsync(runProvider, discovery, cancellationToken),
                SimulatedFlowType.ResourceOwnerPassword =>
                    _flowSimulatorService.SimulateResourceOwnerPasswordAsync(runProvider, discovery, Username.Trim(), Password, ClientSecret.Trim(), cancellationToken),
                _ => throw new InvalidOperationException($"Unhandled flow type: {SelectedFlow}"),
            };

            await foreach (var step in stepStream)
            {
                var existingIndex = Steps.ToList().FindIndex(s => s.StepNumber == step.StepNumber);
                if (existingIndex >= 0)
                    Steps[existingIndex] = step;
                else
                    Steps.Add(step);

                HasResult = true;

                if (step.Status == FlowStepStatus.Success && step.Response?.Body is not null)
                    TryExtractTokens(step.Response.Body);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unexpected error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    private bool CanRun() =>
        !string.IsNullOrWhiteSpace(IssuerUrl) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !IsBusy &&
        SelectedFlow switch
        {
            SimulatedFlowType.ClientCredentials => true,
            SimulatedFlowType.AuthorizationCodePkce => true,
            SimulatedFlowType.RefreshToken => !string.IsNullOrWhiteSpace(RefreshTokenInput),
            SimulatedFlowType.DeviceCode => true,
            SimulatedFlowType.Implicit => true,
            SimulatedFlowType.ResourceOwnerPassword => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password),
            _ => false,
        };

    partial void OnIsBusyChanged(bool value)
    {
        RunCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    partial void OnRefreshTokenInputChanged(string value) => RunCommand.NotifyCanExecuteChanged();

    partial void OnUsernameChanged(string value) => RunCommand.NotifyCanExecuteChanged();

    partial void OnPasswordChanged(string value) => RunCommand.NotifyCanExecuteChanged();

    private void TryExtractTokens(string rawJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("access_token", out var token))
            {
                LastAccessToken = token.GetString() ?? string.Empty;
                HasAccessToken = !string.IsNullOrEmpty(LastAccessToken);
            }

            if (root.TryGetProperty("refresh_token", out var refresh))
                LastRefreshToken = refresh.GetString() ?? string.Empty;
        }
        catch (JsonException)
        {
            // Not JSON (e.g. an error body already handled elsewhere) — nothing to extract.
        }
    }
}
