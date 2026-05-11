using AevumLux.Core.Models;

namespace AevumLux.Core.Services.Interfaces;

/// <summary>
/// Fetches and parses OIDC discovery documents from provider .well-known endpoints.
/// </summary>
public interface IDiscoveryService
{
    /// <summary>
    /// Fetches the OpenID Connect discovery document from the provider's well-known endpoint.
    /// </summary>
    /// <param name="issuerUrl">The issuer URL of the provider (without the well-known path).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A parsed <see cref="DiscoveryDocument"/>.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the response cannot be parsed.</exception>
    Task<DiscoveryDocument> FetchDiscoveryDocumentAsync(string issuerUrl, CancellationToken cancellationToken = default);
}
