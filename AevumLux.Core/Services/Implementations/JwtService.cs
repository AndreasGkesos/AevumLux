using System.Text;
using System.Text.Json;
using AevumLux.Core.Helpers;
using AevumLux.Core.Models;
using AevumLux.Core.Services.Interfaces;

namespace AevumLux.Core.Services.Implementations;

public sealed class JwtService : IJwtService
{
    public JwtTokenInfo Decode(string rawToken)
    {
        Guard.AgainstNullOrWhiteSpace(rawToken, nameof(rawToken));

        var parts = rawToken.Trim().Split('.');
        if (parts.Length != 3)
            throw new FormatException("Token must have exactly three Base64url segments separated by dots.");

        var header = DecodeSegment(parts[0]);
        var payload = DecodeSegment(parts[1]);

        return new JwtTokenInfo
        {
            RawToken = rawToken.Trim(),
            Header = header,
            Payload = payload,
            SignatureBase64 = parts[2],
            TokenType = DetectTokenType(payload),
            ExpiresAt = ReadUnixTimestamp(payload, "exp"),
            IssuedAt = ReadUnixTimestamp(payload, "iat"),
            NotBefore = ReadUnixTimestamp(payload, "nbf"),
        };
    }

    private static Dictionary<string, object?> DecodeSegment(string base64Url)
    {
        var json = DecodeBase64Url(base64Url);
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json)
            ?? [];
    }

    private static string DecodeBase64Url(string base64Url)
    {
        var base64 = base64Url
            .Replace('-', '+')
            .Replace('_', '/');

        var padding = (base64.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty
        };

        var bytes = Convert.FromBase64String(base64 + padding);
        return Encoding.UTF8.GetString(bytes);
    }

    private static TokenType DetectTokenType(Dictionary<string, object?> payload)
    {
        if (payload.TryGetValue("token_use", out var tokenUse))
        {
            return tokenUse?.ToString() switch
            {
                "access" => TokenType.AccessToken,
                "id" => TokenType.IdToken,
                _ => TokenType.Unknown
            };
        }

        if (payload.ContainsKey("at_hash") || payload.ContainsKey("nonce"))
            return TokenType.IdToken;

        if (payload.ContainsKey("scp") || payload.ContainsKey("scope") || payload.ContainsKey("roles"))
            return TokenType.AccessToken;

        return TokenType.Unknown;
    }

    private static DateTime? ReadUnixTimestamp(Dictionary<string, object?> payload, string claim)
    {
        if (!payload.TryGetValue(claim, out var raw) || raw is null)
            return null;

        var text = raw is JsonElement el ? el.GetRawText() : raw.ToString();
        if (long.TryParse(text, out var unix))
            return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;

        return null;
    }
}
