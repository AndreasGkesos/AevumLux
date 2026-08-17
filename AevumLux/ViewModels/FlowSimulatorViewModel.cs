using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AevumLux.ViewModels;

/// <summary>
/// ViewModel for the Flow Simulator page. Currently supports Client Credentials only —
/// a single, real HTTP request/response pair against a live token endpoint. No browser
/// or redirect handling is needed for this flow, unlike the user-present flows planned
/// for later (Authorization Code + PKCE, Device Code, Implicit).
/// </summary>
public sealed partial class FlowSimulatorViewModel : ObservableObject
{
    private readonly HttpClient _httpClient;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    private string _tokenEndpoint = "http://localhost:7087/connect/token";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    private string _clientId = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    private string _clientSecret = string.Empty;

    [ObservableProperty]
    private string _scope = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _hasResult;

    public bool ShowEmptyState => !HasResult && !IsBusy;

    // Request preview — shown before and after sending, so the user sees exactly
    // what was sent even while IsBusy is true.
    [ObservableProperty]
    private string _requestMethod = "POST";

    [ObservableProperty]
    private string _requestBody = string.Empty;

    [ObservableProperty]
    private string _requestHeaders = string.Empty;

    /// <summary>
    /// The full request reconstructed as a copy-pasteable curl command, including the
    /// real client secret (unmasked, unlike RequestBody) — needed to actually replay it.
    /// </summary>
    [ObservableProperty]
    private string _curlCommand = string.Empty;

    /// <summary>
    /// The literal URL this request was sent to. For Client Credentials this is just the
    /// token endpoint — the grant parameters are sent in the POST body, not the query
    /// string, so nothing is appended here. Flows that build a real query string (e.g.
    /// Authorization Code's authorize request) should populate this with the actual URL,
    /// not a reconstructed approximation.
    /// </summary>
    [ObservableProperty]
    private string _fullRequestUrl = string.Empty;

    // Response
    [ObservableProperty]
    private bool _requestSucceeded;

    [ObservableProperty]
    private int _responseStatusCode;

    [ObservableProperty]
    private string _rawResponseJson = string.Empty;

    [ObservableProperty]
    private string _accessToken = string.Empty;

    [ObservableProperty]
    private string _tokenType = string.Empty;

    [ObservableProperty]
    private string _expiresIn = string.Empty;

    [ObservableProperty]
    private bool _hasAccessToken;

    public FlowSimulatorViewModel(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        HasResult = false;
        HasAccessToken = false;
        IsBusy = true;

        try
        {
            var endpoint = TokenEndpoint.Trim();

            var formValues = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = ClientId.Trim(),
                ["client_secret"] = ClientSecret.Trim(),
            };

            if (!string.IsNullOrWhiteSpace(Scope))
                formValues["scope"] = Scope.Trim();

            RequestMethod = "POST";
            RequestHeaders = "Content-Type: application/x-www-form-urlencoded";
            var maskedBody = string.Join("&", formValues.Select(kv => $"{kv.Key}={(kv.Key == "client_secret" ? "***" : Uri.EscapeDataString(kv.Value))}"));
            RequestBody = maskedBody;
            FullRequestUrl = endpoint;

            var realBody = string.Join("&", formValues.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
            CurlCommand = $"curl -X POST \"{endpoint}\" \\\n  -H \"Content-Type: application/x-www-form-urlencoded\" \\\n  -d \"{realBody}\"";

            using var content = new FormUrlEncodedContent(formValues);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            ResponseStatusCode = (int)response.StatusCode;
            RequestSucceeded = response.IsSuccessStatusCode;

            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
            RawResponseJson = PrettyPrint(rawJson);

            if (RequestSucceeded)
                PopulateTokenFields(rawJson);

            HasResult = true;
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"Could not reach the token endpoint. Check the URL and that the test IdentityServer is running.\n\nDetail: {ex.Message}";
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

    private bool CanRun() =>
        !string.IsNullOrWhiteSpace(TokenEndpoint) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret) &&
        !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        RunCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private void PopulateTokenFields(string rawJson)
    {
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        AccessToken = root.TryGetProperty("access_token", out var token) ? token.GetString() ?? string.Empty : string.Empty;
        TokenType = root.TryGetProperty("token_type", out var type) ? type.GetString() ?? string.Empty : string.Empty;
        ExpiresIn = root.TryGetProperty("expires_in", out var expires) ? $"{expires.GetInt32()} seconds" : string.Empty;
        HasAccessToken = !string.IsNullOrEmpty(AccessToken);
    }

    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    private static string PrettyPrint(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, IndentedOptions);
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
