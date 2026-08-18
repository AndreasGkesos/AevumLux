using System.Security.Claims;
using AevumLux.TestIdentityServer;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

const string CookieScheme = "TestIdentityServerCookie";

var builder = WebApplication.CreateBuilder(args);

// Every scenario file under Scenarios/ is loaded and its clients registered together at
// startup — the server runs once, permanently, with every scenario's client simultaneously
// available. Switching what you're testing is done entirely via AevumLux's own scenario
// picker, never by restarting this process or setting an environment variable.
var scenariosDirectory = Path.Combine(AppContext.BaseDirectory, "Scenarios");
var allScenarios = ScenarioLoader.LoadAll(scenariosDirectory);
var allClients = allScenarios.SelectMany(s => s.Clients).ToList();
var clientsById = allClients.ToDictionary(c => c.ClientId);

builder.Services.AddDbContext<IdentityServerDbContext>(options =>
{
    options.UseInMemoryDatabase("AevumLuxTestIdentityServer");
    options.UseOpenIddict();
});

// Device Code's verification step relies on OpenIddict correlating a user_code with the
// browser's already-authenticated session — that correlation is built on top of a real
// authentication scheme, not something OpenIddict validates standalone. A minimal cookie
// scheme (no ASP.NET Identity, no real user store — this is still a test IdP with one fixed
// test user) is the smallest way to give it that "already signed in" session to work with.
builder.Services.AddAuthentication(CookieScheme)
    .AddCookie(CookieScheme, options =>
    {
        options.LoginPath = "/connect/verify";
        options.Cookie.Name = "AevumLuxTestIdentityServer.Session";
    });

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore().UseDbContext<IdentityServerDbContext>();
    })
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token")
            .SetDeviceEndpointUris("/connect/device")
            .SetVerificationEndpointUris("/connect/verify");

        options.AllowClientCredentialsFlow();
        options.AllowAuthorizationCodeFlow();
        options.AllowRefreshTokenFlow();
        options.AllowDeviceCodeFlow();

        // Implicit and ROPC ("password") are both still natively supported by OpenIddict —
        // this is a general-purpose OAuth/OIDC library, not an opinionated one — but neither
        // is enabled by real-world identity providers by default anymore, and OAuth 2.1
        // removes them from the spec entirely. They're wired up per-client below (only
        // scenario clients that opt in via GrantTypes get them) purely so Flow Simulator can
        // make a real call and demonstrate, in the UI, exactly why each is discouraged.
        options.AllowImplicitFlow();
        options.AllowPasswordFlow();

        options.RequireProofKeyForCodeExchange();

        // Server-wide default (15 minutes) — individual clients can override this on their own
        // issued tokens via ClaimsIdentity.SetAccessTokenLifetime at sign-in time (see
        // ApplyAccessTokenLifetimeOverride below), which is what lets cc-expired-tokens get a
        // 5-second lifetime while every other client on this same running server keeps the
        // normal one.
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));

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
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableVerificationEndpointPassthrough();
    });

var app = builder.Build();

await SeedAllScenariosAsync(app, allClients);

app.UseAuthentication();

// Every scenario client with a login step uses the same fixed test-user credentials
// ("test-user" / "test-password") — see SCENARIOS.md. Falls back to those defaults if a
// client didn't set its own, so any client that reaches a login page still works.
const string DefaultTestUsername = "test-user";
const string DefaultTestPassword = "test-password";

// The login page needs to show/validate against the credentials for whichever client_id is
// actually mid-flow — recovered from the OAuth request's carried-forward client_id (GET) or
// posted form field (POST), since multiple clients' login flows can be in progress against
// this one always-running server at the same time.
(string Username, string Password) ResolveLoginCredentials(string? clientId)
{
    if (clientId is not null && clientsById.TryGetValue(clientId, out var client))
        return (client.TestUsername ?? DefaultTestUsername, client.TestPassword ?? DefaultTestPassword);

    return (DefaultTestUsername, DefaultTestPassword);
}

