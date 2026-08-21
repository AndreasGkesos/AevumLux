using AevumLux.Core.Models;
using AevumLux.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AevumLux.Core.Services.Implementations;

/// <summary>
/// In-memory session history store. Registered as a singleton so the history persists
/// for the lifetime of the application but is cleared on restart.
/// </summary>
public sealed class SessionHistoryService : ISessionHistoryService
{
    private const int MaxEntries = 200;

    private readonly List<SessionEntry> _entries = [];
    private readonly ILogger<SessionHistoryService> _logger;

    public SessionHistoryService(ILogger<SessionHistoryService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyList<SessionEntry> Entries => _entries.AsReadOnly();

    /// <inheritdoc/>
    public void AddEntry(SessionEntryType type, string title, string payloadJson)
    {
        _entries.Insert(0, new SessionEntry
        {
            EntryType = type,
            Title = title,
            PayloadJson = payloadJson,
            CreatedAt = DateTime.UtcNow
        });

        if (_entries.Count > MaxEntries)
            _entries.RemoveAt(_entries.Count - 1);
    }

    /// <inheritdoc/>
    public void Clear()
    {
        var count = _entries.Count;
        _entries.Clear();
        _logger.LogInformation("Session history cleared. EntriesRemoved={EntriesRemoved}", count);
    }
}
