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
}
