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

    /// <summary>Gets or sets the OAuth 2.0 client ID.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the DPAPI-encrypted client secret.
    /// Never store the raw secret; always encrypt before persisting.
    /// </summary>
    public string? EncryptedClientSecret { get; set; }

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
}
