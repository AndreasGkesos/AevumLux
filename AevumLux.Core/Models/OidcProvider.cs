namespace AevumLux.Core.Models;

/// <summary>
/// Represents a saved OIDC provider configuration including connection details and environment presets.
/// </summary>
public sealed class OidcProvider
{
    /// <summary>Gets or sets the unique identifier for this provider.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Gets or sets the display name for this provider.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the provider issuer URL (base URL used for discovery).</summary>
    public string IssuerUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the provider's JWKS URI, used by Token Validator and JWKS Explorer.</summary>
    public string? JwksUri { get; set; }

    /// <summary>Gets or sets the OAuth 2.0 client ID.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Gets or sets the environment label (e.g. Development, Staging, Production).</summary>
    public string Environment { get; set; } = ProviderEnvironment.Development;

    /// <summary>Gets or sets the preset type if this provider was created from a built-in preset.</summary>
    public string? PresetType { get; set; }

    /// <summary>Gets or sets the redirect URI used during authorization code flows.</summary>
    public string RedirectUri { get; set; } = "http://localhost:7890/callback";

    /// <summary>Gets or sets the space-delimited scopes to request.</summary>
    public string Scopes { get; set; } = "openid profile email";

    /// <summary>Gets or sets the UTC timestamp when this provider was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the UTC timestamp when this provider was last modified.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets where this provider came from. Manual providers are for real debugging
    /// work and appear in Provider Manager's list. Scenario providers are seeded to pair with
    /// a AevumLux.TestIdentityServer scenario config and appear only in Flow Simulator's
    /// provider picker, filtered out of Provider Manager and other explorer-page pickers.
    /// </summary>
    public ProviderSource Source { get; set; } = ProviderSource.Manual;
}

/// <summary>Where an <see cref="OidcProvider"/> came from.</summary>
public enum ProviderSource
{
    /// <summary>Hand-entered by the user via Provider Manager.</summary>
    Manual,

    /// <summary>Seeded to pair with a AevumLux.TestIdentityServer demo scenario.</summary>
    Scenario
}
