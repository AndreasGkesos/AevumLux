namespace AevumLux.TestIdentityServer;

/// <summary>
/// Describes the client(s), scopes and token behaviour for one demo scenario.
/// Loaded from a JSON file under Scenarios/ at startup, selected via the
/// ACTIVE_SCENARIO environment variable (defaults to "happy-path").
/// </summary>
public sealed class ScenarioOptions
{
    /// <summary>Short name shown in logs on startup, matching the scenario file name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>One-line description of what this scenario demonstrates.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Access token lifetime for every client in this scenario. This is a server-wide
    /// OpenIddict setting (OpenIddictServerOptions.AccessTokenLifetime cannot be set
    /// per-client), so scenarios needing an already-expired token set this short for
    /// the whole scenario rather than for one client.
    /// </summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

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
    /// than what the corresponding AevumLux seeded provider holds.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Display name shown in logs only.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Scopes this client is permitted to request via client_credentials.</summary>
    public List<string> AllowedScopes { get; set; } = [];
}
