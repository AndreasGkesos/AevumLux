using AevumLux.Core.Helpers;
using LiteDB;

namespace AevumLux.Core.Repositories;

/// <summary>
/// Owns the single <see cref="LiteDatabase"/> instance for the application.
/// LiteDB is file-based and single-writer, so one shared instance per process is correct.
/// Register this as a singleton in DI.
/// </summary>
public sealed class LiteDbContext : IDisposable
{
    private readonly LiteDatabase _database;
    private bool _disposed;

    /// <summary>
    /// Initialises the LiteDB database at the given file path.
    /// Creates the file and all parent directories if they do not exist.
    /// </summary>
    public LiteDbContext(string databasePath)
    {
        Guard.AgainstNullOrWhiteSpace(databasePath, nameof(databasePath));

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _database = new LiteDatabase(databasePath);
    }

    /// <summary>Gets the underlying LiteDB database instance.</summary>
    public LiteDatabase Database
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _database;
        }
    }

    /// <summary>Gets a typed collection from the database.</summary>
    public ILiteCollection<T> GetCollection<T>(string name) =>
        Database.GetCollection<T>(name);

    public void Dispose()
    {
        if (_disposed) return;
        _database.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
