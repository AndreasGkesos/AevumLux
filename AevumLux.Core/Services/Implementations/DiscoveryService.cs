using System.Text.Json;
using AevumLux.Core.Helpers;
using AevumLux.Core.Models;
using AevumLux.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AevumLux.Core.Services.Implementations;

/// <summary>
/// Fetches OIDC discovery documents by appending the well-known path to the issuer URL
/// and deserialising the response.
/// </summary>
public sealed class DiscoveryService : IDiscoveryService
{
    private const string WellKnownSuffix = "/.well-known/openid-configuration";

    private readonly HttpClient _httpClient;
    private readonly ILogger<DiscoveryService> _logger;

    public DiscoveryService(HttpClient httpClient, ILogger<DiscoveryService> logger)
    {
        _httpClient = Guard.AgainstNull(httpClient, nameof(httpClient));
        _logger = Guard.AgainstNull(logger, nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<DiscoveryDocument> FetchDiscoveryDocumentAsync(
        string issuerUrl,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstNullOrWhiteSpace(issuerUrl, nameof(issuerUrl));

        var discoveryUrl = BuildDiscoveryUrl(issuerUrl);
        _logger.LogInformation("Fetching discovery document from {Url}", discoveryUrl);

        var response = await _httpClient.GetAsync(discoveryUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

        var document = JsonSerializer.Deserialize<DiscoveryDocument>(rawJson)
            ?? throw new InvalidOperationException("Discovery endpoint returned an empty or unparseable response.");

        document.RawJson = rawJson;
        document.FetchedAt = DateTime.UtcNow;

        _logger.LogInformation("Discovery document fetched successfully for issuer {Issuer}", document.Issuer);
        return document;
    }

    private static string BuildDiscoveryUrl(string issuerUrl)
    {
        var trimmed = issuerUrl.TrimEnd('/');
        return trimmed.EndsWith(WellKnownSuffix, StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : trimmed + WellKnownSuffix;
    }
}
