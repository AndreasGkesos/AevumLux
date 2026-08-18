namespace AevumLux.Core.Services.Interfaces;

/// <summary>Current lifecycle state of the managed Test IdP process.</summary>
public enum TestIdpStatus
{
    /// <summary>The IdP executable has not been published yet and no publish is in progress.</summary>
    NotFound,

    /// <summary>The IdP executable is missing and is currently being published.</summary>
    Publishing,

    /// <summary>Publishing the IdP failed. Simulation scenarios are unavailable.</summary>
    PublishFailed,

    /// <summary>The IdP executable exists but is not currently running.</summary>
    Stopped,

    /// <summary>The IdP process has been started and is starting up.</summary>
    Starting,

    /// <summary>The IdP process is running and serving requests.</summary>
    Running,

    /// <summary>The IdP process is being stopped by the app.</summary>
    Stopping,

    /// <summary>The IdP process exited unexpectedly while it was supposed to be running.</summary>
    Failed,
}

/// <summary>
/// Manages the bundled AevumLux.TestIdentityServer as a child process: locating its published
/// executable relative to this app's own executable, publishing it on first run in development,
/// and starting/stopping/monitoring it so Flow Simulator has something to call.
/// </summary>
public interface ITestIdpProcessService : IDisposable
{
    /// <summary>Current lifecycle state.</summary>
    TestIdpStatus Status { get; }

    /// <summary>The local URL the IdP listens on once running (e.g. http://localhost:7087).</summary>
    string LocalUrl { get; }

    /// <summary>Full path to the IdP executable this service resolves to, whether or not it exists yet.</summary>
    string ExecutablePath { get; }

    /// <summary>Raised whenever <see cref="Status"/> changes.</summary>
    event EventHandler<TestIdpStatus>? StatusChanged;

    /// <summary>Raised for each line of stdout/stderr produced by the IdP process.</summary>
    event EventHandler<string>? LogLineReceived;

    /// <summary>
    /// Checks whether the IdP executable already exists at <see cref="ExecutablePath"/>. If not,
    /// attempts to publish it there (development scenario only — in an installed build the
    /// executable is expected to already be bundled by the installer, so this becomes a no-op).
    /// Safe to call on every app startup; does nothing if the executable is already present.
    /// </summary>
    Task EnsurePublishedAsync();

    /// <summary>Starts the IdP process. No-op if already running or starting.</summary>
    Task StartAsync();

    /// <summary>Stops the IdP process by killing its process tree. No-op if not running.</summary>
    void Stop();
}
