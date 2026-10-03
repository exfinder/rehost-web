using System.Text;
using System.Xml;
using Microsoft.Build.Framework;

namespace Rehost.Web.Build.Tasks;

public sealed class RetargetConfigAssemblyNames : Microsoft.Build.Utilities.Task
{
    [Required]
    public ITaskItem[] Files { get; set; } = [];

    public override bool Execute()
    {
        foreach (var file in Files)
        {
            var path = file.GetMetadata("FullPath");
            try
            {
                Rewrite(path);
            }
            catch (XmlException exception)
            {
                Log.LogError(
                    null, null, null, path, exception.LineNumber, exception.LinePosition, 0, 0,
                    exception.Message);
            }
        }

        return !Log.HasLoggedErrors;
    }

    private void Rewrite(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var (encoding, edits) = FindEdits(bytes);
        if (edits.Count == 0)
        {
            return;
        }

        var preamble = encoding.GetPreamble();
        var preambleLength = bytes.AsSpan().StartsWith(preamble) ? preamble.Length : 0;
        var text = new StringBuilder(encoding.GetString(bytes, preambleLength, bytes.Length - preambleLength));
        var lineStarts = LineStarts(text.ToString());
        foreach (var edit in Enumerable.Reverse(edits))
        {
            var (start, end) = ValueSpan(text, lineStarts[edit.Line - 1] + edit.Position - 1);
            var value = Escape(edit.Retargeted, text[end]);
            text.Remove(start, end - start).Insert(start, value);
        }

        using (var stream = File.Create(path))
        {
            stream.Write(bytes, 0, preambleLength);
            var rewritten = encoding.GetBytes(text.ToString());
            stream.Write(rewritten, 0, rewritten.Length);
        }

        foreach (var edit in edits)
        {
            Log.LogMessage(
                null, null, null, path, edit.Line, edit.Position, 0, 0, MessageImportance.Normal,
                $"Retargeted '{edit.Original}' to '{edit.Retargeted}'.");
        }
    }

    private static (Encoding Encoding, List<Edit> Edits) FindEdits(byte[] bytes)
    {
        var edits = new List<Edit>();
        using var reader = new XmlTextReader(new MemoryStream(bytes))
        {
            DtdProcessing = DtdProcessing.Ignore,
            WhitespaceHandling = WhitespaceHandling.None,
        };
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            while (reader.MoveToNextAttribute())
            {
                var original = reader.Value;
                var retargeted = reader.LocalName == "assembly"
                    ? AssemblyRetargets.RetargetAssemblyName(original)
                    : AssemblyRetargets.Retarget(original);
                if (!string.Equals(original, retargeted, StringComparison.Ordinal))
                {
                    edits.Add(new Edit(reader.LineNumber, reader.LinePosition, original, retargeted));
                }
            }
        }

        return (reader.Encoding ?? new UTF8Encoding(false), edits);
    }

    private static List<int> LineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
            {
                index++;
            }

            if (text[index] is '\r' or '\n')
            {
                starts.Add(index + 1);
            }
        }

        return starts;
    }

    private static (int Start, int End) ValueSpan(StringBuilder text, int attributeStart)
    {
        var index = attributeStart;
        while (text[index] != '=')
        {
            index++;
        }

        index++;
        while (text[index] is not ('"' or '\''))
        {
            index++;
        }

        var quote = text[index];
        var start = index + 1;
        var end = start;
        while (text[end] != quote)
        {
            end++;
        }

        return (start, end);
    }

    private static string Escape(string value, char quote)
    {
        var escaped = value.Replace("&", "&amp;").Replace("<", "&lt;");
        return quote == '"' ? escaped.Replace("\"", "&quot;") : escaped.Replace("'", "&apos;");
    }

    private sealed record Edit(int Line, int Position, string Original, string Retargeted);
}
