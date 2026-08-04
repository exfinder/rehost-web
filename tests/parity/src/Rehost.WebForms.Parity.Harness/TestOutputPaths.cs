#if NET
using System;
using System.IO;

namespace Rehost.WebForms.Parity.Harness;

public static class TestOutputPaths
{
    // .../bin/<configuration>/net10.0/ — deriving from the running assembly's own output path
    // means a child host is always taken from the configuration that is actually executing.
    public static string Configuration()
    {
        return Path.GetFileName(
            Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)))!;
    }

    public static string TestProjectOutput(string projectName)
    {
        return Path.Combine(
            RepositoryLocator.FindRoot(AppContext.BaseDirectory),
            "tests",
            projectName,
            "bin",
            Configuration(),
            "net10.0");
    }
}
#endif
