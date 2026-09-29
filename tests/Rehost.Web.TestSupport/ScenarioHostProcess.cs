using System.Diagnostics;

namespace Rehost.Web.TestSupport;

public sealed class ScenarioHostProcess : IDisposable
{
    private readonly Process _process;
    private readonly Task<string> _standardOutput;
    private readonly Task<string> _standardError;

    public ScenarioHostProcess(Process process)
    {
        _process = process;
        _standardOutput = process.StandardOutput.ReadToEndAsync();
        _standardError = process.StandardError.ReadToEndAsync();
    }

    public int Id => _process.Id;

    public bool HasExited => _process.HasExited;

    public int ExitCode => _process.ExitCode;

    public string StandardOutput => _standardOutput.Result;

    public string StandardError => _standardError.Result;

    public bool WaitForExit(TimeSpan timeout) => _process.WaitForExit(timeout);

    public void WaitForExit()
    {
        // Both streams must drain concurrently, or a child writing more than the pipe buffer
        // blocks while a sequential reader waits on the stream it is not draining.
        Task.WaitAll(_standardOutput, _standardError);
        _process.WaitForExit();
    }

    public void Kill()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
        }

        _process.WaitForExit();
    }

    public void Dispose()
    {
        Kill();
        _process.Dispose();
    }
}
