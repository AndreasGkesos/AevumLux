using AevumLux.Core.Models;
using AevumLux.Core.Services.Interfaces;
using AevumLux.Views.FlowSimulator;
using Microsoft.UI.Dispatching;

namespace AevumLux.Services;

/// <summary>
/// Drives the authorization_code browser step using a WebView2 popup window
/// (<see cref="AuthorizePopupWindow"/>). Must be constructed and used from the UI thread —
/// window creation requires it.
/// </summary>
public sealed class WebView2AuthorizationRedirectHandler : IAuthorizationRedirectHandler
{
    private readonly DispatcherQueue _dispatcherQueue;

    public WebView2AuthorizationRedirectHandler(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    public Task<Uri> CaptureRedirectAsync(
        Uri authorizeUrl,
        string redirectUri,
        Action<HttpRequestDetail>? onLoginSubmitted = null,
        CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<Uri>();

        _dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                var popup = new AuthorizePopupWindow(authorizeUrl, redirectUri);
                popup.Activate();

                if (onLoginSubmitted is not null)
                {
                    _ = popup.LoginSubmissionCaptured.ContinueWith(loginTask =>
                    {
                        if (loginTask.IsCompletedSuccessfully)
                        {
                            var (method, url, body) = loginTask.Result;
                            onLoginSubmitted(new HttpRequestDetail
                            {
                                Method = method,
                                Url = url,
                                Headers = new Dictionary<string, string> { ["Content-Type"] = "application/x-www-form-urlencoded" },
                                Body = body,
                            });
                        }
                    }, TaskScheduler.Default);
                }

                _ = popup.RedirectCaptured.ContinueWith(task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        tcs.TrySetResult(task.Result);
                    }
                    else
                    {
                        Exception exception = new InvalidOperationException("Authorization popup failed.");
                        if (task.Exception is not null)
                            exception = (Exception?)task.Exception.InnerException ?? task.Exception;
                        tcs.TrySetException(exception);
                    }
                }, TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

        return tcs.Task;
    }

    public Task ShowInteractivePageAsync(Uri url, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource();

        _dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                var popup = new InteractivePageWindow(url);
                popup.Activate();
                popup.Closed += (_, _) => tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

        return tcs.Task;
    }
}
