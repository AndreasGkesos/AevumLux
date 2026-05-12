using System.Collections.ObjectModel;
using System.Text.Json;
using AevumLux.Core.Models;
using AevumLux.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AevumLux.ViewModels;

/// <summary>
/// ViewModel for the Discovery Explorer page.
/// Fetches OIDC discovery documents and exposes the parsed result for display.
/// </summary>
public sealed partial class DiscoveryViewModel : ObservableObject
{
    private readonly IDiscoveryService _discoveryService;
    private readonly ISessionHistoryService _sessionHistory;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FetchCommand))]
    private string _issuerUrl = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FetchCommand))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _hasResult;

    public bool ShowEmptyState => !HasResult && !IsBusy;

    [ObservableProperty]
    private string _rawJson = string.Empty;

    [ObservableProperty]
    private string _fetchedAt = string.Empty;

    public ObservableCollection<DiscoveryGroup> Groups { get; } = [];

    public DiscoveryViewModel(IDiscoveryService discoveryService, ISessionHistoryService sessionHistory)
    {
        _discoveryService = discoveryService;
        _sessionHistory = sessionHistory;
    }

    [RelayCommand(CanExecute = nameof(CanFetch))]
    private async Task FetchAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        HasResult = false;
        Groups.Clear();
        RawJson = string.Empty;
        IsBusy = true;

        try
        {
            var issuerUrl = IssuerUrl.Trim();
            var doc = await _discoveryService.FetchDiscoveryDocumentAsync(issuerUrl, cancellationToken);

            RawJson = PrettyPrint(doc.RawJson);
            FetchedAt = $"Fetched {doc.FetchedAt:HH:mm:ss} UTC";
            PopulateGroups(doc);
            HasResult = true;

            _sessionHistory.AddEntry(
                SessionEntryType.DiscoveryFetched,
                $"Discovery: {issuerUrl}",
                doc.RawJson);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Request cancelled.";
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"Could not reach the provider. Check the URL and your connection.\n\nDetail: {ex.Message}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unexpected error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanFetch() => !string.IsNullOrWhiteSpace(IssuerUrl) && !IsBusy;

    private void PopulateGroups(DiscoveryDocument doc)
    {
        Groups.Add(new DiscoveryGroup("Key Endpoints",
        [
            new("Authorization Endpoint", doc.AuthorizationEndpoint, isKey: true),
            new("Token Endpoint", doc.TokenEndpoint, isKey: true),
            new("UserInfo Endpoint", doc.UserinfoEndpoint, isKey: true),
            new("JWKS URI", doc.JwksUri, isKey: true),
            new("End Session Endpoint", doc.EndSessionEndpoint, isKey: true),
        ]));

        Groups.Add(new DiscoveryGroup("Additional Endpoints",
        [
            new("Device Authorization", doc.DeviceAuthorizationEndpoint),
            new("Introspection", doc.IntrospectionEndpoint),
            new("Revocation", doc.RevocationEndpoint),
            new("Registration", doc.RegistrationEndpoint),
        ]));

        Groups.Add(new DiscoveryGroup("Grant Types", ToEntries(doc.GrantTypesSupported)));
        Groups.Add(new DiscoveryGroup("Response Types", ToEntries(doc.ResponseTypesSupported)));
        Groups.Add(new DiscoveryGroup("Scopes Supported", ToEntries(doc.ScopesSupported)));
        Groups.Add(new DiscoveryGroup("Signing Algorithms", ToEntries(doc.IdTokenSigningAlgValuesSupported)));
        Groups.Add(new DiscoveryGroup("PKCE Methods", ToEntries(doc.CodeChallengeMethodsSupported)));
        Groups.Add(new DiscoveryGroup("Token Auth Methods", ToEntries(doc.TokenEndpointAuthMethodsSupported)));
    }

    private static List<DiscoveryEntry> ToEntries(List<string> values) =>
        values.Select(v => new DiscoveryEntry(string.Empty, v)).ToList();

    private static readonly JsonSerializerOptions _indentedOptions = new() { WriteIndented = true };

    private static string PrettyPrint(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, _indentedOptions);
        }
        catch
        {
            return json;
        }
    }
}

/// <summary>A named group of entries shown in the left panel of the Discovery Explorer.</summary>
public sealed class DiscoveryGroup(string title, List<DiscoveryEntry> entries)
{
    public string Title { get; } = title;
    public IReadOnlyList<DiscoveryEntry> Entries { get; } = entries;
    public bool HasEntries => Entries.Any(e => e.HasValue);
}

/// <summary>A single row within a discovery group.</summary>
public sealed class DiscoveryEntry(string label, string? value, bool isKey = false)
{
    public string Label { get; } = label;
    public string Value { get; } = value ?? string.Empty;
    public bool IsKey { get; } = isKey;
    public bool HasValue { get; } = !string.IsNullOrWhiteSpace(value);
}
