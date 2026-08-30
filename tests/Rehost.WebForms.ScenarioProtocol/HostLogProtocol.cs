namespace Rehost.WebForms.ScenarioProtocol;

public sealed record HostLogEntry(
    string Category,
    string Level,
    int EventId,
    string ExceptionType,
    string ExceptionText,
    string Message);

// The host process's own log entries, served live over HTTP like the in-application witness:
// entries the adapter routed into the host pipeline never ride the trace file.
public static class HostLogProtocol
{
    public const string Path = "/host-log";

    private const char Separator = '\u001f';

    public static string Format(HostLogEntry entry) =>
        string.Join(
            Separator,
            entry.Category,
            entry.Level,
            entry.EventId.ToString(),
            entry.ExceptionType,
            entry.ExceptionText.ReplaceLineEndings(" "),
            entry.Message.ReplaceLineEndings(" "));

    public static HostLogEntry[] Parse(string payload) =>
        [.. payload
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(Separator))
            .Where(fields => fields.Length == 6)
            .Select(fields => new HostLogEntry(
                fields[0],
                fields[1],
                int.Parse(fields[2]),
                fields[3],
                fields[4],
                fields[5]))];
}
