namespace Rehost.WebForms.TestSupport;

public static class TraceFile
{
    public static List<string> ReadLines(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        // The child keeps the file open for appending.
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }
}
