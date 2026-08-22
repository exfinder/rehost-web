namespace Rehost.WebForms.ScenarioProtocol;

public enum ScenarioOptionArity
{
    Flag,
    Valued,
    Repeatable,
}

[Flags]
public enum ScenarioModes
{
    Serve = 1,
    Batch = 2,
}

public sealed record ScenarioOption(string Name, ScenarioOptionArity Arity, ScenarioModes Modes);

// The scenario-host CLI, spelled once: the host's parser and TestSupport's mode-locked builders
// both derive from this table, so an option cannot exist with different arity or mode validity
// on the two sides of the process boundary.
public static class ScenarioHostGrammar
{
    // Batch mode has no selector flag; this name is display-only in mode errors.
    public const string BatchModeName = "batch";

    public const string Serve = "--serve";
    public const string App = "--app";
    public const string Temp = "--temp";
    public const string Trace = "--trace";
    public const string Id = "--id";
    public const string ResponseDir = "--response-dir";
    public const string Request = "--request";
    public const string Postback = "--postback";
    public const string Http2 = "--http2";
    public const string KestrelMaxBody = "--kestrel-max-body";
    public const string HoldGate = "--hold-gate";
    public const string MachineConfig = "--machine-config";

    public static readonly IReadOnlyList<ScenarioOption> Options =
    [
        new(Serve, ScenarioOptionArity.Flag, ScenarioModes.Serve),
        new(App, ScenarioOptionArity.Valued, ScenarioModes.Serve | ScenarioModes.Batch),
        new(Temp, ScenarioOptionArity.Valued, ScenarioModes.Serve | ScenarioModes.Batch),
        new(Trace, ScenarioOptionArity.Valued, ScenarioModes.Serve | ScenarioModes.Batch),
        new(Id, ScenarioOptionArity.Valued, ScenarioModes.Serve | ScenarioModes.Batch),
        new(ResponseDir, ScenarioOptionArity.Valued, ScenarioModes.Serve | ScenarioModes.Batch),
        new(Request, ScenarioOptionArity.Repeatable, ScenarioModes.Serve | ScenarioModes.Batch),
        new(Postback, ScenarioOptionArity.Repeatable, ScenarioModes.Serve),
        new(Http2, ScenarioOptionArity.Flag, ScenarioModes.Serve),
        new(KestrelMaxBody, ScenarioOptionArity.Valued, ScenarioModes.Serve),
        new(HoldGate, ScenarioOptionArity.Valued, ScenarioModes.Batch),
        new(MachineConfig, ScenarioOptionArity.Valued, ScenarioModes.Batch),
    ];

    public static string[] Names(ScenarioOptionArity arity) =>
        [.. Options.Where(option => option.Arity == arity).Select(option => option.Name)];

    public static string[] HonoredIn(ScenarioModes mode) =>
        [.. Options.Where(option => option.Modes.HasFlag(mode)).Select(option => option.Name)];
}
