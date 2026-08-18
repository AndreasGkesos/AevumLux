using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Web;
using AevumLux.Core.Helpers;
using AevumLux.Core.Models;
using AevumLux.Core.Services.Interfaces;

namespace AevumLux.Core.Services.Implementations;

/// <summary>
/// Executes OIDC/OAuth 2.0 flows against a real token endpoint, yielding each step as it
/// completes so the UI can render a live timeline instead of a single request/response pair.
/// </summary>
public sealed class FlowSimulatorService : IFlowSimulatorService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthorizationRedirectHandler _redirectHandler;

    /// <summary>
    /// Static explanation text for every OAuth 2.0 / OIDC parameter Flow Simulator can show,
    /// keyed by parameter name — one shared source so the same param means the same thing no
    /// matter which flow or scenario surfaces it. The actual value seen in a given run is never
    /// stored here; see <see cref="Explain"/>, which pairs a key's explanation with a per-call
    /// value to build the <see cref="ParameterExplanation"/> shown in the UI.
    /// </summary>
    private static readonly Dictionary<string, string> ParameterExplanations = new()
    {
        ["response_type_code"] = "Tells the server AevumLux wants an authorization code back, not a token directly (that's the Implicit flow). Fixed value for this flow, always \"code\".",
        ["response_type_token"] = "Tells the server to put the access token directly in the redirect's URL fragment, instead of a code to exchange. This is the property that makes Implicit deprecated — see the warning below.",
        ["client_id"] = "Identifies which registered application is making this request. Comes from the Client ID field above (or the selected scenario provider).",
        ["redirect_uri"] = "Where the server should send the browser back to after the user signs in. Must exactly match what's registered on the server, or the request is rejected before any login page is shown.",
        ["scope"] = "Space-delimited list of permissions being requested. \"offline_access\" is what causes the server to also issue a refresh token alongside the access token.",
        ["scope_granted"] = "The scopes actually granted — may be narrower than what was requested if the server or user restricted it.",
        ["state"] = "A random value AevumLux generated just for this request. The server is expected to echo it back unchanged; if it doesn't match, that's a sign of a forged/replayed redirect (CSRF), so AevumLux rejects the response.",
        ["state_echoed"] = "Echoed back exactly as AevumLux sent it — checked against the original value to rule out a forged/replayed redirect.",
        ["code_challenge"] = "SHA-256 hash of a secret (code_verifier) AevumLux generated and is keeping to itself. Sent now so the server can later verify the token exchange is coming from this same client.",
        ["code_challenge_method"] = "Tells the server which hashing method was used for code_challenge. Always S256 here (the secure option — the weaker \"plain\" method sends the verifier unhashed and should never be used).",
        ["grant_type_authorization_code"] = "Tells the token endpoint which OAuth flow this request is completing — matching the code/PKCE handshake from the earlier steps.",
        ["grant_type_client_credentials"] = "Tells the token endpoint AevumLux is authenticating as itself — no user, no browser, no authorization code involved.",
        ["grant_type_refresh_token"] = "Tells the token endpoint AevumLux is exchanging a refresh token for a new access token, not starting a fresh sign-in.",
        ["grant_type_device_code"] = "Tells the token endpoint this is a device-flow poll, not a normal token exchange.",
        ["grant_type_password"] = "Tells the token endpoint this request carries raw resource-owner credentials directly — the defining, deprecated characteristic of this flow.",
        ["code"] = "The one-time authorization code the server issued after the user signed in. Single-use and short-lived — exchanging it a second time will fail.",
        ["code_verifier"] = "The original secret AevumLux generated when the flow started, now revealed for the first time. The server hashes it and checks the result matches the code_challenge sent earlier — proving this exchange request is coming from the same client/session that started the flow, not an attacker who only intercepted the code.",
        ["client_secret"] = "A shared secret only the client and the server know, proving this request really is coming from the application registered with that client_id. Anyone with the secret can authenticate as that client, so it must never be exposed to end users or shipped in client-side code.",
        ["refresh_token"] = "The long-lived credential from a previous Authorization Code run. Typically opaque or encrypted (unlike an access token, it's not meant to be inspected) — pasted in above.",
        ["device_code"] = "A long, unguessable code AevumLux keeps to itself and uses to poll the token endpoint — never shown to the person.",
        ["user_code"] = "A short, human-typeable code shown to the person, who enters it at the verification URL on a separate device to approve this pending request.",
        ["verification_uri"] = "Where the person goes to sign in and enter the user_code — typically visited on a phone or another computer, not the device that's polling.",
        ["expires_in_device"] = "Seconds until this device_code/user_code pair expires unapproved — after this, AevumLux has to request a new pair and show a new code.",
        ["username"] = "Typed into the identity provider's own login page, not AevumLux — AevumLux never sees this value, only whether the sign-in ultimately succeeded.",
        ["password"] = "Typed into the identity provider's own login page. Masked here too — AevumLux has no access to it at any point in this flow.",
        ["carried_forward"] = "Carried forward from the original authorize request so the server can resume it once sign-in succeeds.",
        ["username_ropc"] = "The user's real username, typed into AevumLux's own UI and sent straight to the token endpoint — never to the identity provider's own login page. This is the anti-pattern this flow demonstrates.",
        ["password_ropc"] = "The user's real password, collected by AevumLux (masked here, but sent in the clear in the actual request body below). Compare to Authorization Code, where the client app never sees this value at all.",
        ["access_token"] = "The credential AevumLux now uses to call the API on the user's behalf — sent as \"Authorization: Bearer <token>\" on each API request.",
        ["access_token_fragment"] = "The access token itself, sitting right here in the URL fragment — visible in browser history, dev tools, and anywhere else this URL ends up. This is the anti-pattern: compare to Authorization Code, where the token only ever travels in a POST body.",
        ["id_token"] = "An OpenID Connect token describing who the user is (a JWT AevumLux can decode directly) — distinct from the access token, which is what the API checks.",
        ["token_type"] = "How the access token should be presented — \"Bearer\" means any holder of the token can use it, so it must be kept as confidential as a password.",
        ["expires_in"] = "Seconds until the access token expires. After this, API calls with it will be rejected and AevumLux needs a new one (via refresh_token, or by repeating this flow).",
    };

    /// <summary>
    /// Builds a <see cref="ParameterExplanation"/> for the Breakdown table by pairing a
    /// parameter's static, shared explanation (looked up by <paramref name="key"/> in
    /// <see cref="ParameterExplanations"/>) with the actual value seen in this specific run.
    /// <paramref name="key"/> is usually the parameter's own name, but a handful of parameters
    /// carry a different explanation depending on context (e.g. response_type's value differs
    /// per flow) — for those, <paramref name="key"/> is a distinct lookup key while
    /// <paramref name="displayName"/> is what's actually shown as the parameter's name.
    /// </summary>
    private static ParameterExplanation Explain(string key, string value, string? displayName = null) =>
        new() { Name = displayName ?? key, Value = value, Explanation = ParameterExplanations.GetValueOrDefault(key, string.Empty) };

    public FlowSimulatorService(HttpClient httpClient, IAuthorizationRedirectHandler redirectHandler)
    {
        _httpClient = Guard.AgainstNull(httpClient, nameof(httpClient));
        _redirectHandler = Guard.AgainstNull(redirectHandler, nameof(redirectHandler));
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<FlowStep> SimulateAuthorizationCodePkceAsync(
        OidcProvider provider,
        DiscoveryDocument discovery,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(provider, nameof(provider));
        Guard.AgainstNull(discovery, nameof(discovery));

        if (string.IsNullOrWhiteSpace(discovery.AuthorizationEndpoint))
            throw new InvalidOperationException("The discovery document has no authorization_endpoint.");
        if (string.IsNullOrWhiteSpace(discovery.TokenEndpoint))
            throw new InvalidOperationException("The discovery document has no token_endpoint.");

        var (codeVerifier, codeChallenge) = GeneratePkcePair();
        var state = GenerateRandomUrlSafeString(16);
        var step = 0;

        // Step 1: client sends the authorize request.
        var authorizeRequestStep = new FlowStep
        {
            StepNumber = ++step,
            Title = "Client calls the authorize endpoint",
            Explanation = "AevumLux builds the authorization URL and opens it in a browser popup. A PKCE code_challenge is sent now; only AevumLux holds the matching code_verifier, so a stolen authorization code alone can't be exchanged for a token.",
            Status = FlowStepStatus.InProgress,
            StartedAt = DateTime.UtcNow,
        };
        yield return authorizeRequestStep;

        var authorizeUrl = BuildAuthorizeUrl(discovery.AuthorizationEndpoint!, provider, state, codeChallenge);
        authorizeRequestStep.Request = new HttpRequestDetail
        {
            Method = "GET",
            Url = authorizeUrl.ToString(),
            Parameters =
            [
                Explain("response_type_code", "code", "response_type"),
                Explain("client_id", provider.ClientId),
                Explain("redirect_uri", provider.RedirectUri),
                Explain("scope", provider.Scopes),
                Explain("state", state),
                Explain("code_challenge", codeChallenge),
                Explain("code_challenge_method", "S256"),
            ],
        };

        // Channel bridges the popup's synchronous login-submission callback (fired from a
        // WebView2 event handler) into this async iterator. Started here, before step 1 is
        // finalized, so an outright rejection of this very GET (e.g. redirect_uri mismatch) —
        // which the server can return before any login page ever shows — is caught as step 1's
        // own failure instead of silently leaving the flow stuck on a later step that assumes a
        // login page was shown.
        var loginSubmissionChannel = Channel.CreateBounded<HttpRequestDetail>(1);

        var redirectTask = _redirectHandler.CaptureRedirectAsync(
            authorizeUrl,
            provider.RedirectUri,
            onLoginSubmitted: request => loginSubmissionChannel.Writer.TryWrite(request),
            cancellationToken);

        // Give the popup a moment to actually navigate and get a response back — an outright
        // rejection of this GET (e.g. redirect_uri mismatch) surfaces as an
        // AuthorizeRequestRejectedException on redirectTask well before any login page would
        // ever show. Racing against a short delay here is what lets step 1 catch that failure
        // itself, instead of only finding out much later when a step that assumes success is
        // already underway.
        var earlyRejectionCheck = await Task.WhenAny(redirectTask, Task.Delay(TimeSpan.FromSeconds(3), cancellationToken));
        if (earlyRejectionCheck == redirectTask && redirectTask.IsFaulted
            && redirectTask.Exception?.InnerException is AuthorizeRequestRejectedException rejection)
        {
            authorizeRequestStep.Status = FlowStepStatus.Failed;
            authorizeRequestStep.CompletedAt = DateTime.UtcNow;
            // The Response half of the card is what the UI's Failed badge/error text are gated
            // on (Visibility bound to Response != null) — without this, a failure with no
            // Response set renders as an entirely blank right-hand card, invisible with no
            // indication anything happened at all.
            authorizeRequestStep.Response = new HttpResponseDetail
            {
                StatusCode = rejection.StatusCode,
                Body = rejection.ResponseBody,
            };
            authorizeRequestStep.Error = new FlowError
            {
                ErrorCode = "authorize_request_rejected",
                RawResponse = rejection.ResponseBody,
                PlainEnglishExplanation = $"The server rejected the authorize request outright (HTTP {rejection.StatusCode}), before showing any login page.",
                LikelyCauses = ["redirect_uri doesn't exactly match what's registered for this client on the server", "Client not permitted to use this grant type", "Requested scope not allowed for this client"],
                ActionableFix = "Check the provider's client registration on the server against the values used here — most commonly a redirect_uri mismatch.",
            };
            yield return authorizeRequestStep;
            yield break;
        }

        authorizeRequestStep.Status = FlowStepStatus.Success;
        authorizeRequestStep.CompletedAt = DateTime.UtcNow;
        yield return authorizeRequestStep;

        // Step 2: the IdP responds with the login page.
        var loginPageStep = new FlowStep
        {
            StepNumber = ++step,
            Title = "Identity provider responds with the login page",
            Status = FlowStepStatus.Success,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Response = new HttpResponseDetail { StatusCode = 200, Body = "(HTML login page — not itself an OAuth response, no fields to break down)" },
            ResponseExplanation = "Instead of an error or a redirect, the server returns an HTML page asking for a username and password. This is the identity provider's own UI — AevumLux does not build or control this page. OAuth itself doesn't define a login endpoint; this response comes from the same /connect/authorize URL step 1 called — this server, like many real ones, uses one endpoint for both showing the login form (GET) and handling its submission (POST, step 3).",
        };
        yield return loginPageStep;

        // Step 3: the user types credentials; the client's popup submits them.
        var credentialSubmitStep = new FlowStep
        {
            StepNumber = ++step,
            Title = "User types credentials, client submits them",
            Explanation = "The person signing in types a username and password into the identity provider's login page (shown in the popup) and submits the form. This is a real POST to the same /connect/authorize URL step 1 GET'd — AevumLux's popup is just hosting the page, not intercepting or reading these values itself.",
            Status = FlowStepStatus.InProgress,
            StartedAt = DateTime.UtcNow,
        };
        yield return credentialSubmitStep;

        var loginSubmittedTask = loginSubmissionChannel.Reader.ReadAsync(cancellationToken).AsTask();

        var firstCompleted = await Task.WhenAny(redirectTask, loginSubmittedTask);
        if (firstCompleted == loginSubmittedTask && loginSubmittedTask.IsCompletedSuccessfully)
        {
            credentialSubmitStep.Request = loginSubmittedTask.Result;
            AnnotateLoginSubmissionParameters(credentialSubmitStep.Request);
        }

        Uri? redirectResult = null;
        Exception? redirectException = null;
        try
        {
            redirectResult = await redirectTask;
        }
        catch (Exception ex)
        {
            redirectException = ex;
        }

        if (credentialSubmitStep.Request is null && loginSubmissionChannel.Reader.TryRead(out var loginRequest))
        {
            credentialSubmitStep.Request = loginRequest;
            AnnotateLoginSubmissionParameters(credentialSubmitStep.Request);
        }

        // WebView2's WebResourceRequested event (which captures the login POST's real body)
        // can, in practice, race with the redirect that follows it closely enough that it's
        // never observed — the redirect itself is proof a POST happened and succeeded, even
        // when its exact body wasn't captured. Never leave this step's card blank/unexplained;
        // fall back to an honest placeholder instead of silently showing nothing.
        credentialSubmitStep.Request ??= new HttpRequestDetail
        {
            Method = "POST",
            Url = authorizeUrl.ToString(),
            Body = "(submitted from the login page — not captured; the redirect that followed is what proves this succeeded)",
        };

        if (redirectException is not null || redirectResult is null)
        {
            credentialSubmitStep.Status = FlowStepStatus.Failed;
            credentialSubmitStep.CompletedAt = DateTime.UtcNow;
            credentialSubmitStep.Error = new FlowError
            {
                ErrorCode = "redirect_capture_failed",
                RawResponse = redirectException?.Message ?? string.Empty,
                PlainEnglishExplanation = "The browser popup was closed, timed out, or never reached the redirect URI.",
                LikelyCauses = ["The user closed the popup before finishing", "The redirect URI is misconfigured on the server", "Network issue reaching the authorization endpoint"],
                ActionableFix = "Try again, and check that the redirect URI registered on the server matches this provider's Redirect URI exactly.",
            };
            yield return credentialSubmitStep;
            yield break;
        }

        credentialSubmitStep.Status = FlowStepStatus.Success;
        credentialSubmitStep.CompletedAt = DateTime.UtcNow;
        yield return credentialSubmitStep;

        // Step 4: the IdP validates the credentials and responds with a redirect containing the code.
        var queryParams = HttpUtility.ParseQueryString(redirectResult.Query);
        var returnedState = queryParams["state"];
        var error = queryParams["error"];
        var code = queryParams["code"];

        var authRedirectStep = new FlowStep
        {
            StepNumber = ++step,
            Title = "Identity provider validates and redirects back with a code",
            Explanation = "The server checks the submitted credentials. If they're valid, instead of returning a page it returns an HTTP redirect (302) pointing at the client's redirect_uri, with an authorization code attached to the URL as a query parameter.",
            Status = FlowStepStatus.InProgress,
            StartedAt = DateTime.UtcNow,
            Response = new HttpResponseDetail
            {
                StatusCode = 302,
                ReasonPhrase = "Found",
                Body = redirectResult.ToString(),
                Parameters =
                [
                    Explain("code", code ?? string.Empty),
                    Explain("state_echoed", returnedState ?? string.Empty, "state"),
                ],
            },
        };
        yield return authRedirectStep;

        if (error is not null)
        {
            authRedirectStep.Status = FlowStepStatus.Failed;
            authRedirectStep.CompletedAt = DateTime.UtcNow;
            authRedirectStep.Error = new FlowError
            {
                ErrorCode = error,
                RawResponse = redirectResult.Query,
                PlainEnglishExplanation = $"The authorization server returned an error instead of a code: {queryParams["error_description"] ?? error}",
                LikelyCauses = ["Redirect URI mismatch", "Client not permitted to use this grant type", "Requested scope not allowed for this client"],
                ActionableFix = "Check the provider's client registration on the server against the values used here.",
            };
            yield return authRedirectStep;
            yield break;
        }

        if (returnedState != state)
        {
            authRedirectStep.Status = FlowStepStatus.Failed;
            authRedirectStep.CompletedAt = DateTime.UtcNow;
            authRedirectStep.Error = new FlowError
            {
                ErrorCode = "state_mismatch",
                RawResponse = redirectResult.Query,
                PlainEnglishExplanation = "The 'state' value returned by the server doesn't match the one AevumLux sent — a sign the redirect may not be trustworthy (e.g. a cross-site request forgery attempt).",
                LikelyCauses = ["The redirect came from a stale or replayed URL"],
                ActionableFix = "Restart the flow.",
            };
            yield return authRedirectStep;
            yield break;
        }

        if (string.IsNullOrEmpty(code))
        {
            authRedirectStep.Status = FlowStepStatus.Failed;
            authRedirectStep.CompletedAt = DateTime.UtcNow;
            authRedirectStep.Error = new FlowError
            {
                ErrorCode = "missing_code",
                RawResponse = redirectResult.Query,
                PlainEnglishExplanation = "The redirect completed without an authorization code or an error.",
                LikelyCauses = ["Unexpected server response"],
                ActionableFix = "Check the server logs for the test IdentityServer.",
            };
            yield return authRedirectStep;
            yield break;
        }

        authRedirectStep.Status = FlowStepStatus.Success;
        authRedirectStep.CompletedAt = DateTime.UtcNow;
        yield return authRedirectStep;

        // Step 5: the client's popup notices the redirect and reads the code out of it — not a
        // network call, just AevumLux watching the browser and acting on the instruction the
        // IdP's redirect gave it. Shown as its own step so nothing here is left implied.
        var captureStep = new FlowStep
        {
            StepNumber = ++step,
            Title = "Client's popup captures the code",
            Explanation = "AevumLux's popup window has been watching every navigation since it opened. It recognizes this one as going to the app's own redirect_uri, stops the browser from actually trying to load it (nothing is really listening there), and reads the code and state values straight out of the URL. This is entirely local — no network call.",
            Status = FlowStepStatus.Success,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            ResponseExplanation = "The popup closes. AevumLux now holds the authorization code and is ready to exchange it.",
        };
        yield return captureStep;

        // Step 6: client sends the token exchange request.
        var exchangeRequestStep = new FlowStep
        {
            StepNumber = ++step,
            Title = "Client calls the token endpoint",
            Explanation = "AevumLux sends the authorization code plus the original code_verifier directly to the token endpoint — server-to-server this time, not through the browser.",
            Status = FlowStepStatus.InProgress,
            StartedAt = DateTime.UtcNow,
        };
        yield return exchangeRequestStep;

        var formValues = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = provider.RedirectUri,
            ["client_id"] = provider.ClientId,
            ["code_verifier"] = codeVerifier,
        };

        var exchangeParameters = new List<ParameterExplanation>
        {
            Explain("grant_type_authorization_code", "authorization_code", "grant_type"),
            Explain("code", code),
            Explain("redirect_uri", provider.RedirectUri),
            Explain("client_id", provider.ClientId),
            Explain("code_verifier", codeVerifier),
        };
        // ExecuteTokenRequestAsync populates both Request and Response on the same step (it's
        // shared by flows that don't split request/response into separate steps) — here, only
        // the Request half belongs on this step; the Response is moved to its own step below so
        // the two aren't shown twice.
        await ExecuteTokenRequestAsync(exchangeRequestStep, discovery.TokenEndpoint!, formValues, cancellationToken, exchangeParameters);
        var tokenResponse = exchangeRequestStep.Response;
        var tokenStatus = exchangeRequestStep.Status;
        var tokenError = exchangeRequestStep.Error;
        var tokenCompletedAt = exchangeRequestStep.CompletedAt;

        exchangeRequestStep.Response = null;
        exchangeRequestStep.Status = FlowStepStatus.Success;
        exchangeRequestStep.Error = null;
        exchangeRequestStep.ResponseExplanation = null;
        yield return exchangeRequestStep;

        // Step 7: the IdP validates the code_verifier and responds with tokens.
        var tokenResponseStep = new FlowStep
        {
            StepNumber = ++step,
            Title = "Identity provider validates and issues tokens",
            Explanation = "The server recomputes the code_challenge from the code_verifier AevumLux just sent and checks it matches what step 1 sent originally — proving this exchange is coming from the same client/session that started the flow. If it matches, it issues an access token (and, since offline_access was requested, a refresh token).",
            Status = tokenStatus,
            StartedAt = tokenCompletedAt ?? DateTime.UtcNow,
            CompletedAt = tokenCompletedAt,
            Response = tokenResponse,
            Error = tokenError,
            ResponseExplanation = tokenStatus == FlowStepStatus.Success
                ? "The token endpoint returned a valid token response."
                : null,
        };
        AnnotateTokenResponseParameters(tokenResponseStep.Response);
        yield return tokenResponseStep;
    }

    /// <summary>Adds explanations for the username/password fields captured from a real login-page POST.</summary>
    private static void AnnotateLoginSubmissionParameters(HttpRequestDetail? request)
    {
        if (request?.Body is null)
            return;

        var form = HttpUtility.ParseQueryString(request.Body);
        if (form["username"] is not null)
            request.Parameters.Add(Explain("username", form["username"] ?? string.Empty));
        if (form["password"] is not null)
            request.Parameters.Add(Explain("password", "••••••••"));
        foreach (string? key in form.AllKeys)
        {
            if (key is null || key is "username" or "password")
                continue;
            request.Parameters.Add(Explain("carried_forward", form[key] ?? string.Empty, key));
        }
    }

    /// <summary>Adds explanations for the standard OAuth token-response fields, when present.</summary>
    private static void AnnotateTokenResponseParameters(HttpResponseDetail? response)
    {
        if (response?.Body is null)
            return;

        try
        {
            using var doc = JsonDocument.Parse(response.Body);
            var root = doc.RootElement;

            void AddIfPresent(string jsonName, string explanationKey)
            {
                if (root.TryGetProperty(jsonName, out var value))
                {
                    var display = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
                    response.Parameters.Add(Explain(explanationKey, display, jsonName));
                }
            }

            AddIfPresent("access_token", "access_token");
            AddIfPresent("refresh_token", "refresh_token");
            AddIfPresent("id_token", "id_token");
            AddIfPresent("token_type", "token_type");
            AddIfPresent("expires_in", "expires_in");
            AddIfPresent("scope", "scope_granted");
        }
        catch (JsonException)
        {
            // Not a parseable token response (e.g. an error body) — nothing to annotate.
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<FlowStep> SimulateClientCredentialsAsync(
        OidcProvider provider,
        DiscoveryDocument discovery,
        string rawClientSecret,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(provider, nameof(provider));
        Guard.AgainstNull(discovery, nameof(discovery));

        if (string.IsNullOrWhiteSpace(discovery.TokenEndpoint))
            throw new InvalidOperationException("The discovery document has no token_endpoint.");

        var requestStep = new FlowStep
        {
            StepNumber = 1,
            Title = "Client calls the token endpoint",
            Explanation = "AevumLux authenticates as itself, using its own client ID and secret, directly against the token endpoint. There's no user and no browser involved.",
            Status = FlowStepStatus.InProgress,
            StartedAt = DateTime.UtcNow,
        };
        yield return requestStep;

        var formValues = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = provider.ClientId,
            ["client_secret"] = rawClientSecret,
        };

        if (!string.IsNullOrWhiteSpace(provider.Scopes))
            formValues["scope"] = provider.Scopes;

        var parameters = new List<ParameterExplanation>
        {
            Explain("grant_type_client_credentials", "client_credentials", "grant_type"),
            Explain("client_id", provider.ClientId),
            Explain("client_secret", rawClientSecret),
        };
        if (!string.IsNullOrWhiteSpace(provider.Scopes))
            parameters.Add(Explain("scope", provider.Scopes));

        await ExecuteTokenRequestAsync(requestStep, discovery.TokenEndpoint!, formValues, cancellationToken, parameters);
        var response = requestStep.Response;
        var status = requestStep.Status;
        var error = requestStep.Error;
        var completedAt = requestStep.CompletedAt;

        requestStep.Response = null;
        requestStep.Status = FlowStepStatus.Success;
        requestStep.Error = null;
        requestStep.ResponseExplanation = null;
        yield return requestStep;

        var responseStep = new FlowStep
        {
            StepNumber = 2,
            Title = "Identity provider validates and issues tokens",
            Explanation = "The server checks the client_id/client_secret pair against what's registered. If they match, it issues an access token directly — there's no separate authorization step, because there's no user to authorize anything on behalf of.",
            Status = status,
            StartedAt = completedAt ?? DateTime.UtcNow,
            CompletedAt = completedAt,
            Response = response,
            Error = error,
            ResponseExplanation = status == FlowStepStatus.Success ? "The token endpoint returned a valid token response." : null,
        };
        AnnotateTokenResponseParameters(responseStep.Response);
        yield return responseStep;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<FlowStep> SimulateDeviceCodeAsync(
        OidcProvider provider,
        DiscoveryDocument discovery,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(provider, nameof(provider));
        Guard.AgainstNull(discovery, nameof(discovery));

        if (string.IsNullOrWhiteSpace(discovery.TokenEndpoint))
            throw new InvalidOperationException("The discovery document has no token_endpoint.");

        var deviceAuthorizationEndpoint = new Uri(new Uri(discovery.TokenEndpoint), "/connect/device").ToString();
        var step = 0;

        // Step 1: client requests a device_code + user_code pair.
        var deviceRequestStep = new FlowStep
        {
            StepNumber = ++step,
            Title = "Client calls the device authorization endpoint",
            Explanation = "AevumLux (standing in for a device with no easy way to type a password — a CLI, a TV, a set-top box) asks the server for a device_code and a short user_code, without any user interaction yet.",
            Status = FlowStepStatus.InProgress,
            StartedAt = DateTime.UtcNow,
        };
        yield return deviceRequestStep;

        var deviceFormValues = new Dictionary<string, string>
        {
            ["client_id"] = provider.ClientId,
        };
        if (!string.IsNullOrWhiteSpace(provider.Scopes))
            deviceFormValues["scope"] = provider.Scopes;

        deviceRequestStep.Request = new HttpRequestDetail
        {
            Method = "POST",
            Url = deviceAuthorizationEndpoint,
            Headers = new Dictionary<string, string> { ["Content-Type"] = "application/x-www-form-urlencoded" },
            Body = string.Join("&", deviceFormValues.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}")),
            Parameters =
            [
                Explain("client_id", provider.ClientId),
                .. string.IsNullOrWhiteSpace(provider.Scopes)
                    ? Array.Empty<ParameterExplanation>()
                    : [Explain("scope", provider.Scopes)],
            ],
        };

        string? deviceCode = null;
        string? userCode = null;
        string? verificationUri = null;
        HttpResponseDetail? deviceResponse = null;
        FlowStepStatus deviceStatus;
        FlowError? deviceError = null;

        try
        {
            using var content = new FormUrlEncodedContent(deviceFormValues);
            using var response = await _httpClient.PostAsync(deviceAuthorizationEndpoint, content, cancellationToken);
            var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);

            deviceResponse = new HttpResponseDetail
            {
                StatusCode = (int)response.StatusCode,
                ReasonPhrase = response.ReasonPhrase ?? string.Empty,
                Body = rawBody,
            };

            if (!response.IsSuccessStatusCode)
            {
                deviceStatus = FlowStepStatus.Failed;
                deviceError = ParseOAuthError(rawBody);
            }
            else
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;
                deviceCode = root.GetProperty("device_code").GetString();
                userCode = root.GetProperty("user_code").GetString();
                // Deliberately the plain verification_uri, not verification_uri_complete — a
                // real device-code flow has the person type the user_code in by hand on their
                // own device, which is the entire reason this flow exists. Auto-filling it via
                // the complete URI would skip the step Flow Simulator exists to demonstrate.
                verificationUri = root.GetProperty("verification_uri").GetString();

                deviceResponse.Parameters.Add(Explain("device_code", deviceCode ?? string.Empty));
                deviceResponse.Parameters.Add(Explain("user_code", userCode ?? string.Empty));
                deviceResponse.Parameters.Add(Explain("verification_uri", verificationUri ?? string.Empty));
                if (root.TryGetProperty("expires_in", out var deviceExpiresIn))
                    deviceResponse.Parameters.Add(Explain("expires_in_device", deviceExpiresIn.ToString(), "expires_in"));

                deviceStatus = FlowStepStatus.Success;
            }
        }
        catch (HttpRequestException ex)
        {
            deviceStatus = FlowStepStatus.Failed;
            deviceError = new FlowError
            {
                ErrorCode = "connection_failed",
                RawResponse = ex.Message,
                PlainEnglishExplanation = "Could not reach the device authorization endpoint.",
                LikelyCauses = ["The test IdentityServer isn't running", "The endpoint URL is wrong"],
                ActionableFix = "Check the URL and that the server is running.",
            };
        }

        deviceRequestStep.Status = FlowStepStatus.Success;
        deviceRequestStep.CompletedAt = DateTime.UtcNow;
        yield return deviceRequestStep;

        // Step 2: the IdP responds with the device_code/user_code pair.
        var deviceResponseStep = new FlowStep
        {
            StepNumber = ++step,
            Title = "Identity provider issues the device and user codes",
            Explanation = deviceStatus == FlowStepStatus.Success
                ? $"The server returns a device_code (kept secret, used by AevumLux to poll) and a user_code (shown to the person, who enters it at {verificationUri})."
                : "The device authorization request failed.",
            Status = deviceStatus,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Response = deviceResponse,
            Error = deviceError,
        };
        yield return deviceResponseStep;

        if (deviceStatus == FlowStepStatus.Failed)
            yield break;

        // Step 3: the client shows the person the verification page to sign in and approve.
        var userCodeStep = new FlowStep
        {
            StepNumber = ++step,
            Title = "Client opens the verification page for the user",
            Explanation = $"In a real flow, the person would visit the verification URL on a genuinely separate device with a browser (their phone, a nearby PC) — the device asking for approval (a TV, a CLI) typically has no keyboard to type a code into. This simulation doesn't involve a second physical device: AevumLux is standing in for both, and opens the verification page itself in a popup right now. Sign in there with this scenario's test credentials, and when it asks for a code, type in this one: {userCode}",
            Status = FlowStepStatus.InProgress,
            StartedAt = DateTime.UtcNow,
            Request = new HttpRequestDetail { Method = "GET", Url = verificationUri ?? string.Empty },
        };
        yield return userCodeStep;

        try
        {
            await _redirectHandler.ShowInteractivePageAsync(new Uri(verificationUri!), cancellationToken);
            userCodeStep.Status = FlowStepStatus.Success;
            userCodeStep.ResponseExplanation = "The verification popup was closed. If you signed in with the correct test credentials and typed the right code, the poll below will pick up the approval.";
        }
        catch (Exception)
        {
            userCodeStep.Status = FlowStepStatus.Success;
            userCodeStep.ResponseExplanation = "The verification popup could not be shown. The poll below will time out unless the verification URL is opened manually.";
        }
        finally
        {
            userCodeStep.CompletedAt = DateTime.UtcNow;
        }
        yield return userCodeStep;

        // Step 4+: poll the token endpoint until approved (or timeout). Each attempt is its own
        // request step followed by its own response step, so a slow_down/authorization_pending
        // sequence is visible attempt by attempt instead of one card silently updating in place.
        var pollFormValues = new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
            ["device_code"] = deviceCode!,
            ["client_id"] = provider.ClientId,
        };

        const int maxAttempts = 10;
        // RFC 8628 §3.5: on slow_down, the client must increase its polling interval by at
        // least 5 seconds for all subsequent requests — not just wait once and resume the old
        // rate. pollInterval accumulates that increase across the whole loop.
        var pollInterval = TimeSpan.FromSeconds(2);
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await Task.Delay(pollInterval, cancellationToken);

            var pollRequestStep = new FlowStep
            {
                StepNumber = ++step,
                Title = $"Client polls the token endpoint (attempt {attempt}/{maxAttempts})",
                Explanation = "While waiting for the user to approve, AevumLux polls the token endpoint with the device_code at a fixed interval (increasing it if the server asks to slow down).",
                Status = FlowStepStatus.InProgress,
                StartedAt = DateTime.UtcNow,
                Request = new HttpRequestDetail
                {
                    Method = "POST",
                    Url = discovery.TokenEndpoint,
                    Headers = new Dictionary<string, string> { ["Content-Type"] = "application/x-www-form-urlencoded" },
                    Body = string.Join("&", pollFormValues.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}")),
                    Parameters =
                    [
                        Explain("grant_type_device_code", "urn:ietf:params:oauth:grant-type:device_code", "grant_type"),
                        Explain("device_code", deviceCode ?? string.Empty),
                        Explain("client_id", provider.ClientId),
                    ],
                },
            };
            yield return pollRequestStep;

            using var content = new FormUrlEncodedContent(pollFormValues);
            using var response = await _httpClient.PostAsync(discovery.TokenEndpoint, content, cancellationToken);
            var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);

            pollRequestStep.Status = FlowStepStatus.Success;
            pollRequestStep.CompletedAt = DateTime.UtcNow;
            yield return pollRequestStep;

            var pollResponse = new HttpResponseDetail
            {
                StatusCode = (int)response.StatusCode,
                ReasonPhrase = response.ReasonPhrase ?? string.Empty,
                Body = rawBody,
            };

            if (response.IsSuccessStatusCode)
            {
                AnnotateTokenResponseParameters(pollResponse);
                var pollResponseStep = new FlowStep
                {
                    StepNumber = ++step,
                    Title = "Identity provider confirms approval and issues tokens",
                    Explanation = $"Approved after {attempt} poll(s). The server returned a valid token response.",
                    Status = FlowStepStatus.Success,
                    StartedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow,
                    Response = pollResponse,
                    ResponseExplanation = "The token endpoint returned a valid token response.",
                };
                yield return pollResponseStep;
                yield break;
            }

            var error = ParseOAuthError(rawBody);
            var stillPending = error.ErrorCode is "authorization_pending" or "slow_down";
            if (error.ErrorCode == "slow_down")
                pollInterval += TimeSpan.FromSeconds(5);

            var thisPollResponseStep = new FlowStep
            {
                StepNumber = ++step,
                Title = stillPending ? "Identity provider reports approval still pending" : "Identity provider rejects the poll",
                Status = stillPending ? FlowStepStatus.Success : FlowStepStatus.Failed,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Response = pollResponse,
                ResponseExplanation = stillPending ? "Not approved yet — AevumLux will wait and poll again." : null,
                Error = stillPending ? null : error,
            };
            yield return thisPollResponseStep;

            if (!stillPending)
                yield break;
        }

        var timeoutStep = new FlowStep
        {
            StepNumber = ++step,
            Title = "Polling gave up",
            Status = FlowStepStatus.Failed,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Error = new FlowError
            {
                ErrorCode = "polling_timeout",
                RawResponse = string.Empty,
                PlainEnglishExplanation = $"Gave up after {maxAttempts} polls without approval.",
                LikelyCauses = ["The verification step never completed"],
                ActionableFix = "Try running the flow again.",
            },
        };
        yield return timeoutStep;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<FlowStep> SimulateRefreshTokenAsync(
        OidcProvider provider,
        DiscoveryDocument discovery,
        string refreshToken,
        string rawClientSecret,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(provider, nameof(provider));
        Guard.AgainstNull(discovery, nameof(discovery));
        Guard.AgainstNullOrWhiteSpace(refreshToken, nameof(refreshToken));

        if (string.IsNullOrWhiteSpace(discovery.TokenEndpoint))
            throw new InvalidOperationException("The discovery document has no token_endpoint.");

        var requestStep = new FlowStep
        {
            StepNumber = 1,
            Title = "Client calls the token endpoint",
            Explanation = "AevumLux exchanges its refresh token for a new access token (and often a new refresh token) without involving the user or the browser again. This is how a long-lived session stays alive past the access token's short expiry.",
            Status = FlowStepStatus.InProgress,
            StartedAt = DateTime.UtcNow,
        };
        yield return requestStep;

        var formValues = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = provider.ClientId,
        };

        if (!string.IsNullOrWhiteSpace(rawClientSecret))
            formValues["client_secret"] = rawClientSecret;

        var parameters = new List<ParameterExplanation>
        {
            Explain("grant_type_refresh_token", "refresh_token", "grant_type"),
            Explain("refresh_token", refreshToken),
            Explain("client_id", provider.ClientId),
        };
        if (!string.IsNullOrWhiteSpace(rawClientSecret))
            parameters.Add(Explain("client_secret", rawClientSecret));

        await ExecuteTokenRequestAsync(requestStep, discovery.TokenEndpoint!, formValues, cancellationToken, parameters);
        var response = requestStep.Response;
        var status = requestStep.Status;
        var error = requestStep.Error;
        var completedAt = requestStep.CompletedAt;

        requestStep.Response = null;
        requestStep.Status = FlowStepStatus.Success;
        requestStep.Error = null;
        requestStep.ResponseExplanation = null;
        yield return requestStep;

        var responseStep = new FlowStep
        {
            StepNumber = 2,
            Title = "Identity provider validates and issues new tokens",
            Explanation = "The server checks that the refresh token is still valid and hasn't already been used (refresh tokens are typically single-use — using one invalidates it, and the response includes a new one to use next time). If valid, it issues a fresh access token.",
            Status = status,
            StartedAt = completedAt ?? DateTime.UtcNow,
            CompletedAt = completedAt,
            Response = response,
            Error = error,
            ResponseExplanation = status == FlowStepStatus.Success ? "The token endpoint returned a valid token response." : null,
        };
        AnnotateTokenResponseParameters(responseStep.Response);
        yield return responseStep;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<FlowStep> SimulateImplicitAsync(
        OidcProvider provider,
        DiscoveryDocument discovery,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(provider, nameof(provider));
        Guard.AgainstNull(discovery, nameof(discovery));

        if (string.IsNullOrWhiteSpace(discovery.AuthorizationEndpoint))
            throw new InvalidOperationException("The discovery document has no authorization_endpoint.");

        var state = GenerateRandomUrlSafeString(16);
        var step = 0;

        // Step 1: client sends the authorize request.
        var authorizeRequestStep = new FlowStep
        {
            StepNumber = ++step,
            Title = "Client calls the authorize endpoint",
            Explanation = "AevumLux opens the authorization endpoint with response_type=token in a browser popup. Instead of getting back a short-lived code to exchange, the server will put the access token directly in the redirect URL's fragment.",
            Status = FlowStepStatus.InProgress,
            StartedAt = DateTime.UtcNow,
        };
        yield return authorizeRequestStep;

        var authorizeUrl = BuildImplicitAuthorizeUrl(discovery.AuthorizationEndpoint!, provider, state);
        authorizeRequestStep.Request = new HttpRequestDetail
        {
            Method = "GET",
            Url = authorizeUrl.ToString(),
            Parameters =
            [
                Explain("response_type_token", "token", "response_type"),
                Explain("client_id", provider.ClientId),
                Explain("redirect_uri", provider.RedirectUri),
                Explain("scope", provider.Scopes),
                Explain("state", state),
            ],
        };

        // Started here, before step 1 is finalized, so an outright rejection of this GET (e.g.
        // redirect_uri mismatch) — which the server can return before any login page ever shows
        // — is caught as step 1's own failure instead of a later step assuming success.
        var loginSubmissionChannel = Channel.CreateBounded<HttpRequestDetail>(1);

        var redirectTask = _redirectHandler.CaptureRedirectAsync(
            authorizeUrl,
            provider.RedirectUri,
            onLoginSubmitted: request => loginSubmissionChannel.Writer.TryWrite(request),
            cancellationToken);

        var earlyRejectionCheck = await Task.WhenAny(redirectTask, Task.Delay(TimeSpan.FromSeconds(3), cancellationToken));
        if (earlyRejectionCheck == redirectTask && redirectTask.IsFaulted
            && redirectTask.Exception?.InnerException is AuthorizeRequestRejectedException rejection)
        {
            authorizeRequestStep.Status = FlowStepStatus.Failed;
            authorizeRequestStep.CompletedAt = DateTime.UtcNow;
            authorizeRequestStep.Response = new HttpResponseDetail
            {
                StatusCode = rejection.StatusCode,
                Body = rejection.ResponseBody,
            };
            authorizeRequestStep.Error = new FlowError
            {
                ErrorCode = "authorize_request_rejected",
                RawResponse = rejection.ResponseBody,
                PlainEnglishExplanation = $"The server rejected the authorize request outright (HTTP {rejection.StatusCode}), before showing any login page.",
                LikelyCauses = ["redirect_uri doesn't exactly match what's registered for this client on the server", "Client not permitted to use this grant type", "Requested scope not allowed for this client"],
                ActionableFix = "Check the provider's client registration on the server against the values used here — most commonly a redirect_uri mismatch.",
            };
            yield return authorizeRequestStep;
            yield break;
        }

        authorizeRequestStep.Status = FlowStepStatus.Success;
        authorizeRequestStep.CompletedAt = DateTime.UtcNow;
        yield return authorizeRequestStep;

        // Step 2: the IdP responds with the login page.
        var loginPageStep = new FlowStep
        {
            StepNumber = ++step,
            Title = "Identity provider responds with the login page",
            Status = FlowStepStatus.Success,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Response = new HttpResponseDetail { StatusCode = 200, Body = "(HTML login page — not itself an OAuth response, no fields to break down)" },
            ResponseExplanation = "Same as Authorization Code — the server shows a login page and waits for the user to sign in, from this same /connect/authorize URL.",
        };
        yield return loginPageStep;

        // Step 3: the user types credentials; the client's popup submits them.
        var credentialSubmitStep = new FlowStep
        {
            StepNumber = ++step,
            Title = "User types credentials, client submits them",
            Explanation = "The person signing in types a username and password into the identity provider's login page and submits the form — a real POST, captured as it happens.",
            Status = FlowStepStatus.InProgress,
            StartedAt = DateTime.UtcNow,
        };
        yield return credentialSubmitStep;

        var loginSubmittedTask = loginSubmissionChannel.Reader.ReadAsync(cancellationToken).AsTask();

        var firstCompleted = await Task.WhenAny(redirectTask, loginSubmittedTask);
        if (firstCompleted == loginSubmittedTask && loginSubmittedTask.IsCompletedSuccessfully)
        {
            credentialSubmitStep.Request = loginSubmittedTask.Result;
            AnnotateLoginSubmissionParameters(credentialSubmitStep.Request);
        }

        Uri? redirectResult = null;
        Exception? redirectException = null;
        try
        {
            redirectResult = await redirectTask;
        }
        catch (Exception ex)
        {
            redirectException = ex;
        }

        if (credentialSubmitStep.Request is null && loginSubmissionChannel.Reader.TryRead(out var loginRequest))
        {
            credentialSubmitStep.Request = loginRequest;
            AnnotateLoginSubmissionParameters(credentialSubmitStep.Request);
        }

        // Fallback if the WebView2 capture is missed — never leave this step's card blank.
        credentialSubmitStep.Request ??= new HttpRequestDetail
        {
            Method = "POST",
            Url = authorizeUrl.ToString(),
            Body = "(submitted from the login page — not captured; the redirect that followed is what proves this succeeded)",
        };

        if (redirectException is not null || redirectResult is null)
        {
            credentialSubmitStep.Status = FlowStepStatus.Failed;
            credentialSubmitStep.CompletedAt = DateTime.UtcNow;
            credentialSubmitStep.Error = new FlowError
            {
                ErrorCode = "redirect_capture_failed",
                RawResponse = redirectException?.Message ?? string.Empty,
                PlainEnglishExplanation = "The browser popup was closed, timed out, or never reached the redirect URI.",
                LikelyCauses = ["The user closed the popup before finishing", "The redirect URI is misconfigured on the server"],
                ActionableFix = "Try again, and check that the redirect URI registered on the server matches this provider's Redirect URI exactly.",
            };
            yield return credentialSubmitStep;
            yield break;
        }

        credentialSubmitStep.Status = FlowStepStatus.Success;
        credentialSubmitStep.CompletedAt = DateTime.UtcNow;
        yield return credentialSubmitStep;

        // Step 4: the IdP validates credentials and redirects with the access token in the fragment.
        // The access token is in the URL FRAGMENT (after #), which browsers never send to the
        // server — so it has to be parsed client-side from the captured redirect URL itself,
        // unlike Authorization Code's query-string parameters.
        var fragment = redirectResult.Fragment.TrimStart('#');
        var fragmentParams = HttpUtility.ParseQueryString(fragment);
        var accessToken = fragmentParams["access_token"];
        var returnedState = fragmentParams["state"];
        var error = fragmentParams["error"];

        var authRedirectStep = new FlowStep
        {
            StepNumber = ++step,
            Title = "Identity provider validates and redirects with the token",
            Explanation = "The server checks the credentials. If valid, it redirects back with the access token attached directly to the URL fragment — no code, no separate exchange step.",
            Status = FlowStepStatus.InProgress,
            StartedAt = DateTime.UtcNow,
            DeprecationWarning =
                "DEPRECATED — removed in OAuth 2.1. The access token below came back as part of the redirect URL " +
                "(after the '#'), not through a server-to-server exchange. That means it can end up in browser " +
                "history, in Referer headers if the page then loads external resources, and in any logging that " +
                "captures full URLs — none of which ever see a token in Authorization Code + PKCE, because that " +
                "flow's token only ever travels in a POST body straight from the app to the token endpoint. There's " +
                "also no client authentication step here at all. Real IdPs now default this off (some, like this " +
                "test server's underlying library, technically still support it — the fact that it needs deliberate " +
                "opt-in per client, as it does here, is itself a sign of where the industry has moved). Use " +
                "Authorization Code + PKCE instead; it covers every case Implicit did.",
            Response = new HttpResponseDetail
            {
                StatusCode = 302,
                ReasonPhrase = "Found",
                Body = redirectResult.ToString(),
                Parameters = string.IsNullOrEmpty(accessToken)
                    ? []
                    :
                    [
                        Explain("access_token_fragment", accessToken, "access_token"),
                        Explain("token_type", fragmentParams["token_type"] ?? "Bearer"),
                        Explain("expires_in", fragmentParams["expires_in"] ?? string.Empty),
                        Explain("state_echoed", returnedState ?? string.Empty, "state"),
                    ],
            },
        };
        yield return authRedirectStep;

        if (error is not null)
        {
            authRedirectStep.Status = FlowStepStatus.Failed;
            authRedirectStep.CompletedAt = DateTime.UtcNow;
            authRedirectStep.Error = new FlowError
            {
                ErrorCode = error,
                RawResponse = redirectResult.Fragment,
                PlainEnglishExplanation = $"The authorization server returned an error: {fragmentParams["error_description"] ?? error}",
                LikelyCauses = ["Redirect URI mismatch", "Client not permitted to use this grant type"],
                ActionableFix = "Check the provider's client registration on the server against the values used here.",
            };
            yield return authRedirectStep;
            yield break;
        }

        if (returnedState != state || string.IsNullOrEmpty(accessToken))
        {
            authRedirectStep.Status = FlowStepStatus.Failed;
            authRedirectStep.CompletedAt = DateTime.UtcNow;
            authRedirectStep.Error = new FlowError
            {
                ErrorCode = string.IsNullOrEmpty(accessToken) ? "missing_token" : "state_mismatch",
                RawResponse = redirectResult.Fragment,
                PlainEnglishExplanation = string.IsNullOrEmpty(accessToken)
                    ? "The redirect completed without an access token or an error."
                    : "The 'state' value returned doesn't match what AevumLux sent.",
                LikelyCauses = ["Unexpected server response"],
                ActionableFix = "Check the server logs for the test IdentityServer.",
            };
            yield return authRedirectStep;
            yield break;
        }

        authRedirectStep.Status = FlowStepStatus.Success;
        authRedirectStep.CompletedAt = DateTime.UtcNow;
        authRedirectStep.ResponseExplanation = "The browser was redirected back with the access token sitting in the URL fragment — visible to this step, but also to anything else with access to that URL.";
        yield return authRedirectStep;

        // Step 5: the client's popup notices the redirect and reads the token out of it.
        var captureStep = new FlowStep
        {
            StepNumber = ++step,
            Title = "Client's popup captures the token",
            Explanation = "AevumLux's popup recognizes the navigation to its own redirect_uri, stops the browser from loading it, and reads the access token straight out of the URL fragment. Entirely local — no network call, and no separate exchange step exists in this flow.",
            Status = FlowStepStatus.Success,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            ResponseExplanation = "The popup closes. AevumLux now holds the access token directly.",
        };
        yield return captureStep;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<FlowStep> SimulateResourceOwnerPasswordAsync(
        OidcProvider provider,
        DiscoveryDocument discovery,
        string username,
        string password,
        string rawClientSecret,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(provider, nameof(provider));
        Guard.AgainstNull(discovery, nameof(discovery));

        if (string.IsNullOrWhiteSpace(discovery.TokenEndpoint))
            throw new InvalidOperationException("The discovery document has no token_endpoint.");

        var requestStep = new FlowStep
        {
            StepNumber = 1,
            Title = "Client calls the token endpoint with raw credentials",
            Explanation = "AevumLux collects the user's username and password itself — in its own UI, not the identity provider's — and posts them directly to the token endpoint.",
            Status = FlowStepStatus.InProgress,
            StartedAt = DateTime.UtcNow,
            DeprecationWarning =
                "DEPRECATED — dropped in OAuth 2.1. Look at the request body below: it contains the raw " +
                "'username' and 'password' fields. This app (not the identity provider) is the thing that saw " +
                "the user's actual password, which defeats the entire point of delegated authorization — the " +
                "IdP exists so client apps never need to touch credentials. Concretely, this breaks multi-factor " +
                "authentication and federated/SSO login (there's no place for a second factor or a redirect to " +
                "a corporate IdP in a single password field), and it trains users to type their password into " +
                "any app that asks, which is exactly the muscle memory phishing relies on. Authorization Code + " +
                "PKCE is the replacement: the user authenticates on the identity provider's own page, which the " +
                "client app never sees the contents of.",
        };
        yield return requestStep;

        var formValues = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = username,
            ["password"] = password,
            ["client_id"] = provider.ClientId,
        };

        if (!string.IsNullOrWhiteSpace(rawClientSecret))
            formValues["client_secret"] = rawClientSecret;
        if (!string.IsNullOrWhiteSpace(provider.Scopes))
            formValues["scope"] = provider.Scopes;

        var parameters = new List<ParameterExplanation>
        {
            Explain("grant_type_password", "password", "grant_type"),
            Explain("username_ropc", username, "username"),
            Explain("password_ropc", "••••••••", "password"),
            Explain("client_id", provider.ClientId),
        };
        if (!string.IsNullOrWhiteSpace(rawClientSecret))
            parameters.Add(Explain("client_secret", rawClientSecret));
        if (!string.IsNullOrWhiteSpace(provider.Scopes))
            parameters.Add(Explain("scope", provider.Scopes));

        await ExecuteTokenRequestAsync(requestStep, discovery.TokenEndpoint!, formValues, cancellationToken, parameters);
        var response = requestStep.Response;
        var status = requestStep.Status;
        var error = requestStep.Error;
        var completedAt = requestStep.CompletedAt;

        requestStep.Response = null;
        requestStep.Status = FlowStepStatus.Success;
        requestStep.Error = null;
        requestStep.ResponseExplanation = null;
        yield return requestStep;

        var responseStep = new FlowStep
        {
            StepNumber = 2,
            Title = "Identity provider validates the credentials and issues tokens",
            Explanation = "The server checks the username/password directly (no separate login page was ever shown) and, if valid, issues an access token — same trust boundary violation either way: the IdP has to just take AevumLux's word for what the user actually typed.",
            Status = status,
            StartedAt = completedAt ?? DateTime.UtcNow,
            CompletedAt = completedAt,
            Response = response,
            Error = error,
            ResponseExplanation = status == FlowStepStatus.Success ? "The token endpoint returned a valid token response." : null,
        };
        AnnotateTokenResponseParameters(responseStep.Response);
        yield return responseStep;
    }

    private static Uri BuildImplicitAuthorizeUrl(string authorizationEndpoint, OidcProvider provider, string state)
    {
        var uriBuilder = new UriBuilder(authorizationEndpoint);
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);
        query["response_type"] = "token";
        query["client_id"] = provider.ClientId;
        query["redirect_uri"] = provider.RedirectUri;
        query["scope"] = provider.Scopes;
        query["state"] = state;
        uriBuilder.Query = query.ToString();
        return uriBuilder.Uri;
    }

    private async Task ExecuteTokenRequestAsync(
        FlowStep step,
        string tokenEndpoint,
        Dictionary<string, string> formValues,
        CancellationToken cancellationToken,
        List<ParameterExplanation>? parameters = null)
    {
        step.Request = new HttpRequestDetail
        {
            Method = "POST",
            Url = tokenEndpoint,
            Headers = new Dictionary<string, string> { ["Content-Type"] = "application/x-www-form-urlencoded" },
            Body = string.Join("&", formValues.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}")),
            Parameters = parameters ?? [],
        };

        try
        {
            using var content = new FormUrlEncodedContent(formValues);
            using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint) { Content = content };
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);

            step.Response = new HttpResponseDetail
            {
                StatusCode = (int)response.StatusCode,
                ReasonPhrase = response.ReasonPhrase ?? string.Empty,
                Body = rawBody,
            };
            step.CompletedAt = DateTime.UtcNow;

            if (response.IsSuccessStatusCode)
            {
                step.Status = FlowStepStatus.Success;
                step.ResponseExplanation = "The token endpoint returned a valid token response.";
            }
            else
            {
                step.Status = FlowStepStatus.Failed;
                step.Error = ParseOAuthError(rawBody);
            }
        }
        catch (HttpRequestException ex)
        {
            step.Status = FlowStepStatus.Failed;
            step.CompletedAt = DateTime.UtcNow;
            step.Error = new FlowError
            {
                ErrorCode = "connection_failed",
                RawResponse = ex.Message,
                PlainEnglishExplanation = "Could not reach the token endpoint.",
                LikelyCauses = ["The test IdentityServer isn't running", "The token endpoint URL is wrong"],
                ActionableFix = "Check the URL and that the server is running.",
            };
        }
    }

    private static FlowError ParseOAuthError(string rawBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            var errorCode = root.TryGetProperty("error", out var e) ? e.GetString() ?? "unknown_error" : "unknown_error";
            var description = root.TryGetProperty("error_description", out var d) ? d.GetString() ?? string.Empty : string.Empty;

            return new FlowError
            {
                ErrorCode = errorCode,
                RawResponse = rawBody,
                PlainEnglishExplanation = string.IsNullOrEmpty(description) ? $"The server rejected the request: {errorCode}" : description,
                LikelyCauses = errorCode switch
                {
                    "invalid_client" => ["Wrong client ID or secret", "Client not registered for this grant type"],
                    "invalid_grant" => ["Authorization code or refresh token already used, expired, or invalid", "PKCE code_verifier doesn't match the original code_challenge"],
                    "invalid_scope" => ["Requested scope isn't allowed for this client"],
                    "unsupported_grant_type" => ["This client isn't permitted to use this grant type"],
                    _ => ["See error_description for detail"],
                },
                ActionableFix = "Check the provider's client registration on the server against the values used here.",
            };
        }
        catch (JsonException)
        {
            return new FlowError
            {
                ErrorCode = "unparseable_error",
                RawResponse = rawBody,
                PlainEnglishExplanation = "The server returned an error response that wasn't valid JSON.",
                LikelyCauses = [],
                ActionableFix = "Check the raw response body.",
            };
        }
    }

    private static Uri BuildAuthorizeUrl(string authorizationEndpoint, OidcProvider provider, string state, string codeChallenge)
    {
        var uriBuilder = new UriBuilder(authorizationEndpoint);
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);
        query["response_type"] = "code";
        query["client_id"] = provider.ClientId;
        query["redirect_uri"] = provider.RedirectUri;
        query["scope"] = provider.Scopes;
        query["state"] = state;
        query["code_challenge"] = codeChallenge;
        query["code_challenge_method"] = "S256";
        uriBuilder.Query = query.ToString();
        return uriBuilder.Uri;
    }

    private static (string Verifier, string Challenge) GeneratePkcePair()
    {
        var verifier = GenerateRandomUrlSafeString(64);
        var challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        var challenge = Base64UrlEncode(challengeBytes);
        return (verifier, challenge);
    }

    private static string GenerateRandomUrlSafeString(int byteLength)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
