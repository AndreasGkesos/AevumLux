using AevumLux.Core.Models;

namespace AevumLux.Core.Services.Interfaces;

/// <summary>Simulates OIDC and OAuth 2.0 flows step by step, yielding each step as it executes.</summary>
public interface IFlowSimulatorService
{
    /// <summary>
    /// Executes the Authorization Code with PKCE flow, yielding each <see cref="FlowStep"/>
    /// as it is completed.
    /// </summary>
    IAsyncEnumerable<FlowStep> SimulateAuthorizationCodePkceAsync(
        OidcProvider provider,
        DiscoveryDocument discovery,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the Client Credentials flow, yielding each <see cref="FlowStep"/>.
    /// </summary>
    IAsyncEnumerable<FlowStep> SimulateClientCredentialsAsync(
        OidcProvider provider,
        DiscoveryDocument discovery,
        string rawClientSecret,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the Device Code flow including polling visualization.
    /// </summary>
    IAsyncEnumerable<FlowStep> SimulateDeviceCodeAsync(
        OidcProvider provider,
        DiscoveryDocument discovery,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the Refresh Token flow given an existing refresh token.
    /// </summary>
    IAsyncEnumerable<FlowStep> SimulateRefreshTokenAsync(
        OidcProvider provider,
        DiscoveryDocument discovery,
        string refreshToken,
        string rawClientSecret,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the Implicit flow. Deprecated by OAuth 2.1 — the access token comes back
    /// directly in the redirect URL's fragment instead of through a server-to-server exchange,
    /// which exposes it to browser history, referrer headers and anything else with access to
    /// the URL. Authorization Code + PKCE replaces this without giving up any capability.
    /// </summary>
    IAsyncEnumerable<FlowStep> SimulateImplicitAsync(
        OidcProvider provider,
        DiscoveryDocument discovery,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the Resource Owner Password Credentials flow. Deprecated — the client app
    /// collects the user's raw username and password directly, instead of the user ever
    /// authenticating with the identity provider. This defeats delegated auth entirely: it's
    /// incompatible with MFA and federated/SSO login, and trains users to type their password
    /// into whatever app asks for it.
    /// </summary>
    IAsyncEnumerable<FlowStep> SimulateResourceOwnerPasswordAsync(
        OidcProvider provider,
        DiscoveryDocument discovery,
        string username,
        string password,
        string rawClientSecret,
        CancellationToken cancellationToken = default);
}
