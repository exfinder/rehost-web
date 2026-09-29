using Rehost.Web.ScenarioProtocol;

namespace Rehost.Web.ScenarioHost;


// The scan owns option arity; each mode's options type declares what it honors and AssertHonored
// runs before any value is required, so a cross-mode option is reported ahead of missing
// requireds. An option added here but honored by neither mode fails every invocation passing it.
internal sealed class ArgumentScan
{
    private static readonly string[] Flags = ScenarioHostGrammar.Names(ScenarioOptionArity.Flag);
    private static readonly string[] Repeatable = ScenarioHostGrammar.Names(ScenarioOptionArity.Repeatable);
    private static readonly string[] Valued = ScenarioHostGrammar.Names(ScenarioOptionArity.Valued);

    private readonly Dictionary<string, string> _values = [];
    private readonly Dictionary<string, List<string>> _lists = [];
    private readonly HashSet<string> _flags = [];

    private ArgumentScan()
    {
    }

    internal static ArgumentScan Scan(string[] args)
    {
        var scan = new ArgumentScan();
        for (var i = 0; i < args.Length; i++)
        {
            var option = args[i];
            var value = i + 1 < args.Length ? args[i + 1] : null;
            if (Flags.Contains(option))
            {
                scan._flags.Add(option);
            }
            else if (Repeatable.Contains(option))
            {
                (scan._lists.TryGetValue(option, out var list)
                    ? list
                    : scan._lists[option] = []).Add(Require(value, option));
                i++;
            }
            else if (Valued.Contains(option))
            {
                scan._values[option] = Require(value, option);
                i++;
            }
            else
            {
                throw new ArgumentException("Unrecognized argument: " + option);
            }
        }

        return scan;
    }

    internal bool Flag(string option) => _flags.Contains(option);

    internal string? Value(string option) => _values.GetValueOrDefault(option);

    internal string Required(string option) =>
        Value(option) ?? throw new ArgumentException(option + " is required.");

    internal List<string> Repeated(string option) => _lists.GetValueOrDefault(option) ?? [];

    internal void AssertHonored(string mode, string[] honored)
    {
        var leftover = _flags
            .Concat(_values.Keys)
            .Concat(_lists.Keys)
            .Where(option => !honored.Contains(option))
            .OrderBy(option => option, StringComparer.Ordinal)
            .FirstOrDefault();
        if (leftover != null)
        {
            throw new ArgumentException(
                leftover + " is not honored in " + mode + " mode; remove it or switch the mode.");
        }
    }

    private static string Require(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(name + " is required.");
        }

        return value;
    }
}

