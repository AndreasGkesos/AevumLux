using AevumLux.Core.Models;

namespace AevumLux.Core.Services.Interfaces;

/// <summary>
/// Performs cryptographic and claims validation on JWT tokens using JWKS from the provider.
/// </summary>
public interface ITokenValidationService
{
    /// <summary>
    /// Fetches the JWKS from <paramref name="jwksUri"/> and validates the token signature,
    /// expiry, issuer and audience.
    /// </summary>
    /// <param name="rawToken">The raw JWT to validate.</param>
    /// <param name="jwksUri">The URI of the provider's JWKS endpoint.</param>
    /// <param name="expectedIssuer">The expected issuer claim value.</param>
    /// <param name="expectedAudience">The expected audience claim value, or null to skip audience check.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<TokenValidationResult> ValidateAsync(
        string rawToken,
        string jwksUri,
        string expectedIssuer,
        string? expectedAudience,
        CancellationToken cancellationToken = default);

    /// <summary>Fetches and parses the JWKS document from the given URI.</summary>
    Task<JwksDocument> FetchJwksAsync(string jwksUri, CancellationToken cancellationToken = default);
}
