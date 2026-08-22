namespace Rehost.WebForms.TestSupport;

public static class TestOutputPaths
{
    // .../bin/<configuration>/net10.0/ — deriving from the running assembly's own output path
    // means a child host is always taken from the configuration that is actually executing.
    public static string Configuration()
    {
        return Path.GetFileName(
            Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)))!;
    }

    public static string ProjectOutput(string repoRelativeProjectDirectory)
    {
        return Path.Combine(
            RepositoryLocator.FindRoot(AppContext.BaseDirectory),
            repoRelativeProjectDirectory,
            "bin",
            Configuration(),
            "net10.0");
    }

    public static string TestProjectOutput(string projectName) =>
        ProjectOutput("tests/" + projectName);
}
