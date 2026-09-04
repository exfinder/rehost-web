namespace Rehost.WebForms.Hosting;

using System;
using System.IO;
using System.Threading;
using System.Web;
using System.Web.Hosting;

internal sealed class ClassicPipelineActivation
{
    private readonly WebFormsApplicationOptions _options;
    private readonly Lazy<ClassicPipelineDispatcher> _dispatcher;
    private ApplicationManager? _manager;
    private Action? _restartRequested;
    private int _shutdown;

    internal ClassicPipelineActivation(WebFormsApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _dispatcher = new Lazy<ClassicPipelineDispatcher>(
            Activate,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    internal string ApplicationId => _options.ApplicationId;

    internal string VirtualRootPath => _options.VirtualRootPath;

    internal string PhysicalRootPath => _options.PhysicalRootPath;

    // Activation is deferred to the first request so that request observes a cold application:
    // activating at startup would make every served request a warm one.
    internal ClassicPipelineDispatcher Dispatcher => _dispatcher.Value;

    internal static string TemporaryDirectory()
    {
        return HttpRuntime.CodegenDir ?? Path.GetTempPath();
    }

    internal void RegisterRestartRequested(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        _restartRequested = callback;
    }

    internal void Shutdown()
    {
        if (Interlocked.Exchange(ref _shutdown, 1) != 0 || !_dispatcher.IsValueCreated)
        {
            return;
        }

        var manager = _manager;
        if (manager == null)
        {
            return;
        }

        HostingEnvironment.RaiseStopListening();
        manager.StopObject(_options.ApplicationId, typeof(ClassicPipelineDispatcher));
        manager.ShutdownApplication(_options.ApplicationId);
        manager.Close();
    }

    private ClassicPipelineDispatcher Activate()
    {
        var manager = ApplicationManager.GetApplicationManager();
        manager.Open();

        var registered = manager.CreateObject(
            _options.ApplicationId,
            typeof(ClassicPipelineDispatcher),
            _options.VirtualRootPath,
            EnsureTrailingSeparator(_options.PhysicalRootPath),
            failIfExists: true,
            // Framework's default. An initialization failure, including a compile error in
            // application code, is stashed and rendered on every request instead of aborting
            // activation, so the diagnostics reach whoever asked for the page.
            throwOnError: false);

        _manager = manager;

        var dispatcher = registered as ClassicPipelineDispatcher
            ?? throw new InvalidOperationException(
                "ApplicationManager did not return a ClassicPipelineDispatcher.");

        // The exchange classifies the stop (host-initiated already claimed it) and keeps the
        // ApplicationStopping handler the callback triggers from re-entering this teardown.
        dispatcher.RegisterStopped(() =>
        {
            if (Interlocked.Exchange(ref _shutdown, 1) != 0)
            {
                return;
            }

            _restartRequested?.Invoke();
        });

        dispatcher.StartProcessing();

        return dispatcher;
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return Path.EndsInDirectorySeparator(path)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
