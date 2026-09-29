using Rehost.Web.Hosting;

namespace Rehost.Web.Tests;

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

    internal RehostWebOptions CreateOptions(string virtualRoot = "/")
    {
        return new RehostWebOptions
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
