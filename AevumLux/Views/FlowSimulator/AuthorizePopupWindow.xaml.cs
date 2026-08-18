using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using AevumLux.Core.Services.Interfaces;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using Windows.Storage.Streams;

namespace AevumLux.Views.FlowSimulator;

/// <summary>
/// A minimal popup window hosting a WebView2 control, used to drive the user through an
/// authorization_code flow's browser step. Watches outgoing navigations for one that starts
/// with the client's redirect URI, captures the full URL (including the code/state/error query
/// params the server appended), then closes itself. Also captures the login page's own POST
/// (username/password submission) as it happens, so Flow Simulator can show it as a real step —
/// not just the final redirect — instead of collapsing "open browser" and "user signs in" into
/// one invisible step.
/// </summary>
public sealed partial class AuthorizePopupWindow : Window
{
    private readonly string _redirectUri;
    private readonly TaskCompletionSource<Uri> _redirectCaptured = new();
    private readonly TaskCompletionSource<(string Method, string Url, string? Body)> _loginSubmissionCaptured = new();

    public Task<Uri> RedirectCaptured => _redirectCaptured.Task;

    /// <summary>
    /// Resolves with the login page's own POST (method, URL, form body) the moment the user
    /// submits it — separate from <see cref="RedirectCaptured"/>, which only resolves once the
    /// server redirects back to the client's redirect_uri afterward.
    /// </summary>
    public Task<(string Method, string Url, string? Body)> LoginSubmissionCaptured => _loginSubmissionCaptured.Task;

    public AuthorizePopupWindow(Uri authorizeUrl, string redirectUri)
    {
        _redirectUri = redirectUri;
        InitializeComponent();

        Title = "Sign in — AevumLux Flow Simulator";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(480, 640));

        Closed += (_, _) =>
        {
            _redirectCaptured.TrySetException(new OperationCanceledException("The authorization popup was closed before completing."));
            _loginSubmissionCaptured.TrySetCanceled();
        };

        _ = InitializeBrowserAsync(authorizeUrl);
    }

    private async Task InitializeBrowserAsync(Uri authorizeUrl)
    {
        await Browser.EnsureCoreWebView2Async();

        // WebResourceRequested sees the raw outgoing request (including the POST body a form
        // submit sends) before it leaves the browser — NavigationStarting alone only exposes
        // the target URL, not what a form actually posted. Filtered on All (not just Document)
        // since a form POST's navigation isn't always classified as Document by WebView2, and
        // missing it here means the login step falls back to a placeholder instead of the real
        // captured request/credentials.
        var authorizePathPrefix = authorizeUrl.GetLeftPart(UriPartial.Path);
        Browser.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        Browser.CoreWebView2.WebResourceRequested += (_, args) =>
        {
            if (args.Request.Method == "POST"
                && args.Request.Uri.StartsWith(authorizePathPrefix, StringComparison.OrdinalIgnoreCase)
                && !_loginSubmissionCaptured.Task.IsCompleted)
            {
                string? body = null;
                if (args.Request.Content is IRandomAccessStream stream)
                {
                    // Read via a .NET Stream wrapper without disposing the underlying
                    // IRandomAccessStream, then seek it back to 0 so the real request still
                    // goes out to the server with its body intact.
                    using var netStream = stream.AsStreamForRead();
                    using var reader = new StreamReader(netStream, leaveOpen: true);
                    body = reader.ReadToEnd();
                    stream.Seek(0);
                }

                _loginSubmissionCaptured.TrySetResult((args.Request.Method, args.Request.Uri, body));
            }
        };

        Browser.CoreWebView2.NavigationStarting += (_, args) =>
        {
            if (args.Uri.StartsWith(_redirectUri, StringComparison.OrdinalIgnoreCase))
            {
                args.Cancel = true;
                _redirectCaptured.TrySetResult(new Uri(args.Uri));
                Close();
            }
        };

        // The server can reject the request outright (e.g. a redirect_uri mismatch) before it
        // ever gets to showing a login page or issuing a real OAuth redirect — in that case the
        // popup just displays the raw error JSON and neither NavigationStarting's redirect-URI
        // check nor the login-submission capture ever fires, leaving RedirectCaptured's task
        // pending forever (and Flow Simulator stuck showing stale data from a previous run).
        // Watching the top-level document's actual HTTP status on the very first request to the
        // authorize URL is what catches that case — a distinct exception type from "popup closed
        // early" or "redirect never came" so the service layer can tell this apart and report it
        // as a rejection of the initial GET, not a failed login step.
        //
        // Only the status code is read here (synchronously — args.Response is only valid for
        // the duration of this callback without taking a deferral, and the status code is all
        // this check needs). Setting the TCS result and closing the window are deferred to the
        // UI thread's next dispatch cycle via DispatcherQueue rather than done inline: closing
        // the Window — which tears down CoreWebView2 — from inside a WebResourceResponseReceived
        // callback while WebView2's native code is still actively processing that same response
        // is unsafe reentrancy and was observed to crash with AccessViolationException.
        var checkedFirstResponse = false;
        var dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("No DispatcherQueue for the current thread — AuthorizePopupWindow must be constructed on a UI thread.");
        Browser.CoreWebView2.WebResourceResponseReceived += (_, args) =>
        {
            // Prefix match, not exact equality — WebView2 can report the request URL with
            // different query-parameter encoding/ordering than authorizeUrl.ToString() even
            // though it's logically the same request, which would otherwise silently make this
            // check never fire for any response and leave RedirectCaptured pending forever.
            if (checkedFirstResponse
                || !args.Request.Uri.StartsWith(authorizePathPrefix, StringComparison.OrdinalIgnoreCase)
                || args.Request.Method != "GET"
                || _redirectCaptured.Task.IsCompleted)
            {
                return;
            }

            checkedFirstResponse = true;
            if (args.Response.StatusCode < 400)
                return;

            var statusCode = (int)args.Response.StatusCode;
            dispatcherQueue.TryEnqueue(() =>
            {
                _redirectCaptured.TrySetException(new AuthorizeRequestRejectedException(statusCode, string.Empty));
                Close();
            });
        };

        Browser.CoreWebView2.Navigate(authorizeUrl.ToString());
    }
}
