using System.Text.Json.Serialization;

namespace AevumLux.Core.Models;

/// <summary>
/// Represents the parsed contents of an OIDC discovery document
/// fetched from the .well-known/openid-configuration endpoint.
/// </summary>
public sealed class DiscoveryDocument
{
    [JsonPropertyName("issuer")]
    public string? Issuer { get; set; }

    [JsonPropertyName("authorization_endpoint")]
    public string? AuthorizationEndpoint { get; set; }

    [JsonPropertyName("token_endpoint")]
    public string? TokenEndpoint { get; set; }

    [JsonPropertyName("userinfo_endpoint")]
    public string? UserinfoEndpoint { get; set; }

    [JsonPropertyName("jwks_uri")]
    public string? JwksUri { get; set; }

    [JsonPropertyName("end_session_endpoint")]
    public string? EndSessionEndpoint { get; set; }

    [JsonPropertyName("registration_endpoint")]
    public string? RegistrationEndpoint { get; set; }

    [JsonPropertyName("introspection_endpoint")]
    public string? IntrospectionEndpoint { get; set; }

    [JsonPropertyName("revocation_endpoint")]
    public string? RevocationEndpoint { get; set; }

    [JsonPropertyName("device_authorization_endpoint")]
    public string? DeviceAuthorizationEndpoint { get; set; }

    [JsonPropertyName("response_types_supported")]
    public List<string> ResponseTypesSupported { get; set; } = [];

    [JsonPropertyName("grant_types_supported")]
    public List<string> GrantTypesSupported { get; set; } = [];

    [JsonPropertyName("scopes_supported")]
    public List<string> ScopesSupported { get; set; } = [];

    [JsonPropertyName("subject_types_supported")]
    public List<string> SubjectTypesSupported { get; set; } = [];

    [JsonPropertyName("id_token_signing_alg_values_supported")]
    public List<string> IdTokenSigningAlgValuesSupported { get; set; } = [];

    [JsonPropertyName("token_endpoint_auth_methods_supported")]
    public List<string> TokenEndpointAuthMethodsSupported { get; set; } = [];

    [JsonPropertyName("claims_supported")]
    public List<string> ClaimsSupported { get; set; } = [];

    [JsonPropertyName("code_challenge_methods_supported")]
    public List<string> CodeChallengeMethodsSupported { get; set; } = [];

    [JsonPropertyName("response_modes_supported")]
    public List<string> ResponseModesSupported { get; set; } = [];

    /// <summary>Gets the raw JSON of the full document for display purposes.</summary>
    [JsonIgnore]
    public string RawJson { get; set; } = string.Empty;

    /// <summary>Gets the UTC time this document was fetched.</summary>
    [JsonIgnore]
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}