app.MapMethods("/connect/authorize", ["GET", "POST"], async (HttpContext httpContext) =>
{
    var request = httpContext.GetOpenIddictServerRequest()
        ?? throw new InvalidOperationException("The OpenIddict request cannot be retrieved.");

    // This is a test IdP with no real user store — but it does show a real login page and
    // require real form input, so the "user authenticates" step in Authorization Code /
    // Implicit is visible and felt, not an instant invisible auto-sign-in. Consent is still
    // always granted once credentials check out; there's no separate consent screen.
    var (username, password) = ResolveLoginCredentials(request.ClientId);

    if (httpContext.Request.Method == HttpMethods.Get)
        return Results.Content(LoginPage.Render(httpContext.Request.QueryString.Value ?? "", username, password), "text/html");

    var form = await httpContext.Request.ReadFormAsync();
    if (form["username"] != username || form["password"] != password)
        return Results.Content(LoginPage.Render(httpContext.Request.QueryString.Value ?? "", username, password, invalidAttempt: true), "text/html");

    var applicationManager = httpContext.RequestServices.GetRequiredService<IOpenIddictApplicationManager>();
    var application = await applicationManager.FindByClientIdAsync(request.ClientId!)
        ?? throw new InvalidOperationException("The client application could not be found.");

    var identity = new ClaimsIdentity(
        authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
        nameType: Claims.Name,
        roleType: Claims.Role);

    identity.SetClaim(Claims.Subject, "test-user-001");
    identity.SetClaim(Claims.Name, "Test User");
    identity.SetClaim(Claims.Email, "test-user@example.test");
    identity.SetScopes(request.GetScopes());
    identity.SetResources("api"); // maps to the "aud" claim — real IdPs virtually always set one.
    ApplyAccessTokenLifetimeOverride(identity, request.ClientId);

    foreach (var claim in identity.Claims)
        claim.SetDestinations(Destinations.AccessToken, Destinations.IdentityToken);

    return Results.SignIn(new ClaimsPrincipal(identity), authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
});

app.MapMethods("/connect/verify", ["GET", "POST"], async (HttpContext httpContext) =>
{
    // Device Code's verification step: a real person would visit this URL on a second device,
    // sign in, and type in the user_code shown on the first device. This test IdP shows the
    // same login page as /connect/authorize (fixed test credentials, no real user store) so
    // that step is visible/felt instead of being auto-approved invisibly.
    //
    // OpenIddict correlates a user_code with a pending device authorization by reading it off
    // an authenticated request — it needs the browser to already be signed in via a real
    // authentication scheme (the cookie scheme registered above), then re-validates that
    // correlation itself when this endpoint calls AuthenticateAsync(OpenIddict's own scheme).
    // That's why credential check (cookie sign-in) and user_code check (OpenIddict's own) are
    // two separate steps here, matching OpenIddict's own device-flow sample pattern.
    if (httpContext.Request.Method == HttpMethods.Post)
    {
        var form = await httpContext.Request.ReadFormAsync();
        if (form["username"] != DefaultTestUsername || form["password"] != DefaultTestPassword)
            return Results.Content(LoginPage.Render(httpContext.Request.QueryString.Value ?? "", DefaultTestUsername, DefaultTestPassword, invalidAttempt: true, requireUserCode: true), "text/html");

        var sessionIdentity = new ClaimsIdentity(authenticationType: CookieScheme);
        sessionIdentity.AddClaim(new Claim(Claims.Subject, "test-user-001"));
        await httpContext.SignInAsync(CookieScheme, new ClaimsPrincipal(sessionIdentity));

        return Results.Redirect($"/connect/verify?user_code={Uri.EscapeDataString(form["user_code"].ToString())}");
    }

    var request = httpContext.GetOpenIddictServerRequest()
        ?? throw new InvalidOperationException("The OpenIddict request cannot be retrieved.");

    // Not signed in yet (first visit, or the POST above hasn't happened) — show the login +
    // code entry page. Once signed in via the cookie scheme, OpenIddict's own middleware
    // handles correlating request.UserCode with a pending device authorization.
    var cookieAuthentication = await httpContext.AuthenticateAsync(CookieScheme);
    if (!cookieAuthentication.Succeeded)
        return Results.Content(LoginPage.Render(httpContext.Request.QueryString.Value ?? "", DefaultTestUsername, DefaultTestPassword, requireUserCode: true), "text/html");

    if (string.IsNullOrEmpty(request.UserCode))
        return Results.Content(LoginPage.Render(httpContext.Request.QueryString.Value ?? "", DefaultTestUsername, DefaultTestPassword, requireUserCode: true), "text/html");

    // Signed in AND a user_code is present — ask OpenIddict itself whether that code matches
    // a real pending device authorization. AuthenticateAsync only succeeds for a genuine one.
    //
    // TODO: for an unknown/expired user_code, this call throws ArgumentNullException from deep
    // inside OpenIddict's own pipeline (OpenIddictServerHandlers.RedeemTokenEntry.HandleAsync,
    // via ClaimsPrincipal.GetTokenId on a null principal) instead of returning a failed
    // AuthenticateResult the way a valid-but-unauthenticated call normally would. The try/catch
    // below papers over it with the same "try again" UX a clean failure would produce, but the
    // root cause is still open — worth investigating directly against OpenIddict's source
    // (OpenIddict.Server.OpenIddictServerHandlers.Device.cs / the ValidateVerificationAuthentication
    // + RedeemTokenEntry handlers) rather than guessing further from the outside.
    AuthenticateResult deviceAuthentication;
    try
    {
        deviceAuthentication = await httpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
    catch (ArgumentNullException)
    {
        deviceAuthentication = AuthenticateResult.Fail("Invalid or expired user_code.");
    }

    if (!deviceAuthentication.Succeeded)
        return Results.Content(LoginPage.Render("", DefaultTestUsername, DefaultTestPassword, invalidAttempt: true, requireUserCode: true), "text/html");

    var identity = new ClaimsIdentity(
        authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
        nameType: Claims.Name,
        roleType: Claims.Role);

    identity.SetClaim(Claims.Subject, "test-user-001");
    identity.SetClaim(Claims.Name, "Test User");
    identity.SetScopes(request.GetScopes());
    identity.SetResources("api"); // maps to the "aud" claim — real IdPs virtually always set one.
    ApplyAccessTokenLifetimeOverride(identity, request.ClientId);

    foreach (var claim in identity.Claims)
        claim.SetDestinations(Destinations.AccessToken);

    return Results.SignIn(new ClaimsPrincipal(identity), authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
});

app.MapPost("/connect/token", async (HttpContext httpContext) =>
{
    var request = httpContext.GetOpenIddictServerRequest()
        ?? throw new InvalidOperationException("The OpenIddict request cannot be retrieved.");

    if (request.IsDeviceCodeGrantType())
    {
        // OpenIddict restores the ClaimsPrincipal that was signed in against this device_code's
        // authorization when /connect/verify approved it. If the user hasn't hit /connect/verify
        // yet, OpenIddict itself returns authorization_pending before this branch ever runs.
        var principal = (await httpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)).Principal
            ?? throw new InvalidOperationException("The authentication principal could not be retrieved.");

        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    if (request.IsPasswordGrantType())
    {
        // OpenIddict has already validated client_id/client_secret before this handler runs.
        // What's left to demonstrate is the anti-pattern itself: the client app collected the
        // user's raw username/password directly (see FlowSimulatorViewModel) and is posting
        // them straight to the token endpoint — the user never interacts with the identity
        // provider at all. That's exactly the property real IdPs and OAuth 2.1 moved away from.

        // Fixed test credentials — this IdP has no real user store.
        if (request.Username != "test-user" || request.Password != "test-password")
            return Results.BadRequest(new { error = Errors.InvalidGrant, error_description = "Invalid username or password." });

        var identity = new ClaimsIdentity(
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, "test-user-001");
        identity.SetClaim(Claims.Name, "Test User");
        identity.SetScopes(request.GetScopes());
        identity.SetResources("api"); // maps to the "aud" claim — real IdPs virtually always set one.
        ApplyAccessTokenLifetimeOverride(identity, request.ClientId);

        foreach (var claim in identity.Claims)
            claim.SetDestinations(Destinations.AccessToken);

        return Results.SignIn(new ClaimsPrincipal(identity), authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    if (request.IsClientCredentialsGrantType())
    {
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
        identity.SetResources("api"); // maps to the "aud" claim — real IdPs virtually always set one.
        ApplyAccessTokenLifetimeOverride(identity, request.ClientId);

        foreach (var claim in identity.Claims)
            claim.SetDestinations(Destinations.AccessToken);

        return Results.SignIn(new ClaimsPrincipal(identity), authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
    {
        // The authorization_code/refresh_token exchange: OpenIddict has already validated the
        // code/PKCE verifier or refresh token and restored the original ClaimsPrincipal that
        // was signed in at /connect/authorize — just re-sign it in to issue new tokens.
        var principal = (await httpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)).Principal
            ?? throw new InvalidOperationException("The authentication principal could not be retrieved.");

        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    return Results.BadRequest(new { error = Errors.UnsupportedGrantType });
});

app.Logger.LogInformation(
    "AevumLux Test IdentityServer running with {ScenarioCount} scenarios / {ClientCount} clients loaded: {ClientIds}",
    allScenarios.Count, allClients.Count, string.Join(", ", allClients.Select(c => c.ClientId)));

app.Run();

/// <summary>
/// Overrides the access token lifetime for a specific client's issued tokens, when that
/// client's scenario set one (e.g. cc-expired-tokens' 5 seconds), independent of every other
/// client's tokens on this same running server. No-op when the client has no override — those
/// tokens just use the server-wide default set via options.SetAccessTokenLifetime above.
/// </summary>
void ApplyAccessTokenLifetimeOverride(ClaimsIdentity identity, string? clientId)
{
    if (clientId is not null
        && clientsById.TryGetValue(clientId, out var client)
        && client.AccessTokenLifetime is { } lifetime)
    {
        identity.SetAccessTokenLifetime(lifetime);
    }
}

static async Task SeedAllScenariosAsync(WebApplication app, IReadOnlyList<ScenarioClient> clients)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<IdentityServerDbContext>();
    await context.Database.EnsureCreatedAsync();

    var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
    var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

    var allScopes = clients.SelectMany(c => c.AllowedScopes).Distinct();
    foreach (var scopeName in allScopes)
    {
        if (await scopeManager.FindByNameAsync(scopeName) is null)
            await scopeManager.CreateAsync(new OpenIddictScopeDescriptor { Name = scopeName });
    }

    foreach (var client in clients)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = client.ClientId,
            ClientSecret = client.IsPublicClient ? null : client.ClientSecret,
            DisplayName = client.DisplayName,
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
            },
        };

        if (client.IsPublicClient)
            descriptor.ClientType = ClientTypes.Public;

        foreach (var grantType in client.GrantTypes)
        {
            switch (grantType)
            {
                case "client_credentials":
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
                    break;
                case "authorization_code":
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
                    descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
                    break;
                case "refresh_token":
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
                    break;
                case "device_code":
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Device);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.DeviceCode);
                    break;
                case "password":
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.Password);
                    break;
                case "implicit":
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.Implicit);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Token);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.IdTokenToken);
                    break;
            }
        }

        foreach (var redirectUri in client.RedirectUris)
            descriptor.RedirectUris.Add(new Uri(redirectUri));

        foreach (var scopeName in client.AllowedScopes)
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scopeName);

        await applicationManager.CreateAsync(descriptor);
    }
}
