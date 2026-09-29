using System.Diagnostics;
using System.Text;

namespace Rehost.Web.ScenarioProtocol;

// Scenario events come from places that share no stream — a bin assembly before the application
// starts, generated App_Code and Global.asax, and the host process itself — so they meet in one
// append-only file named by the environment.
public static class TraceChannel
{
    public const string TraceVariable = "REHOST_SCENARIO_TRACE";

    private static readonly object Gate = new();

    public static void Record(string entry) => RecordTo(TraceVariable, entry);

    public static void RecordTo(string variable, string entry)
    {
        var path = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var payload = Encoding.UTF8.GetBytes(entry + Environment.NewLine);

        lock (Gate)
        {
            // Writers and readers must both share the file: on Windows an overlapping open with
            // a narrower share mode throws, and a trace write that throws loses the entry and
            // fails whatever recorded it. Retry rides out readers that do not share for write.
            var attempt = Stopwatch.StartNew();
            while (true)
            {
                try
                {
                    using var stream = new FileStream(
                        path,
                        FileMode.Append,
                        FileAccess.Write,
                        FileShare.ReadWrite | FileShare.Delete);
                    stream.Write(payload, 0, payload.Length);
                    return;
                }
                catch (IOException) when (attempt.ElapsedMilliseconds < 5000)
                {
                    Thread.Sleep(1);
                }
            }
        }
    }

    public static string ReadAll(string path)
    {
        if (!File.Exists(path))
        {
            return "";
        }

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static List<string> ReadLines(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lines.Add(line);
        }

        return lines;
    }
}
