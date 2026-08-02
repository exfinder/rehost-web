using Rehost.WebForms.Hosting;

namespace Rehost.WebForms.Runtime.Tests;

internal sealed class TemporaryApplication : IDisposable
{
    private TemporaryApplication(DirectoryInfo physicalRoot, string outputDirectory)
    {
        PhysicalRoot = physicalRoot;
        OutputDirectory = outputDirectory;
    }

    internal DirectoryInfo PhysicalRoot { get; }

    internal string OutputDirectory { get; }

    internal static TemporaryApplication Create()
    {
        return new TemporaryApplication(
            Directory.CreateTempSubdirectory("rehost-bootstrap-"),
            AppContext.BaseDirectory);
    }

    internal WebFormsApplicationOptions CreateOptions(string virtualRoot = "/")
    {
        return new WebFormsApplicationOptions
        {
            ApplicationId = "test-app",
            PhysicalRootPath = PhysicalRoot.FullName,
            VirtualRootPath = virtualRoot,
        };
    }

    internal ApplicationBootstrapConfiguration CreateConfiguration()
    {
        return ApplicationBootstrapConfiguration.Create(CreateOptions(), OutputDirectory);
    }

    public void Dispose()
    {
        PhysicalRoot.Delete(recursive: true);
    }
}
