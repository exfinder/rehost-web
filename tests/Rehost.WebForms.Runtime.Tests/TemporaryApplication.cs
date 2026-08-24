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

    // The key directory stays inside the disposable root so bootstrap tests never touch the real
    // per-user default.
    internal string MachineKeyDirectory => Path.Combine(PhysicalRoot.FullName, ".machine-keys");

    internal WebFormsApplicationOptions CreateOptions(string virtualRoot = "/")
    {
        return new WebFormsApplicationOptions
        {
            ApplicationId = "test-app",
            PhysicalRootPath = PhysicalRoot.FullName,
            VirtualRootPath = virtualRoot,
            MachineKeyDirectory = MachineKeyDirectory,
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
