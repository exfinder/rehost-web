using Rehost.Web.ScenarioProtocol;

namespace Rehost.Web.ScenarioHost;

internal abstract class ScenarioOptionsCore
{
    private protected ScenarioOptionsCore(ArgumentScan scan)
    {
        ApplicationId = scan.Value(ScenarioHostGrammar.Id) ?? "scenario";
        ApplicationPath = Path.GetFullPath(scan.Required(ScenarioHostGrammar.App));
        CompilationTempDirectory = Path.GetFullPath(scan.Required(ScenarioHostGrammar.Temp));
        TracePath = Path.GetFullPath(scan.Required(ScenarioHostGrammar.Trace));
        ResponseDirectory = scan.Value(ScenarioHostGrammar.ResponseDir) is { } responses
            ? Path.GetFullPath(responses)
            : null;
        Requests = scan.Repeated(ScenarioHostGrammar.Request);
    }

    internal string ApplicationId { get; }

    internal string ApplicationPath { get; }

    internal string CompilationTempDirectory { get; }

    internal string TracePath { get; }

    internal string? ResponseDirectory { get; }

    internal List<string> Requests { get; }
}

internal sealed class ServeOptions : ScenarioOptionsCore
{
    private ServeOptions(ArgumentScan scan)
        : base(scan)
    {
        KestrelMaxBodyBytes = scan.Value(ScenarioHostGrammar.KestrelMaxBody) is { } maxBody
            ? long.Parse(maxBody)
            : null;
        Http2 = scan.Flag(ScenarioHostGrammar.Http2);
        Postbacks = scan.Repeated(ScenarioHostGrammar.Postback);
    }

    internal long? KestrelMaxBodyBytes { get; }

    internal bool Http2 { get; }

    internal List<string> Postbacks { get; }

    internal static ServeOptions From(ArgumentScan scan)
    {
        scan.AssertHonored(ScenarioHostGrammar.Serve, ScenarioHostGrammar.HonoredIn(ScenarioModes.Serve));
        if (scan.Repeated(ScenarioHostGrammar.Request).Count != 0 && scan.Repeated(ScenarioHostGrammar.Postback).Count != 0)
        {
            throw new ArgumentException(
                ScenarioHostGrammar.Request + " and " + ScenarioHostGrammar.Postback
                + " cannot be combined in " + ScenarioHostGrammar.Serve + " mode.");
        }

        return new ServeOptions(scan);
    }
}

internal sealed class BatchOptions : ScenarioOptionsCore
{
    private BatchOptions(ArgumentScan scan)
        : base(scan)
    {
        HoldGate = scan.Value(ScenarioHostGrammar.HoldGate);
        MachineConfigurationPath = scan.Value(ScenarioHostGrammar.MachineConfig) is { } machineConfig
            ? Path.GetFullPath(machineConfig)
            : null;

        if (Requests.Count == 0)
        {
            Requests.Add("/default");
        }
    }

    internal string? HoldGate { get; }

    internal string? MachineConfigurationPath { get; }

    internal static BatchOptions From(ArgumentScan scan)
    {
        scan.AssertHonored(ScenarioHostGrammar.BatchModeName, ScenarioHostGrammar.HonoredIn(ScenarioModes.Batch));
        return new BatchOptions(scan);
    }
}
