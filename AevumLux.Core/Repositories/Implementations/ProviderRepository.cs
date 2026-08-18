using AevumLux.Core.Helpers;
using AevumLux.Core.Models;
using AevumLux.Core.Repositories.Interfaces;

namespace AevumLux.Core.Repositories.Implementations;

/// <summary>
/// LiteDB-backed repository for <see cref="OidcProvider"/> entities.
/// All operations are wrapped in <see cref="Task.Run"/> to keep LiteDB's
/// synchronous API off the UI thread.
/// </summary>
public sealed class ProviderRepository : IProviderRepository
{
    private const string CollectionName = "providers";

    private readonly LiteDbContext _context;

    public ProviderRepository(LiteDbContext context)
    {
        _context = Guard.AgainstNull(context, nameof(context));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<OidcProvider>> GetAllAsync() =>
        Task.Run<IReadOnlyList<OidcProvider>>(() =>
        {
            var collection = _context.GetCollection<OidcProvider>(CollectionName);
            return collection.FindAll().OrderBy(p => p.Name).ToList();
        });

    /// <inheritdoc/>
    public Task<OidcProvider?> GetByIdAsync(string id) =>
        Task.Run<OidcProvider?>(() =>
        {
            Guard.AgainstNullOrWhiteSpace(id, nameof(id));
            var collection = _context.GetCollection<OidcProvider>(CollectionName);
            return collection.FindById(id);
        });

    /// <inheritdoc/>
    public Task UpsertAsync(OidcProvider provider) =>
        Task.Run(() =>
        {
            Guard.AgainstNull(provider, nameof(provider));
            var collection = _context.GetCollection<OidcProvider>(CollectionName);
            collection.Upsert(provider);
        });

    /// <inheritdoc/>
    public Task DeleteAsync(string id) =>
        Task.Run(() =>
        {
            Guard.AgainstNullOrWhiteSpace(id, nameof(id));
            var collection = _context.GetCollection<OidcProvider>(CollectionName);
            collection.Delete(id);
        });

    /// <inheritdoc/>
    public Task SeedIfMissingAsync(IReadOnlyList<OidcProvider> providers) =>
        Task.Run(() =>
        {
            Guard.AgainstNull(providers, nameof(providers));
            var collection = _context.GetCollection<OidcProvider>(CollectionName);
            foreach (var provider in providers)
            {
                if (collection.FindById(provider.Id) is null)
                    collection.Insert(provider);
            }
        });
}
