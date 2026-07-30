[assembly: System.Web.PreApplicationStartMethod(
    typeof(Rehost.WebForms.ScenarioProbes.PreStartProbe),
    nameof(Rehost.WebForms.ScenarioProbes.PreStartProbe.Start))]

namespace Rehost.WebForms.ScenarioProbes;

// Fixture application code records ordering here. The file rather than standard output, because
// the events come from four places that do not share a stream: a bin assembly before the
// application starts, generated App_Code, generated Global.asax, and the host itself.
public static class ScenarioJournal
{
    public const string TraceVariable = "REHOST_SCENARIO_TRACE";

    private static readonly Lock Gate = new();

    public static void Record(string entry)
    {
        var path = Environment.GetEnvironmentVariable(TraceVariable);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        lock (Gate)
        {
            File.AppendAllText(path, entry + Environment.NewLine);
        }
    }

    // The generated assembly's identity is what tells a later run whether its output was reused
    // or recompiled, since a recompile draws a new random name.
    public static void RecordAssembly(string label, object instance)
    {
        var assembly = instance.GetType().Assembly;
        Record(label + ":" + assembly.GetName().Name);
    }
}

public static class PreStartProbe
{
    private static int _started;

    public static void Start()
    {
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
        {
            return;
        }

        ScenarioJournal.Record("pre-start");
    }
}
