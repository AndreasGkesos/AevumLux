using System.Diagnostics;
using AevumLux.Core.Services.Interfaces;

namespace AevumLux.Core.Services.Implementations;

/// <summary>
/// Manages AevumLux.TestIdentityServer as a child process. The published executable always
/// lives at "TestIdp\AevumLux.TestIdentityServer.exe" relative to this app's own executable
/// (<see cref="AppContext.BaseDirectory"/>) — the same relative layout in development (where
/// this service publishes it there on first run) and in an installed build (where the WiX
/// installer bundles it there ahead of time). The app never writes to this folder except during
/// that one-time development publish; an installed build only ever reads and executes it.
/// </summary>
public sealed class TestIdpProcessService : ITestIdpProcessService
{
    private const string LocalUrlValue = "http://localhost:7087";
    private const string SourceProjectRelativePath = @"..\..\..\..\..\AevumLux.TestIdentityServer\AevumLux.TestIdentityServer.csproj";

    private readonly object _lock = new();
    private Process? _process;
    private TestIdpStatus _status;

    public TestIdpProcessService()
    {
        var testIdpDir = Path.Combine(AppContext.BaseDirectory, "TestIdp");
        ExecutablePath = Path.Combine(testIdpDir, "AevumLux.TestIdentityServer.exe");
        _status = File.Exists(ExecutablePath) ? TestIdpStatus.Stopped : TestIdpStatus.NotFound;
    }

    public TestIdpStatus Status
    {
        get { lock (_lock) return _status; }
        private set
        {
            lock (_lock)
            {
                if (_status == value)
                    return;
                _status = value;
            }
            StatusChanged?.Invoke(this, value);
        }
    }

    public string LocalUrl => LocalUrlValue;

    public string ExecutablePath { get; }

    public event EventHandler<TestIdpStatus>? StatusChanged;

    public event EventHandler<string>? LogLineReceived;

    public async Task EnsurePublishedAsync()
    {
        if (File.Exists(ExecutablePath))
        {
            Status = TestIdpStatus.Stopped;
            return;
        }

        var sourceProject = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, SourceProjectRelativePath));
        if (!File.Exists(sourceProject))
        {
            // Installed build with no bundled IdP and no source project to publish from.
            Status = TestIdpStatus.NotFound;
            return;
        }

        Status = TestIdpStatus.Publishing;

        var testIdpDir = Path.Combine(AppContext.BaseDirectory, "TestIdp");
        var publishArgs = $"publish \"{sourceProject}\" -c Release -r win-x64 --self-contained true -o \"{testIdpDir}\"";

        try
        {
            var startInfo = new ProcessStartInfo("dotnet", publishArgs)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var publishProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            publishProcess.OutputDataReceived += (_, e) => { if (e.Data is not null) LogLineReceived?.Invoke(this, e.Data); };
            publishProcess.ErrorDataReceived += (_, e) => { if (e.Data is not null) LogLineReceived?.Invoke(this, e.Data); };

            publishProcess.Start();
            publishProcess.BeginOutputReadLine();
            publishProcess.BeginErrorReadLine();
            await publishProcess.WaitForExitAsync();

            Status = publishProcess.ExitCode == 0 && File.Exists(ExecutablePath)
                ? TestIdpStatus.Stopped
                : TestIdpStatus.PublishFailed;
        }
        catch
        {
            Status = TestIdpStatus.PublishFailed;
        }
    }

    public Task StartAsync()
    {
        lock (_lock)
        {
            if (_status is TestIdpStatus.Running or TestIdpStatus.Starting)
                return Task.CompletedTask;

            if (!File.Exists(ExecutablePath))
            {
                Status = TestIdpStatus.NotFound;
                return Task.CompletedTask;
            }
        }

        Status = TestIdpStatus.Starting;

        var startInfo = new ProcessStartInfo(ExecutablePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(ExecutablePath)!,
        };
        startInfo.EnvironmentVariables["ASPNETCORE_URLS"] = LocalUrlValue;

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) LogLineReceived?.Invoke(this, e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) LogLineReceived?.Invoke(this, e.Data); };
        process.Exited += (_, _) =>
        {
            lock (_lock)
            {
                if (_status is TestIdpStatus.Stopping)
                    return;
            }
            Status = TestIdpStatus.Failed;
        };

        lock (_lock)
        {
            _process = process;
        }

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        Status = TestIdpStatus.Running;
        return Task.CompletedTask;
    }

    public void Stop()
    {
        Process? process;
        lock (_lock)
        {
            if (_status is not (TestIdpStatus.Running or TestIdpStatus.Starting))
                return;

            process = _process;
            _status = TestIdpStatus.Stopping;
        }

        try
        {
            process?.Kill(entireProcessTree: true);
            process?.WaitForExit(5000);
        }
        catch
        {
            // Process may have already exited on its own.
        }
        finally
        {
            process?.Dispose();
            lock (_lock)
            {
                _process = null;
            }
            Status = TestIdpStatus.Stopped;
        }
    }

    public void Dispose() => Stop();
}
