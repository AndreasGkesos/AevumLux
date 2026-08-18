using System.Net;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace AevumLux.TestIdentityServer;

/// <summary>
/// Renders the static HTML login page shown by /connect/authorize and /connect/verify.
/// Not a real identity provider's login page — this test IdP has no real user store — but a
/// real form the user has to type into and submit, so the "user authenticates" step in
/// Authorization Code, Implicit and Device Code actually feels like a step instead of an
/// instant, invisible auto-sign-in.
/// </summary>
internal static class LoginPage
{
    public static string Render(string originalQueryString, string testUsername, string testPassword, bool invalidAttempt = false, bool requireUserCode = false)
    {
        var errorBanner = invalidAttempt
            ? "<div class=\"error\">Incorrect username, password, or code. Try the test credentials shown below.</div>"
            : string.Empty;

        // OpenIddict's authorization/verification endpoints read the OAuth request params from
        // the POST body on submission, not the query string — so every original query param has
        // to be carried forward as a hidden field, not just referenced in the form's action URL.
        // user_code is deliberately excluded here even if present in the query string (i.e. from
        // a verification_uri_complete link) — requireUserCode instead adds a real, empty input
        // for it, so the person has to type in the code Flow Simulator displayed, same as a real
        // device-code flow's manual entry step, instead of it silently riding along as a hidden field.
        var hiddenFields = new StringBuilder();
        foreach (var (key, values) in QueryHelpers.ParseQuery(originalQueryString))
        {
            if (requireUserCode && key == "user_code")
                continue;

            foreach (var value in values)
                hiddenFields.Append($"""<input type="hidden" name="{WebUtility.HtmlEncode(key)}" value="{WebUtility.HtmlEncode(value)}" />""");
        }

        var userCodeField = requireUserCode
            ? """<label for="user_code">Code shown in Flow Simulator's "User Verification" step</label><input type="text" id="user_code" name="user_code" autocomplete="off" style="text-transform: uppercase;" />"""
            : string.Empty;

        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8" />
                <title>Sign in — AevumLux Test IdentityServer</title>
                <style>
                    body { font-family: 'Segoe UI', sans-serif; background: #f3f3f3; display: flex; align-items: center; justify-content: center; height: 100vh; margin: 0; }
                    .card { background: white; border-radius: 8px; padding: 32px; width: 320px; box-shadow: 0 2px 8px rgba(0,0,0,0.12); }
                    h1 { font-size: 18px; margin: 0 0 4px; }
                    p.sub { color: #666; font-size: 13px; margin: 0 0 20px; }
                    label { display: block; font-size: 12px; color: #444; margin: 12px 0 4px; }
                    input { width: 100%; box-sizing: border-box; padding: 8px; border: 1px solid #ccc; border-radius: 4px; font-size: 14px; }
                    button { width: 100%; margin-top: 20px; padding: 10px; background: #0067c0; color: white; border: none; border-radius: 4px; font-size: 14px; cursor: pointer; }
                    button:hover { background: #005aa8; }
                    .hint { margin-top: 16px; padding: 10px; background: #f0f7ff; border-radius: 4px; font-size: 12px; color: #333; }
                    .error { margin-bottom: 16px; padding: 10px; background: #fde7e9; color: #a4262c; border-radius: 4px; font-size: 13px; }
                </style>
            </head>
            <body>
                <div class="card">
                    <h1>Sign in</h1>
                    <p class="sub">AevumLux Test IdentityServer — this is a test tool, not a real login.</p>
                    {{errorBanner}}
                    <form method="post">
                        {{hiddenFields}}
                        <label for="username">Username</label>
                        <input type="text" id="username" name="username" autocomplete="username" autofocus />
                        <label for="password">Password</label>
                        <input type="password" id="password" name="password" autocomplete="current-password" />
                        {{userCodeField}}
                        <button type="submit">Sign in</button>
                    </form>
                    <div class="hint">Test credentials for this scenario — see SCENARIOS.md:<br/><strong>{{WebUtility.HtmlEncode(testUsername)}}</strong> / <strong>{{WebUtility.HtmlEncode(testPassword)}}</strong></div>
                </div>
            </body>
            </html>
            """;
    }
}
