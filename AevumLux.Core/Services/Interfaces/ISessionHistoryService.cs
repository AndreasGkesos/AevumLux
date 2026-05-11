using AevumLux.Core.Models;

namespace AevumLux.Core.Services.Interfaces;

/// <summary>
/// Manages the in-session activity history. History is in-memory and cleared on app close
/// unless the user explicitly saves a session.
/// </summary>
public interface ISessionHistoryService
{
    /// <summary>Gets a read-only view of the current session's history entries, newest first.</summary>
    IReadOnlyList<SessionEntry> Entries { get; }

    /// <summary>Adds an entry to the session history.</summary>
    void AddEntry(SessionEntryType type, string title, string payloadJson);

    /// <summary>Clears all entries from the session history.</summary>
    void Clear();
}
