using Microsoft.EntityFrameworkCore;

namespace AevumLux.TestIdentityServer;

/// <summary>
/// EF Core context backing OpenIddict's client/token/scope stores.
/// Uses the in-memory provider — state is deliberately not persisted across
/// restarts, since every scenario is meant to start from a clean, known state.
/// </summary>
public sealed class IdentityServerDbContext(DbContextOptions<IdentityServerDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.UseOpenIddict();
    }
}
