using System.Collections.ObjectModel;
using System.Text.Json;
using AevumLux.Core.Models;
using AevumLux.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AevumLux.ViewModels;

/// <summary>ViewModel for the JWKS Explorer page.</summary>
public sealed partial class JwksExplorerViewModel : ObservableObject
{
    private readonly ITokenValidationService _tokenValidationService;
    private readonly IJwtService _jwtService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FetchCommand))]
    private string _jwksUri = string.Empty;

    [ObservableProperty]
    private string _rawToken = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _hasResult;

    public bool ShowEmptyState => !HasResult && !IsBusy;

    public ObservableCollection<ExplorerKey> Keys { get; } = [];

    public JwksExplorerViewModel(ITokenValidationService tokenValidationService, IJwtService jwtService)
    {
        _tokenValidationService = tokenValidationService;
        _jwtService = jwtService;
    }

    [RelayCommand(CanExecute = nameof(CanFetch))]
    private async Task FetchAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        HasResult = false;
        Keys.Clear();
        IsBusy = true;

        try
        {
            var jwks = await _tokenValidationService.FetchJwksAsync(JwksUri.Trim(), cancellationToken);
            var tokenKid = TryExtractKid(RawToken);

            foreach (var key in jwks.Keys)
                Keys.Add(BuildExplorerKey(key, tokenKid));

            HasResult = true;
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"Could not fetch the JWKS document. Check the URL and your connection.\n\nDetail: {ex.Message}";
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

    private bool CanFetch() => !string.IsNullOrWhiteSpace(JwksUri) && !IsBusy;

    partial void OnIsBusyChanged(bool value) => FetchCommand.NotifyCanExecuteChanged();

    private string? TryExtractKid(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return null;

        try
        {
            var info = _jwtService.Decode(rawToken.Trim());
            return info.Header.TryGetValue("kid", out var kidValue)
                ? (kidValue is JsonElement el ? el.GetString() : kidValue?.ToString())
                : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static ExplorerKey BuildExplorerKey(JsonWebKey key, string? tokenKid)
    {
        var matches = tokenKid is not null && string.Equals(key.KeyId, tokenKid, StringComparison.Ordinal);

        var details = new List<KeyDetail>();
        if (key.KeyType is not null) details.Add(new KeyDetail("Key Type", key.KeyType));
        if (key.Use is not null) details.Add(new KeyDetail("Use", DescribeUse(key.Use)));
        if (key.Algorithm is not null) details.Add(new KeyDetail("Algorithm", key.Algorithm));
        if (key.Curve is not null) details.Add(new KeyDetail("Curve", key.Curve));
        if (key.Modulus is not null) details.Add(new KeyDetail("Modulus (n)", Truncate(key.Modulus, 60)));
        if (key.Exponent is not null) details.Add(new KeyDetail("Exponent (e)", key.Exponent));
        if (key.X is not null) details.Add(new KeyDetail("X", Truncate(key.X, 60)));
        if (key.Y is not null) details.Add(new KeyDetail("Y", Truncate(key.Y, 60)));

        return new ExplorerKey(
            key.KeyId ?? "(no kid)",
            key.KeyType ?? "Unknown",
            matches,
            tokenKid is not null,
            details);
    }

    private static string DescribeUse(string use) => use switch
    {
        "sig" => "Signature verification",
        "enc" => "Encryption",
        _ => use
    };

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "…";
}

/// <summary>A single labelled field shown for a key.</summary>
public sealed class KeyDetail(string label, string value)
{
    public string Label { get; } = label;
    public string Value { get; } = value;
}

/// <summary>A JWKS key enriched for display, including whether it matches a supplied token's kid.</summary>
public sealed class ExplorerKey(string keyId, string keyType, bool matchesToken, bool hasTokenToCompare, IReadOnlyList<KeyDetail> details)
{
    public string KeyId { get; } = keyId;
    public string KeyType { get; } = keyType;
    public bool MatchesToken { get; } = matchesToken;
    public bool HasTokenToCompare { get; } = hasTokenToCompare;
    public IReadOnlyList<KeyDetail> Details { get; } = details;
}
