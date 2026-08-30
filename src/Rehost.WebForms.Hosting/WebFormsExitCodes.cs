namespace Rehost.WebForms.Hosting;

/// <summary>
/// Process exit codes the runtime asks its host to end with.
/// </summary>
public static class WebFormsExitCodes
{
    /// <summary>
    /// The runtime asked for the application to be rebuilt — an initialization failure,
    /// a configuration change, or <c>HttpRuntime.UnloadAppDomain</c>. The .NET Framework
    /// answer is an AppDomain recycle, which the port cannot express; a supervisor that
    /// restarts the process on this code gets the same outcome. A host-initiated stop
    /// leaves the exit code alone.
    /// </summary>
    public const int RestartRequested = 82;
}
