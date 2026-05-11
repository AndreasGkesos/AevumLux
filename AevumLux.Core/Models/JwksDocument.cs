using System.Text.Json.Serialization;

namespace AevumLux.Core.Models;

/// <summary>Represents a JSON Web Key Set document.</summary>
public sealed class JwksDocument
{
    [JsonPropertyName("keys")]
    public List<JsonWebKey> Keys { get; set; } = [];
}

/// <summary>Represents a single JSON Web Key with its cryptographic properties.</summary>
public sealed class JsonWebKey
{
    [JsonPropertyName("kty")]
    public string? KeyType { get; set; }

    [JsonPropertyName("kid")]
    public string? KeyId { get; set; }

    [JsonPropertyName("use")]
    public string? Use { get; set; }

    [JsonPropertyName("alg")]
    public string? Algorithm { get; set; }

    [JsonPropertyName("n")]
    public string? Modulus { get; set; }

    [JsonPropertyName("e")]
    public string? Exponent { get; set; }

    [JsonPropertyName("crv")]
    public string? Curve { get; set; }

    [JsonPropertyName("x")]
    public string? X { get; set; }

    [JsonPropertyName("y")]
    public string? Y { get; set; }

    /// <summary>Gets or sets whether this key matched the kid in a provided token header.</summary>
    [JsonIgnore]
    public bool? MatchesTokenKid { get; set; }
}
