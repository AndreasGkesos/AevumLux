using System.Collections.ObjectModel;
using System.Text.Json;
using AevumLux.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AevumLux.ViewModels;

/// <summary>ViewModel for the Scope Analyser page.</summary>
public sealed partial class ScopeAnalyserViewModel : ObservableObject
{
    private readonly IJwtService _jwtService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyseCommand))]
    private string _rawToken = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _hasResult;

    public bool ShowEmptyState => !HasResult;

    public ObservableCollection<AnalysedScope> Scopes { get; } = [];

    public ScopeAnalyserViewModel(IJwtService jwtService)
    {
        _jwtService = jwtService;
    }

    [RelayCommand(CanExecute = nameof(CanAnalyse))]
    private void Analyse()
    {
        ErrorMessage = null;
        HasResult = false;
        Scopes.Clear();

        try
        {
            var info = _jwtService.Decode(RawToken.Trim());

            foreach (var scope in ExtractScopes(info.Payload))
                Scopes.Add(BuildAnalysedScope(scope, info.Payload));

            HasResult = true;
        }
        catch (FormatException ex)
        {
            ErrorMessage = $"Invalid token format. Make sure you paste a complete JWT.\n\nDetail: {ex.Message}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unexpected error: {ex.Message}";
        }
    }

    private bool CanAnalyse() => !string.IsNullOrWhiteSpace(RawToken);

    private static IEnumerable<string> ExtractScopes(Dictionary<string, object?> payload)
    {
        foreach (var key in (string[])["scp", "scope"])
        {
            if (!payload.TryGetValue(key, out var raw) || raw is null)
                continue;

            var text = raw is JsonElement el
                ? (el.ValueKind == JsonValueKind.Array
                    ? string.Join(" ", el.EnumerateArray().Select(x => x.GetString()))
                    : el.GetString() ?? string.Empty)
                : raw.ToString() ?? string.Empty;

            foreach (var scope in text.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                yield return scope;

            yield break;
        }
    }

    private static AnalysedScope BuildAnalysedScope(string scope, Dictionary<string, object?> payload)
    {
        var isStandard = StandardScopes.TryGetValue(scope, out var definition);
        var description = isStandard ? definition!.Description : "Custom or provider-defined scope. Its exact meaning is not standardised — check the provider's documentation.";
        var expectedClaims = isStandard ? definition!.ExpectedClaims : [];

        var claimChecks = expectedClaims
            .Select(claim => BuildClaimCheck(claim, payload))
            .ToList();

        return new AnalysedScope(scope, isStandard, description, claimChecks);
    }

    private static ExpectedClaimCheck BuildClaimCheck(string claimName, Dictionary<string, object?> payload)
    {
        if (!payload.TryGetValue(claimName, out var raw) || raw is null)
            return new ExpectedClaimCheck(claimName, isPresent: false, value: null);

        var value = raw is JsonElement el ? FormatJsonElement(el) : raw.ToString();
        return new ExpectedClaimCheck(claimName, isPresent: true, value: value);
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

    private static readonly Dictionary<string, ScopeDefinition> StandardScopes = new()
    {
        ["openid"] = new ScopeDefinition(
            "Required for OpenID Connect. Signals that the client wants an ID token identifying the user.",
            []),
        ["profile"] = new ScopeDefinition(
            "Grants access to the user's basic profile information.",
            ["name", "family_name", "given_name", "middle_name", "nickname", "preferred_username", "profile", "picture", "website", "gender", "birthdate", "zoneinfo", "locale", "updated_at"]),
        ["email"] = new ScopeDefinition(
            "Grants access to the user's email address and verification status.",
            ["email", "email_verified"]),
        ["address"] = new ScopeDefinition(
            "Grants access to the user's postal address.",
            ["address"]),
        ["phone"] = new ScopeDefinition(
            "Grants access to the user's phone number and verification status.",
            ["phone_number", "phone_number_verified"]),
        ["offline_access"] = new ScopeDefinition(
            "Requests a refresh token, allowing the client to obtain new access tokens without the user being present. Grants long-lived access — treat with caution.",
            []),
    };

    private sealed record ScopeDefinition(string Description, string[] ExpectedClaims);
}

/// <summary>A single scope enriched with its meaning and whether expected claims are present.</summary>
public sealed class AnalysedScope(string name, bool isStandard, string description, IReadOnlyList<ExpectedClaimCheck> expectedClaims)
{
    public string Name { get; } = name;
    public bool IsStandard { get; } = isStandard;
    public string Description { get; } = description;
    public IReadOnlyList<ExpectedClaimCheck> ExpectedClaims { get; } = expectedClaims;
    public bool HasExpectedClaims { get; } = expectedClaims.Count > 0;
}

/// <summary>Whether a claim expected by a scope is actually present in the token, and its value if so.</summary>
public sealed class ExpectedClaimCheck(string claimName, bool isPresent, string? value)
{
    public string ClaimName { get; } = claimName;
    public bool IsPresent { get; } = isPresent;
    public string DisplayText { get; } = isPresent ? $"{claimName}: {value}" : claimName;
}
