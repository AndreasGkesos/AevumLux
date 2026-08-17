using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using AevumLux.Core.Helpers;
using AevumLux.Core.Models;
using AevumLux.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using CoreJsonWebKey = AevumLux.Core.Models.JsonWebKey;
using CoreTokenValidationResult = AevumLux.Core.Models.TokenValidationResult;

namespace AevumLux.Core.Services.Implementations;

/// <summary>
/// Validates JWT signatures against a provider's JWKS and checks standard time-based
/// and identity claims (expiry, not-before, issuer, audience).
/// </summary>
public sealed class TokenValidationService : ITokenValidationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TokenValidationService> _logger;

    public TokenValidationService(HttpClient httpClient, ILogger<TokenValidationService> logger)
    {
        _httpClient = Guard.AgainstNull(httpClient, nameof(httpClient));
        _logger = Guard.AgainstNull(logger, nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<JwksDocument> FetchJwksAsync(string jwksUri, CancellationToken cancellationToken = default)
    {
        Guard.AgainstNullOrWhiteSpace(jwksUri, nameof(jwksUri));

        var response = await _httpClient.GetAsync(jwksUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<JwksDocument>(json)
            ?? throw new InvalidOperationException("JWKS endpoint returned an empty or unparseable response.");
    }

    /// <inheritdoc/>
    public async Task<CoreTokenValidationResult> ValidateAsync(
        string rawToken,
        string jwksUri,
        string expectedIssuer,
        string? expectedAudience,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstNullOrWhiteSpace(rawToken, nameof(rawToken));
        Guard.AgainstNullOrWhiteSpace(jwksUri, nameof(jwksUri));
        Guard.AgainstNullOrWhiteSpace(expectedIssuer, nameof(expectedIssuer));

        rawToken = rawToken.Trim();
        var result = new CoreTokenValidationResult();

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(rawToken))
        {
            result.Checks.Add(Failed("Token Format", "The token is a well-formed JWT.", "The token could not be parsed as a JWT."));
            result.IsValid = false;
            return result;
        }

        var jwks = await FetchJwksAsync(jwksUri, cancellationToken);
        var signingKeys = jwks.Keys.Select(ToSecurityKey).Where(k => k is not null).Select(k => k!).ToList();

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = signingKeys,
            ValidateIssuer = true,
            ValidIssuer = expectedIssuer,
            ValidateAudience = expectedAudience is not null,
            ValidAudience = expectedAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
        };

        try
        {
            handler.ValidateToken(rawToken, validationParameters, out var validatedToken);
            var jwt = (JwtSecurityToken)validatedToken;

            result.Checks.Add(Passed("Signature", "The token signature matches a key from the provider's JWKS."));
            result.Checks.Add(Passed("Expiry", "The token has not expired (exp claim)."));
            result.Checks.Add(Passed("Issuer", "The token issuer matches the expected issuer.", jwt.Issuer, expectedIssuer));

            if (expectedAudience is not null)
                result.Checks.Add(Passed("Audience", "The token audience matches the expected audience.", string.Join(", ", jwt.Audiences), expectedAudience));

            result.IsValid = true;
        }
        catch (SecurityTokenSignatureKeyNotFoundException ex)
        {
            result.Checks.Add(Failed("Signature", "The token signature matches a key from the provider's JWKS.", $"No matching key found for this token's kid. {ex.Message}"));
        }
        catch (SecurityTokenInvalidSignatureException ex)
        {
            result.Checks.Add(Failed("Signature", "The token signature matches a key from the provider's JWKS.", ex.Message));
        }
        catch (SecurityTokenExpiredException ex)
        {
            result.Checks.Add(Failed("Expiry", "The token has not expired (exp claim).", ex.Message));
        }
        catch (SecurityTokenNotYetValidException ex)
        {
            result.Checks.Add(Failed("Not Before", "The token is not used before its nbf claim.", ex.Message));
        }
        catch (SecurityTokenInvalidIssuerException ex)
        {
            result.Checks.Add(Failed("Issuer", "The token issuer matches the expected issuer.", ex.Message, expectedIssuer: expectedIssuer));
        }
        catch (SecurityTokenInvalidAudienceException ex)
        {
            result.Checks.Add(Failed("Audience", "The token audience matches the expected audience.", ex.Message, expectedIssuer: expectedAudience));
        }
        catch (SecurityTokenException ex)
        {
            result.Checks.Add(Failed("Token Validation", "The token passes all cryptographic and claims checks.", ex.Message));
        }

        result.IsValid = result.Checks.All(c => c.Passed);

        return result;
    }

    private static SecurityKey? ToSecurityKey(CoreJsonWebKey key)
    {
        try
        {
            return key.KeyType switch
            {
                "RSA" when key.Modulus is not null && key.Exponent is not null => new RsaSecurityKey(new System.Security.Cryptography.RSAParameters
                {
                    Modulus = Base64UrlDecode(key.Modulus),
                    Exponent = Base64UrlDecode(key.Exponent),
                })
                { KeyId = key.KeyId },
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var base64 = input.Replace('-', '+').Replace('_', '/');
        var padding = (base64.Length % 4) switch { 2 => "==", 3 => "=", _ => string.Empty };
        return Convert.FromBase64String(base64 + padding);
    }

    private static ValidationCheck Passed(string name, string description, string? actual = null, string? expected = null) => new()
    {
        Name = name,
        Passed = true,
        Description = description,
        ActualValue = actual,
        ExpectedValue = expected,
    };

    private static ValidationCheck Failed(string name, string description, string reason, string? expectedIssuer = null) => new()
    {
        Name = name,
        Passed = false,
        Description = description,
        FailureReason = reason,
        ExpectedValue = expectedIssuer,
    };
}
