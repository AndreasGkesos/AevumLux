using AevumLux.Core.Models;

namespace AevumLux.Core.Services.Interfaces;

/// <summary>
/// Drives the user through an authorization_code flow's browser step and captures the
/// redirect back to the client's redirect URI. Implemented in the app layer (WebView2 popup)
/// since Core has no UI framework dependency — the flow simulator service calls this to get
/// from "here is the authorize URL" to "here is the code/state/error that came back".
/// </summary>
public interface IAuthorizationRedirectHandler
{
    /// <summary>
    /// Navigates to <paramref name="authorizeUrl"/> and waits for the browser to be redirected
    /// to a URL starting with <paramref name="redirectUri"/>, then returns that full redirect URL.
    /// If <paramref name="onLoginSubmitted"/> is given, it's invoked as soon as the login page's
    /// own POST (username/password submission) is observed, with the raw method/URL/body of
    /// that request — separate from, and before, the eventual redirect.
    /// </summary>
    Task<Uri> CaptureRedirectAsync(
        Uri authorizeUrl,
        string redirectUri,
        Action<HttpRequestDetail>? onLoginSubmitted = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens <paramref name="url"/> in a browser popup and waits for the user to close it —
    /// used for steps with no redirect_uri to capture (e.g. Device Code's verification page),
    /// where the point is just to make the user actually see and interact with a real page.
    /// </summary>
    Task ShowInteractivePageAsync(Uri url, CancellationToken cancellationToken = default);
}

/// <summary>
/// Thrown by <see cref="IAuthorizationRedirectHandler.CaptureRedirectAsync"/> when the server
/// rejects the very first request to the authorize endpoint outright (e.g. a redirect_uri
/// mismatch) — before any login page is shown or any real OAuth redirect happens. Distinct from
/// a generic capture failure (popup closed early, network issue) so the flow simulator can
/// report this as the initial authorize request itself failing, not a failed login step.
/// </summary>
public sealed class AuthorizeRequestRejectedException(int statusCode, string responseBody)
    : Exception($"The authorization server rejected the request outright (HTTP {statusCode}) before any login page or redirect.")
{
    public int StatusCode { get; } = statusCode;
    public string ResponseBody { get; } = responseBody;
}
