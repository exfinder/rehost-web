using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Rehost.Web.Parity.Harness;

public static class FixtureValidator
{
    private const string ProbeAssemblyName = "Rehost.Web.Parity.Probes";

    public static void Validate(string applicationPath, Assembly hostAssembly)
    {
        applicationPath = Path.GetFullPath(applicationPath);
        var probePath = Path.Combine(applicationPath, "bin", ProbeAssemblyName + ".dll");
        var hostProbePath = Path.Combine(
            AppContext.BaseDirectory,
            ProbeAssemblyName + ".dll");

        if (!File.Exists(Path.Combine(applicationPath, "web.config")))
        {
            throw new FileNotFoundException(
                "Fixture web.config is missing.",
                Path.Combine(applicationPath, "web.config"));
        }

        if (!File.Exists(probePath))
        {
            throw new FileNotFoundException(
                "Probe handler/module assembly must exist only in fixture app/bin.",
                probePath);
        }

        if (File.Exists(hostProbePath))
        {
            throw new InvalidOperationException(
                "Probe assembly must not exist beside the host executable.");
        }

        if (hostAssembly.GetReferencedAssemblies().Any(
                name => string.Equals(
                    name.Name,
                    ProbeAssemblyName,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Host assembly must not reference " + ProbeAssemblyName + ".");
        }

        if (AppDomain.CurrentDomain.GetAssemblies().Any(
                assembly => string.Equals(
                    assembly.GetName().Name,
                    ProbeAssemblyName,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Probe assembly was loaded before application activation.");
        }
    }
}
