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
            true,
            true);

        _manager = manager;

        return registered as ClassicPipelineDispatcher
            ?? throw new InvalidOperationException(
                "ApplicationManager did not return a ClassicPipelineDispatcher.");
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return Path.EndsInDirectorySeparator(path)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
