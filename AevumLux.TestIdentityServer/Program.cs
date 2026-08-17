using System.Security.Claims;
using AevumLux.TestIdentityServer;
using Microsoft.AspNetCore;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

var scenarioName = Environment.GetEnvironmentVariable("ACTIVE_SCENARIO") ?? "cc-happy-path";
var scenariosDirectory = Path.Combine(AppContext.BaseDirectory, "Scenarios");
var scenario = ScenarioLoader.Load(scenariosDirectory, scenarioName);

builder.Services.AddDbContext<IdentityServerDbContext>(options =>
{
    options.UseInMemoryDatabase("AevumLuxTestIdentityServer");
    options.UseOpenIddict();
});

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore().UseDbContext<IdentityServerDbContext>();
    })
    .AddServer(options =>
    {
        options.SetTokenEndpointUris("/connect/token");
        options.AllowClientCredentialsFlow();
        options.SetAccessTokenLifetime(scenario.AccessTokenLifetime);

        options.AddEphemeralEncryptionKey();
        options.AddEphemeralSigningKey();

        // Access tokens are encrypted (JWE) by default in OpenIddict, which is the right
        // production default but makes them opaque — unreadable by JWT Decoder/Claims
        // Inspector/Token Validator, or any JWT tool. Since this server exists specifically
        // to produce tokens for AevumLux to inspect, issue plain signed JWTs instead.
        options.DisableAccessTokenEncryption();

        // Local test/demo server only — never do this in a real deployment.
        // Lets scenario batch scripts and curl-based testing hit plain HTTP without cert trust setup.
        options.UseAspNetCore()
            .DisableTransportSecurityRequirement()
            .EnableTokenEndpointPassthrough();
    });

var app = builder.Build();

await SeedScenarioAsync(app, scenario);

app.UseAuthentication();

app.MapPost("/connect/token", async (HttpContext httpContext) =>
{
    var request = httpContext.GetOpenIddictServerRequest()
        ?? throw new InvalidOperationException("The OpenIddict request cannot be retrieved.");

    if (!request.IsClientCredentialsGrantType())
        return Results.BadRequest(new { error = Errors.UnsupportedGrantType });

    // OpenIddict has already validated the client_id/client_secret pair against the
    // registered application before this handler runs — an invalid secret never reaches here.
    var applicationManager = httpContext.RequestServices.GetRequiredService<IOpenIddictApplicationManager>();
    var application = await applicationManager.FindByClientIdAsync(request.ClientId!)
        ?? throw new InvalidOperationException("The client application could not be found.");

    var identity = new ClaimsIdentity(
        authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
        nameType: Claims.Name,
        roleType: Claims.Role);

    identity.SetClaim(Claims.Subject, await applicationManager.GetClientIdAsync(application));
    identity.SetClaim(Claims.Name, await applicationManager.GetDisplayNameAsync(application));
    identity.SetScopes(request.GetScopes());

    foreach (var claim in identity.Claims)
        claim.SetDestinations(Destinations.AccessToken);

    return Results.SignIn(new ClaimsPrincipal(identity), authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
});

app.Logger.LogInformation("AevumLux Test IdentityServer running scenario: {Scenario} — {Description}", scenario.Name, scenario.Description);

app.Run();

static async Task SeedScenarioAsync(WebApplication app, ScenarioOptions scenario)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<IdentityServerDbContext>();
    await context.Database.EnsureCreatedAsync();

    var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
    var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

    var allScopes = scenario.Clients.SelectMany(c => c.AllowedScopes).Distinct();
    foreach (var scopeName in allScopes)
    {
        if (await scopeManager.FindByNameAsync(scopeName) is null)
            await scopeManager.CreateAsync(new OpenIddictScopeDescriptor { Name = scopeName });
    }

    foreach (var client in scenario.Clients)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = client.ClientId,
            ClientSecret = client.ClientSecret,
            DisplayName = client.DisplayName,
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
            },
        };

        foreach (var scopeName in client.AllowedScopes)
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scopeName);

        await applicationManager.CreateAsync(descriptor);
    }
}
