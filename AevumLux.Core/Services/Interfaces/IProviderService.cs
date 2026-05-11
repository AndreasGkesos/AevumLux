using AevumLux.Core.Models;

namespace AevumLux.Core.Services.Interfaces;

/// <summary>
/// Manages OIDC provider configurations, including built-in presets and user-saved providers.
/// </summary>
public interface IProviderService
{
    /// <summary>Returns all built-in provider presets.</summary>
    IReadOnlyList<ProviderPreset> GetBuiltInPresets();

    /// <summary>Creates a new <see cref="OidcProvider"/> pre-populated from a built-in preset.</summary>
    OidcProvider CreateFromPreset(string presetType);

    /// <summary>
    /// Saves a provider, encrypting the client secret with DPAPI before persisting.
    /// </summary>
    Task SaveProviderAsync(OidcProvider provider, string? rawClientSecret);

    /// <summary>Returns all saved providers.</summary>
    Task<IReadOnlyList<OidcProvider>> GetAllProvidersAsync();

    /// <summary>Returns a provider by its ID, or null if not found.</summary>
    Task<OidcProvider?> GetProviderByIdAsync(string id);

    /// <summary>Deletes the provider with the given ID.</summary>
    Task DeleteProviderAsync(string id);

    /// <summary>
    /// Exports all provider configurations as JSON, excluding client secrets for security.
    /// </summary>
    Task<string> ExportProvidersAsJsonAsync();

    /// <summary>Imports provider configurations from a JSON export. Client secrets are not imported.</summary>
    Task ImportProvidersFromJsonAsync(string json);
}
