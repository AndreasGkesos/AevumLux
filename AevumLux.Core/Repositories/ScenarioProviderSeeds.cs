using AevumLux.Core.Models;

namespace AevumLux.Core.Repositories;

/// <summary>
/// The standard set of scenario providers paired with AevumLux.TestIdentityServer's scenario
/// configs (see AevumLux.TestIdentityServer/Scenarios/*.json and SCENARIOS.md). Seeding these
/// makes the full demo matrix reproducible on a fresh machine without hand-typing each one
/// into Provider Manager.
/// </summary>
public static class ScenarioProviderSeeds
{
    private const string IssuerUrl = "http://localhost:7087";

    public static IReadOnlyList<OidcProvider> GetAll() =>
    [
        new OidcProvider
        {
            Id = "scenario-cc-happy-path",
            Name = "CC — Happy Path",
            IssuerUrl = IssuerUrl,
            ClientId = "cc-happy-path",
            Scopes = "api",
            Source = ProviderSource.Scenario,
        },
        new OidcProvider
        {
            Id = "scenario-cc-wrong-secret",
            Name = "CC — Wrong Secret",
            IssuerUrl = IssuerUrl,
            ClientId = "cc-wrong-secret",
            Scopes = "api",
            Source = ProviderSource.Scenario,
        },
        new OidcProvider
        {
            Id = "scenario-cc-expired-tokens",
            Name = "CC — Expired Tokens",
            IssuerUrl = IssuerUrl,
            ClientId = "cc-expired-tokens",
            Scopes = "api",
            Source = ProviderSource.Scenario,
        },
        new OidcProvider
        {
            Id = "scenario-ac-happy-path",
            Name = "AC — Happy Path",
            IssuerUrl = IssuerUrl,
            ClientId = "ac-happy-path",
            RedirectUri = "http://localhost:7890/callback",
            Scopes = "api offline_access",
            Source = ProviderSource.Scenario,
        },
        new OidcProvider
        {
            Id = "scenario-ac-wrong-redirect",
            Name = "AC — Wrong Redirect",
            IssuerUrl = IssuerUrl,
            ClientId = "ac-wrong-redirect",
            RedirectUri = "http://localhost:7890/callback",
            Scopes = "api offline_access",
            Source = ProviderSource.Scenario,
        },
        new OidcProvider
        {
            Id = "scenario-dc-happy-path",
            Name = "DC — Happy Path",
            IssuerUrl = IssuerUrl,
            ClientId = "dc-happy-path",
            Scopes = "api",
            Source = ProviderSource.Scenario,
        },
        new OidcProvider
        {
            Id = "scenario-implicit-happy-path",
            Name = "Implicit — Happy Path (Deprecated)",
            IssuerUrl = IssuerUrl,
            ClientId = "implicit-happy-path",
            RedirectUri = "http://localhost:7890/callback",
            Scopes = "api",
            Source = ProviderSource.Scenario,
        },
        new OidcProvider
        {
            Id = "scenario-ropc-happy-path",
            Name = "ROPC — Happy Path (Deprecated)",
            IssuerUrl = IssuerUrl,
            ClientId = "ropc-happy-path",
            Scopes = "api",
            Source = ProviderSource.Scenario,
        },
    ];
}
