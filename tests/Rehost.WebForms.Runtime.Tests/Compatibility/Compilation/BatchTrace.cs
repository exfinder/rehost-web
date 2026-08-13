namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

internal static class BatchTrace
{
    internal static string Value(IEnumerable<string> trace, string prefix) =>
        trace.FirstOrDefault(entry => entry.StartsWith(prefix))?[prefix.Length..]
            ?? throw new InvalidOperationException(
                $"No '{prefix}' entry in trace:{Environment.NewLine}{string.Join(Environment.NewLine, trace)}");

    internal static int IndexOf(IReadOnlyList<string> trace, string prefix)
    {
        for (var index = 0; index < trace.Count; index++)
        {
            if (trace[index].StartsWith(prefix))
            {
                return index;
            }
        }

        throw new InvalidOperationException(
            $"No '{prefix}' entry in trace:{Environment.NewLine}{string.Join(Environment.NewLine, trace)}");
    }

    internal static bool IsLogicallyDeleted(string path) =>
        !File.Exists(path) || File.Exists(path + ".delete");
}
