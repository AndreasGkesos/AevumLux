using AevumLux.Core.Models;

namespace AevumLux.Core.Services.Interfaces;

/// <summary>Decodes and analyses JWT tokens without performing cryptographic validation.</summary>
public interface IJwtService
{
    /// <summary>
    /// Decodes a raw JWT string into its header, payload and signature components.
    /// This method does NOT validate the signature.
    /// </summary>
    /// <param name="rawToken">The raw JWT string (three Base64url segments separated by dots).</param>
    /// <returns>A fully decoded <see cref="JwtTokenInfo"/>.</returns>
    /// <exception cref="FormatException">Thrown when the token is not a valid JWT structure.</exception>
    JwtTokenInfo Decode(string rawToken);
}
