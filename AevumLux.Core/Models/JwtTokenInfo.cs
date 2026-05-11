namespace AevumLux.Core.Models;

/// <summary>Represents a decoded JWT token with all three parts parsed.</summary>
public sealed class JwtTokenInfo
{
    /// <summary>Gets or sets the raw JWT string (header.payload.signature).</summary>
    public string RawToken { get; set; } = string.Empty;

    /// <summary>Gets or sets the decoded header claims.</summary>
    public Dictionary<string, object?> Header { get; set; } = [];

    /// <summary>Gets or sets the decoded payload claims.</summary>
    public Dictionary<string, object?> Payload { get; set; } = [];

    /// <summary>Gets or sets the base64url-encoded signature segment.</summary>
    public string SignatureBase64 { get; set; } = string.Empty;

    /// <summary>Gets or sets the detected token type (AccessToken, IdToken, RefreshToken, Unknown).</summary>
    public TokenType TokenType { get; set; } = TokenType.Unknown;

    /// <summary>Gets or sets the token expiry time in UTC, if present.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Gets or sets the token issued-at time in UTC, if present.</summary>
    public DateTime? IssuedAt { get; set; }

    /// <summary>Gets or sets the token not-before time in UTC, if present.</summary>
    public DateTime? NotBefore { get; set; }

    /// <summary>Gets whether the token is currently expired.</summary>
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;

    /// <summary>Gets the remaining time until expiry, or null if no expiry claim.</summary>
    public TimeSpan? TimeUntilExpiry => ExpiresAt.HasValue
        ? ExpiresAt.Value - DateTime.UtcNow
        : null;
}

/// <summary>Classifies the role of a JWT in an OIDC context.</summary>
public enum TokenType
{
    Unknown,
    AccessToken,
    IdToken,
    RefreshToken
}
