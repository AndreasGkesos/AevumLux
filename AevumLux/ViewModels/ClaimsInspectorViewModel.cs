using System.Collections.ObjectModel;
using System.Text.Json;
using AevumLux.Core.Models;
using AevumLux.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AevumLux.ViewModels;

/// <summary>ViewModel for the Claims Inspector page.</summary>
public sealed partial class ClaimsInspectorViewModel : ObservableObject
{
    private readonly IJwtService _jwtService;
    private readonly ISessionHistoryService _sessionHistory;
    private readonly ILogger<ClaimsInspectorViewModel> _logger;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InspectCommand))]
    private string _rawToken = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _hasResult;

    public bool ShowEmptyState => !HasResult;

    public ObservableCollection<ClaimCategoryGroup> CategoryGroups { get; } = [];
    public ObservableCollection<Observation> Observations { get; } = [];
    public ObservableCollection<string> Scopes { get; } = [];

    [ObservableProperty]
    private bool _hasScopes;

    [ObservableProperty]
    private bool _hasObservations;

    public ClaimsInspectorViewModel(IJwtService jwtService, ISessionHistoryService sessionHistory, ILogger<ClaimsInspectorViewModel> logger)
    {
        _jwtService = jwtService;
        _sessionHistory = sessionHistory;
        _logger = logger;
    }

    [RelayCommand(CanExecute = nameof(CanInspect))]
    private void Inspect()
    {
        ErrorMessage = null;
        HasResult = false;
        CategoryGroups.Clear();
        Observations.Clear();
        Scopes.Clear();

        try
        {
            var info = _jwtService.Decode(RawToken.Trim());

            var rows = info.Payload.Select(kvp => BuildRow(kvp.Key, kvp.Value))
                .Concat(info.Header.Select(kvp => BuildRow(kvp.Key, kvp.Value)))
                .OrderBy(r => r.Category)
                .ThenBy(r => r.Key)
                .ToList();

            foreach (var category in Enum.GetValues<ClaimCategory>())
            {
                var entries = rows.Where(r => r.Category == category).ToList();
                if (entries.Count > 0)
                    CategoryGroups.Add(new ClaimCategoryGroup(CategoryTitle(category), entries));
            }

            foreach (var scope in ExtractScopes(info.Payload))
                Scopes.Add(scope);
            HasScopes = Scopes.Count > 0;

            foreach (var observation in BuildObservations(info))
                Observations.Add(observation);
            HasObservations = Observations.Count > 0;

            HasResult = true;

            _sessionHistory.AddEntry(
                SessionEntryType.ClaimsInspected,
                $"Claims: {Truncate(RawToken.Trim(), 40)}",
                JsonSerializer.Serialize(info.Payload));

            _logger.LogInformation(
                "Claims inspected. TokenType={TokenType} ClaimCount={ClaimCount}",
                info.TokenType,
                info.Header.Count + info.Payload.Count);
        }
        catch (FormatException ex)
        {
            ErrorMessage = $"Invalid token format. Make sure you paste a complete JWT.\n\nDetail: {ex.Message}";
            _logger.LogWarning(ex, "Claims inspection failed: invalid token format.");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unexpected error: {ex.Message}";
            _logger.LogError(ex, "Claims inspection failed unexpectedly.");
        }
    }

    private bool CanInspect() => !string.IsNullOrWhiteSpace(RawToken);

    private static IEnumerable<string> ExtractScopes(Dictionary<string, object?> payload)
    {
        foreach (var key in (string[])["scp", "scope"])
        {
            if (!payload.TryGetValue(key, out var raw) || raw is null)
                continue;

            var text = raw is JsonElement el ? FormatJsonElement(el) : raw.ToString() ?? string.Empty;
            foreach (var scope in text.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                yield return scope;

            yield break;
        }
    }

    private static List<Observation> BuildObservations(JwtTokenInfo info)
    {
        var observations = new List<Observation>();

        var alg = info.Header.TryGetValue("alg", out var algVal)
            ? (algVal is JsonElement el ? el.GetString() : algVal?.ToString())
            : null;

        if (string.Equals(alg, "none", StringComparison.OrdinalIgnoreCase))
            observations.Add(new Observation(ObservationSeverity.Critical, "Algorithm is \"none\" - this token has no signature and can be forged by anyone."));
        else if (alg is "HS256" or "HS384" or "HS512")
            observations.Add(new Observation(ObservationSeverity.Info, $"Signed with {alg} (symmetric) - the same secret is used to sign and verify. Anyone with the secret can mint tokens."));
        else if (alg is null)
            observations.Add(new Observation(ObservationSeverity.Warning, "No \"alg\" claim found in the header."));

        if (!info.Payload.ContainsKey("exp"))
            observations.Add(new Observation(ObservationSeverity.Warning, "No \"exp\" claim - this token never expires."));
        else if (info.ExpiresAt is not null && info.IssuedAt is not null)
        {
            var lifetime = info.ExpiresAt.Value - info.IssuedAt.Value;
            if (lifetime > TimeSpan.FromHours(24))
                observations.Add(new Observation(ObservationSeverity.Warning, $"Long-lived token - valid for {lifetime.TotalHours:0} hours from issuance."));
        }

        if (!info.Payload.ContainsKey("aud"))
            observations.Add(new Observation(ObservationSeverity.Warning, "No \"aud\" claim - a resource server cannot confirm this token was intended for it."));

        if (info.TokenType == TokenType.Unknown)
            observations.Add(new Observation(ObservationSeverity.Info, "Could not determine whether this is an access token or ID token from its claims."));

        if (info.IsExpired)
            observations.Add(new Observation(ObservationSeverity.Info, "This token has already expired."));

        return observations;
    }

    private static readonly HashSet<string> UnixTimestampClaims = ["exp", "iat", "nbf", "auth_time"];

    private static InspectedClaim BuildRow(string key, object? value)
    {
        var displayValue = value is JsonElement el
            ? FormatJsonElement(el)
            : value?.ToString() ?? string.Empty;

        if (UnixTimestampClaims.Contains(key) && long.TryParse(displayValue, out var unix))
        {
            var utc = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
            displayValue = $"{displayValue}  ({utc:yyyy-MM-dd HH:mm:ss} UTC)";
        }

        return new InspectedClaim(key, displayValue, Categorize(key), Describe(key));
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

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "…";

    private static string CategoryTitle(ClaimCategory category) => category switch
    {
        ClaimCategory.Identity => "Identity",
        ClaimCategory.Access => "Access & Authorization",
        ClaimCategory.Metadata => "Token Metadata",
        _ => "Other"
    };

    private static ClaimCategory Categorize(string key) => key switch
    {
        "sub" or "name" or "given_name" or "family_name" or "middle_name" or "nickname"
            or "preferred_username" or "profile" or "picture" or "website" or "email"
            or "email_verified" or "gender" or "birthdate" or "zoneinfo" or "locale"
            or "phone_number" or "phone_number_verified" or "address" or "updated_at"
                => ClaimCategory.Identity,

        "scp" or "scope" or "roles" or "groups" or "entitlements" or "permissions"
            or "azp" or "aud" or "client_id"
                => ClaimCategory.Access,

        "iss" or "exp" or "iat" or "nbf" or "jti" or "nonce" or "at_hash" or "c_hash"
            or "acr" or "amr" or "auth_time" or "sid" or "tid" or "oid" or "alg" or "typ" or "kid"
                => ClaimCategory.Metadata,

        _ => ClaimCategory.Other
    };

    private static string Describe(string key) => key switch
    {
        "sub" => "The unique identifier of the subject (user) this token represents.",
        "name" => "The user's full display name.",
        "given_name" => "The user's first name.",
        "family_name" => "The user's last name.",
        "email" => "The user's email address.",
        "email_verified" => "Whether the user's email address has been verified by the provider.",
        "phone_number" => "The user's phone number.",
        "picture" => "URL of the user's profile picture.",
        "locale" => "The user's locale, e.g. en-US.",
        "zoneinfo" => "The user's timezone, e.g. Europe/Athens.",
        "updated_at" => "When the user's profile was last updated.",

        "scp" or "scope" => "The OAuth scopes granted to this token — what it is allowed to access.",
        "roles" => "Application-defined roles assigned to the user.",
        "groups" => "Directory groups the user belongs to.",
        "azp" => "Authorized party — the client the token was issued to, when it differs from aud.",
        "aud" => "Audience — the intended recipient(s) of this token. A resource server should reject tokens where it is not listed here.",
        "client_id" => "The client application this token was issued to.",

        "iss" => "Issuer — identifies the authorization server that created and signed this token.",
        "exp" => "Expiration time — the token must not be accepted after this Unix timestamp.",
        "iat" => "Issued At — when this token was created.",
        "nbf" => "Not Before — the token must not be accepted before this Unix timestamp.",
        "jti" => "JWT ID — a unique identifier for this specific token, useful for revocation/replay checks.",
        "nonce" => "A value tying an ID token to a specific authentication request, preventing replay.",
        "at_hash" => "Access Token hash — allows the client to verify the access token was not swapped.",
        "c_hash" => "Authorization Code hash — allows the client to verify the code was not swapped.",
        "acr" => "Authentication Context Class Reference — describes how the user authenticated (e.g. MFA used).",
        "amr" => "Authentication Methods References — the specific methods used to authenticate (e.g. password, otp).",
        "auth_time" => "The time the user actually authenticated, which may predate token issuance.",
        "sid" => "Session ID — identifies the user's session at the provider, used for logout coordination.",
        "tid" => "Tenant ID — identifies the tenant/organization in multi-tenant providers.",
        "oid" => "Object ID — a stable identifier for the user within the provider's directory.",

        "alg" => "Algorithm — the cryptographic algorithm used to sign this token.",
        "typ" => "Type — identifies the token as a JWT.",
        "kid" => "Key ID — identifies which key from the provider's JWKS was used to sign this token.",

        _ => string.Empty
    };
}

/// <summary>Broad category a claim belongs to, for grouping purposes.</summary>
public enum ClaimCategory
{
    Identity,
    Access,
    Metadata,
    Other
}

/// <summary>A single claim enriched with category and plain-English description.</summary>
public sealed class InspectedClaim(string key, string value, ClaimCategory category, string description)
{
    public string Key { get; } = key;
    public string Value { get; } = value;
    public ClaimCategory Category { get; } = category;
    public string Description { get; } = description;
    public bool HasDescription { get; } = !string.IsNullOrEmpty(description);
}

/// <summary>A named group of claims sharing the same category, for display.</summary>
public sealed class ClaimCategoryGroup(string title, IReadOnlyList<InspectedClaim> claims)
{
    public string Title { get; } = title;
    public IReadOnlyList<InspectedClaim> Claims { get; } = claims;
}

/// <summary>Severity of a security-relevant observation about a token.</summary>
public enum ObservationSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>A single security- or structure-relevant observation surfaced during inspection.</summary>
public sealed class Observation(ObservationSeverity severity, string message)
{
    public ObservationSeverity Severity { get; } = severity;
    public string Message { get; } = message;
    public bool IsCritical { get; } = severity == ObservationSeverity.Critical;
    public bool IsWarning { get; } = severity == ObservationSeverity.Warning;
    public bool IsInfo { get; } = severity == ObservationSeverity.Info;
}
