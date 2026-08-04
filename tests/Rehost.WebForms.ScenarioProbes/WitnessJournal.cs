namespace Rehost.WebForms.ScenarioProbes;

// In-memory and served over HTTP by WitnessHandler: live facts never ride the file journal,
// whose Windows sharing semantics once lost an outcome marker under load (see the client-reset
// post-mortem). The file journal remains only for process-death and cross-process evidence.
public static class WitnessJournal
{
    private static readonly Lock Gate = new();
    private static readonly List<string> Events = [];

    public static void Record(string entry)
    {
        lock (Gate)
        {
            Events.Add(entry);
        }
    }

    public static string[] Snapshot()
    {
        lock (Gate)
        {
            return [.. Events];
        }
    }
}
