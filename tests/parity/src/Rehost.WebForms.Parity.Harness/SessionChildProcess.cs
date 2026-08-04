using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
#if !NET
using System.Text;
#endif
using System.Threading.Tasks;
using Rehost.WebForms.Parity.Contracts;

namespace Rehost.WebForms.Parity.Harness;

// Each session observes a cold runtime, so it needs a process whose HttpRuntime singleton,
// AssemblyLoadContext resolver, and activated application have never been touched.
public static class SessionChildProcess
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);

    public static SessionObservation Run(
        Assembly hostAssembly,
        SessionSpecification session,
        string manifestPath,
        string fixtureRoot,
        TimeSpan? timeout = null)
    {
        var startInfo = CreateStartInfo(hostAssembly, session.Name, manifestPath, fixtureRoot);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The session process failed to start.");
        // Both streams must drain concurrently. A session observation larger than the pipe
        // buffer, which is 4 KB on Windows against 64 KB elsewhere, blocks the child mid-write
        // while a sequential reader waits on the stream it is not draining.
        var outputReader = process.StandardOutput.ReadToEndAsync();
        var errorReader = process.StandardError.ReadToEndAsync();

        var budget = timeout ?? DefaultTimeout;
        if (!process.WaitForExit((int)budget.TotalMilliseconds))
        {
            Kill(process);
            process.WaitForExit();
            Task.WaitAll(outputReader, errorReader);
            throw new TimeoutException(
                "Session '"
                + session.Name
                + "' did not exit within "
                + budget.TotalSeconds
                + "s and was killed. "
                + errorReader.Result);
        }

        Task.WaitAll(outputReader, errorReader);

        if (errorReader.Result.Length > 0)
        {
            Console.Error.Write(errorReader.Result);
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Session '"
                + session.Name
                + "' exited with code "
                + process.ExitCode
                + ".");
        }

        return ParityJson.Deserialize<SessionObservation>(outputReader.Result)
            ?? throw new InvalidDataException(
                "Session '" + session.Name + "' produced no observation.");
    }

    private static ProcessStartInfo CreateStartInfo(
        Assembly hostAssembly,
        string sessionName,
        string manifestPath,
        string fixtureRoot)
    {
#if NET
        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The host process path is unavailable.");
        var startInfo = new ProcessStartInfo(executablePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // A dotted assembly name defeats GetFileNameWithoutExtension, which would truncate at
        // the first dot; compare against the apphost path instead.
        var entryAssembly = hostAssembly.Location;
        var apphostPath = Path.ChangeExtension(
            entryAssembly,
            OperatingSystem.IsWindows() ? ".exe" : null);
        if (!string.Equals(executablePath, apphostPath, StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(entryAssembly);
        }

        startInfo.ArgumentList.Add("run-session");
        startInfo.ArgumentList.Add("--session");
        startInfo.ArgumentList.Add(sessionName);
        startInfo.ArgumentList.Add("--manifest");
        startInfo.ArgumentList.Add(manifestPath);
        startInfo.ArgumentList.Add("--fixtures");
        startInfo.ArgumentList.Add(fixtureRoot);
#else
        var executablePath = new Uri(hostAssembly.GetName().CodeBase).LocalPath;
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            Arguments = string.Join(
                " ",
                "run-session",
                "--session",
                Quote(sessionName),
                "--manifest",
                Quote(manifestPath),
                "--fixtures",
                Quote(fixtureRoot))
        };
#endif
        return startInfo;
    }

    private static void Kill(Process process)
    {
        try
        {
#if NET
            process.Kill(entireProcessTree: true);
#else
            process.Kill();
#endif
        }
        catch (InvalidOperationException)
        {
            // Exited between the timeout and the kill.
        }
    }

#if !NET
    private static string Quote(string value)
    {
        return "\"" + value + "\"";
    }
#endif
}
