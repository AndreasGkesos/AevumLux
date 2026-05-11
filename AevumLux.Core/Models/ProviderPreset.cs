namespace AevumLux.Core.Models;

/// <summary>Defines the built-in provider preset types available for quick setup.</summary>
public static class ProviderPresetType
{
    public const string Keycloak = "Keycloak";
    public const string AzureAd = "AzureAD";
    public const string Auth0 = "Auth0";
    public const string Okta = "Okta";
    public const string Custom = "Custom";
}

/// <summary>Defines the supported environment labels for a provider configuration.</summary>
public static class ProviderEnvironment
{
    public const string Development = "Development";
    public const string Staging = "Staging";
    public const string Production = "Production";
}

/// <summary>Represents a built-in provider preset with pre-filled discovery URL.</summary>
public sealed class ProviderPreset
{
    public string PresetType { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string DiscoveryUrlTemplate { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
