namespace AevumLux.Core.Models;

/// <summary>Represents a single entry in the in-session history log.</summary>
public sealed class SessionEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Gets or sets the type of activity recorded.</summary>
    public SessionEntryType EntryType { get; set; }

    /// <summary>Gets or sets a short human-readable title for this history entry.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC time this entry was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the serialised JSON payload for this entry (token info, discovery doc, etc.).</summary>
    public string PayloadJson { get; set; } = string.Empty;
}

/// <summary>Categorises what kind of activity a session entry represents.</summary>
public enum SessionEntryType
{
    JwtDecoded,
    DiscoveryFetched,
    TokenValidated,
    FlowSimulated,
    ClaimsInspected
}
