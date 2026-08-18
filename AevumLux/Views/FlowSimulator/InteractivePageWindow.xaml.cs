using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;

namespace AevumLux.Views.FlowSimulator;

/// <summary>
/// A minimal popup window hosting a WebView2 control, used for steps that need the user to
/// see and interact with a real page but have no redirect_uri to capture — e.g. Device Code's
/// verification page. Watches for the verification GET (the one carrying user_code, issued
/// after the login form's POST redirects to it) succeeding with a 200, and closes itself the
/// moment that happens — otherwise the window just sits there indefinitely after a successful
/// sign-in, since /connect/verify's success response is an empty 200 with nothing else to
/// navigate to or watch for. The caller awaits <see cref="Window.Closed"/>; there is no result
/// to extract here, unlike <see cref="AuthorizePopupWindow"/>.
/// </summary>
public sealed partial class InteractivePageWindow : Window
{
    public InteractivePageWindow(Uri url)
    {
        InitializeComponent();

        Title = "AevumLux Flow Simulator";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(480, 640));

        _ = InitializeBrowserAsync(url);
    }

    private async Task InitializeBrowserAsync(Uri url)
    {
        await Browser.EnsureCoreWebView2Async();

        var verificationPath = url.GetLeftPart(UriPartial.Path);
        var dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("No DispatcherQueue for the current thread — InteractivePageWindow must be constructed on a UI thread.");

        Browser.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        Browser.CoreWebView2.WebResourceResponseReceived += (_, args) =>
        {
            // Only the GET carrying user_code (the one following the login form's POST
            // redirect) can actually succeed — the initial GET has no user_code yet, and the
            // login POST itself redirects rather than returning 200 directly. A 200 here means
            // OpenIddict's Results.SignIn call succeeded: the code was approved.
            if (args.Request.Method != "GET"
                || !args.Request.Uri.StartsWith(verificationPath, StringComparison.OrdinalIgnoreCase)
                || !args.Request.Uri.Contains("user_code=", StringComparison.OrdinalIgnoreCase)
                || args.Response.StatusCode != 200)
            {
                return;
            }

            // Deferred to the UI thread's next dispatch cycle, same reasoning as
            // AuthorizePopupWindow: closing the Window (tearing down CoreWebView2) from inside
            // a WebResourceResponseReceived callback while WebView2 is still processing that
            // same response is unsafe reentrancy.
            dispatcherQueue.TryEnqueue(Close);
        };

        Browser.CoreWebView2.Navigate(url.ToString());
    }
}
