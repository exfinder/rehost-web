using System.Web;
using Rehost.WebForms.ScenarioProtocol;

namespace Rehost.WebForms.ScenarioProbes;

// In-memory and served over HTTP by WitnessHandler: live facts never ride the trace file,
// whose Windows sharing semantics once lost an outcome marker under load (see the client-reset
// post-mortem). The trace file remains only for process-death and cross-process evidence.
public static class Witness
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

    public static string? Token(HttpRequest request) =>
        request.QueryString[WitnessProtocol.TokenKey];

    // Stages reach the witness only for requests carrying a token, so untagged requests
    // (and /witness itself) leave no trace.
    public static void Stage(HttpRequest request, string stage)
    {
        var token = Token(request);
        if (token != null)
        {
            Stage(token, stage);
        }
    }

    public static void Stage(string? token, string stage) =>
        Record(WitnessProtocol.StagePrefix + token + ":" + stage);

    public static string[] Snapshot()
    {
        lock (Gate)
        {
            return [.. Events];
        }
    }
}
