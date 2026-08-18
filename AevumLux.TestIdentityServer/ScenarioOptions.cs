namespace AevumLux.TestIdentityServer;

/// <summary>
/// Describes the client(s) for one demo scenario. Loaded from a JSON file under Scenarios/ —
/// every scenario file is loaded and its clients registered together at startup, so all
/// scenarios are simultaneously available on one always-running server. Switching what you're
/// testing is done entirely from AevumLux's own scenario picker, not by restarting this server.
/// </summary>
public sealed class ScenarioOptions
{
    /// <summary>Short name shown in logs on startup, matching the scenario file name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>One-line description of what this scenario demonstrates.</summary>
    public string Description { get; set; } = string.Empty;

    public List<ScenarioClient> Clients { get; set; } = [];
}

/// <summary>A single OAuth client registered for this scenario.</summary>
public sealed class ScenarioClient
{
    /// <summary>The client_id a caller must present.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The client_secret a caller must present. Deliberately mismatched in
    /// "wrong secret"-style scenarios by setting this to a different value
    /// than what the corresponding AevumLux seeded provider holds. Empty/null
    /// for public clients (authorization_code + PKCE without a client secret).
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Display name shown in logs only.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Scopes this client is permitted to request via client_credentials.</summary>
    public List<string> AllowedScopes { get; set; } = [];

    /// <summary>
    /// Which grant types this client may use. "client_credentials", "authorization_code",
    /// "refresh_token", "device_code" and "password" (ROPC — handled manually, OpenIddict
    /// deliberately dropped native support for it) are supported. Defaults to
    /// client_credentials for backward compatibility with the existing cc- scenarios.
    /// </summary>
    public List<string> GrantTypes { get; set; } = ["client_credentials"];

    /// <summary>
    /// Redirect URIs this client is permitted to use for authorization_code flows.
    /// Required when GrantTypes includes "authorization_code".
    /// </summary>
    public List<string> RedirectUris { get; set; } = [];

    /// <summary>
    /// When true, this client is public (no client_secret) and must use PKCE.
    /// Applies to authorization_code clients only.
    /// </summary>
    public bool IsPublicClient { get; set; }

    /// <summary>
    /// Fixed test username shown/required on the login page rendered by /connect/authorize
    /// and /connect/verify. Set for any scenario whose flow involves a browser login step
    /// (authorization_code, implicit, device_code) so the login feels like an actual step
    /// instead of an instant, invisible auto-sign-in.
    /// </summary>
    public string? TestUsername { get; set; }

    /// <summary>Fixed test password paired with <see cref="TestUsername"/>.</summary>
    public string? TestPassword { get; set; }

    /// <summary>
    /// Overrides the access token lifetime for tokens issued to this specific client, via
    /// OpenIddict's per-identity ClaimsIdentity.SetAccessTokenLifetime — independent of every
    /// other client on this same server. Null means "use the server default" (15 minutes).
    /// Only cc-expired-tokens sets this (to 5 seconds); everything else is null, which is what
    /// lets every scenario's clients coexist on one always-running server instead of each
    /// needing its own process with its own server-wide lifetime.
    /// </summary>
    public TimeSpan? AccessTokenLifetime { get; set; }
}
