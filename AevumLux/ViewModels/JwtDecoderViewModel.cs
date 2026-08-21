using System.Collections.ObjectModel;
using System.Text.Json;
using AevumLux.Core.Models;
using AevumLux.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AevumLux.ViewModels;

public sealed partial class JwtDecoderViewModel : ObservableObject
{
    private readonly IJwtService _jwtService;
    private readonly ISessionHistoryService _sessionHistory;
    private readonly ILogger<JwtDecoderViewModel> _logger;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DecodeCommand))]
    private string _rawToken = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _hasResult;

    [ObservableProperty]
    private string _tokenTypeBadge = string.Empty;

    [ObservableProperty]
    private string _expiryStatus = string.Empty;

    [ObservableProperty]
    private bool _isExpired;

    public bool ShowEmptyState => !HasResult;

    public ObservableCollection<ClaimRow> HeaderClaims { get; } = [];
    public ObservableCollection<ClaimRow> PayloadClaims { get; } = [];

    public JwtDecoderViewModel(IJwtService jwtService, ISessionHistoryService sessionHistory, ILogger<JwtDecoderViewModel> logger)
    {
        _jwtService = jwtService;
        _sessionHistory = sessionHistory;
        _logger = logger;
    }

    [RelayCommand(CanExecute = nameof(CanDecode))]
    private void Decode()
    {
        ErrorMessage = null;
        HasResult = false;
        HeaderClaims.Clear();
        PayloadClaims.Clear();

        try
        {
            var info = _jwtService.Decode(RawToken.Trim());

            PopulateClaims(HeaderClaims, info.Header);
            PopulateClaims(PayloadClaims, info.Payload);

            TokenTypeBadge = info.TokenType switch
            {
                TokenType.AccessToken => "Access Token",
                TokenType.IdToken => "ID Token",
                TokenType.RefreshToken => "Refresh Token",
                _ => "JWT"
            };

            IsExpired = info.IsExpired;
            ExpiryStatus = BuildExpiryStatus(info);
            HasResult = true;

            _sessionHistory.AddEntry(
                SessionEntryType.JwtDecoded,
                $"JWT: {Truncate(RawToken.Trim(), 40)}",
                JsonSerializer.Serialize(info.Payload));

            _logger.LogInformation(
                "JWT decoded. TokenType={TokenType} Issuer={Issuer} Expiry={Expiry} ClaimNames={ClaimNames}",
                TokenTypeBadge,
                info.Payload.TryGetValue("iss", out var iss) ? iss?.ToString() : null,
                info.ExpiresAt,
                string.Join(", ", info.Payload.Keys));
        }
        catch (FormatException ex)
        {
            ErrorMessage = $"Invalid token format. Make sure you paste a complete JWT.\n\nDetail: {ex.Message}";
            _logger.LogWarning(ex, "JWT decode failed: invalid token format.");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unexpected error: {ex.Message}";
            _logger.LogError(ex, "JWT decode failed unexpectedly.");
        }
    }

    private bool CanDecode() => !string.IsNullOrWhiteSpace(RawToken);

    private static void PopulateClaims(ObservableCollection<ClaimRow> target, Dictionary<string, object?> claims)
    {
        foreach (var (key, value) in claims)
        {
            var displayValue = value is JsonElement el
                ? FormatJsonElement(el)
                : value?.ToString() ?? string.Empty;

            target.Add(new ClaimRow(key, FriendlyClaimName(key), displayValue));
        }
    }

    private static string FormatJsonElement(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString() ?? string.Empty,
        JsonValueKind.Number => el.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "(null)",
        JsonValueKind.Array => string.Join(", ", el.EnumerateArray().Select(x => x.GetRawText().Trim('"'))),
        _ => el.GetRawText()
    };

    private static string BuildExpiryStatus(JwtTokenInfo info)
    {
        if (info.ExpiresAt is null)
            return "No expiry claim (exp)";

        if (info.IsExpired)
        {
            var ago = DateTime.UtcNow - info.ExpiresAt.Value;
            return $"Expired {FormatDuration(ago)} ago  ({info.ExpiresAt.Value:yyyy-MM-dd HH:mm:ss} UTC)";
        }

        var remaining = info.TimeUntilExpiry!.Value;
        return $"Expires in {FormatDuration(remaining)}  ({info.ExpiresAt.Value:yyyy-MM-dd HH:mm:ss} UTC)";
    }

    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays}d {ts.Hours}h";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
        return $"{(int)ts.TotalSeconds}s";
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "…";

    private static string FriendlyClaimName(string key) => key switch
    {
        "sub" => "Subject",
        "iss" => "Issuer",
        "aud" => "Audience",
        "exp" => "Expires At",
        "iat" => "Issued At",
        "nbf" => "Not Before",
        "jti" => "JWT ID",
        "azp" => "Authorized Party",
        "nonce" => "Nonce",
        "at_hash" => "Access Token Hash",
        "c_hash" => "Code Hash",
        "acr" => "Auth Context Class",
        "amr" => "Auth Methods",
        "auth_time" => "Auth Time",
        "name" => "Full Name",
        "given_name" => "First Name",
        "family_name" => "Last Name",
        "email" => "Email",
        "email_verified" => "Email Verified",
        "phone_number" => "Phone",
        "picture" => "Picture URL",
        "locale" => "Locale",
        "zoneinfo" => "Timezone",
        "updated_at" => "Profile Updated At",
        "sid" => "Session ID",
        "scp" => "Scopes",
        "scope" => "Scopes",
        "roles" => "Roles",
        "groups" => "Groups",
        "tid" => "Tenant ID",
        "oid" => "Object ID",
        "alg" => "Algorithm",
        "typ" => "Type",
        "kid" => "Key ID",
        _ => key
    };
}

public sealed class ClaimRow(string key, string friendlyName, string value)
{
    public string Key { get; } = key;
    public string FriendlyName { get; } = friendlyName != key ? friendlyName : string.Empty;
    public string Value { get; } = value;
    public bool HasFriendlyName { get; } = friendlyName != key;
}
