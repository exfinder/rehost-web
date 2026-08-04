using System.Diagnostics;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

internal sealed class ScenarioProcess(Process process)
{
    private readonly Task<string> _standardOutput = process.StandardOutput.ReadToEndAsync();
    private readonly Task<string> _standardError = process.StandardError.ReadToEndAsync();

    internal int ExitCode => process.ExitCode;

    internal bool HasExited => process.HasExited;

    internal string StandardError => _standardError.Result;

    internal void WaitForExit()
    {
        // Both streams must drain concurrently, or a child writing more than the pipe buffer
        // blocks while a sequential reader waits on the stream it is not draining.
        Task.WaitAll(_standardOutput, _standardError);
        process.WaitForExit();
    }
}
