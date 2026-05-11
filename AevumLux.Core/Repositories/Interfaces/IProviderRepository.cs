using AevumLux.Core.Models;

namespace AevumLux.Core.Repositories.Interfaces;

/// <summary>
/// Provides persistence operations for <see cref="OidcProvider"/> entities.
/// All database access must go through this interface — never use LiteDB directly outside repositories.
/// </summary>
public interface IProviderRepository
{
    /// <summary>Returns all stored providers.</summary>
    Task<IReadOnlyList<OidcProvider>> GetAllAsync();

    /// <summary>Returns the provider with the given ID, or null if not found.</summary>
    Task<OidcProvider?> GetByIdAsync(string id);

    /// <summary>Inserts or updates the given provider.</summary>
    Task UpsertAsync(OidcProvider provider);

    /// <summary>Deletes the provider with the given ID. No-op if not found.</summary>
    Task DeleteAsync(string id);
}
