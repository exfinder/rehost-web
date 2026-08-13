using Rehost.WebForms.Parity.Contracts;

[assembly: System.Web.PreApplicationStartMethod(
    typeof(Rehost.WebForms.ScenarioProbes.PreStartProbe),
    nameof(Rehost.WebForms.ScenarioProbes.PreStartProbe.Start))]

namespace Rehost.WebForms.ScenarioProbes;

// Fixture application code records ordering here; the writing lives in TraceChannel.
public static class TraceProbe
{
    public const string TraceVariable = TraceChannel.TraceVariable;

    public static void Record(string entry) => TraceChannel.Record(entry);

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

        TraceProbe.Record("pre-start");
    }
}
