using System.Text.RegularExpressions;

var repo = Directory.GetCurrentDirectory();
var scriptRoot = Path.Combine(repo, "src", "System.Web.Extensions.ReferenceSource", "Script");
var outputRoot = Path.Combine(repo, "src", "Rehost.WebForms.Extensions", "Scripts");

if (!Directory.Exists(scriptRoot))
{
    Console.Error.WriteLine($"Run from the repository root; '{scriptRoot}' does not exist.");
    return 1;
}

Directory.CreateDirectory(outputRoot);

var written = 0;
foreach (var jsa in Directory.GetFiles(scriptRoot, "*.jsa").OrderBy(path => path, StringComparer.Ordinal))
{
    var name = Path.GetFileNameWithoutExtension(jsa);

    // COPYRIGHT stays undefined in both builds: the shipped release script carries the
    // one //! banner from the .jsa, not the 62 per-file headers. DEBUGINTERNAL never ships.
    Write($"{name}.js", Build(jsa, []));
    Write($"{name}.debug.js", Build(jsa, ["DEBUG"]));
}

Console.WriteLine($"Wrote {written} scripts to {outputRoot}");
return 0;

void Write(string fileName, string content)
{
    File.WriteAllText(Path.Combine(outputRoot, fileName), content);
    written++;
}

string Build(string jsa, string[] symbols)
{
    var lines = new List<string>();
    Emit(Path.GetFileName(jsa), new HashSet<string>(symbols, StringComparer.Ordinal), lines);
    return string.Join("\n", lines) + "\n";
}

void Emit(string relativePath, HashSet<string> symbols, List<string> output)
{
    var include = new Regex("""^\s*#include\s+"([^"]+)"\s*$""");
    var ifDirective = new Regex(@"^\s*#if\s+(\w+)\s*$");
    var elseDirective = new Regex(@"^\s*#else\s*$");
    var endIfDirective = new Regex(@"^\s*#endif\s*$");
    var lineDirective = new Regex(@"^(\s*)##(\w+)[ \t]?(.*)$");

    var branches = new Stack<bool>();

    foreach (var line in File.ReadLines(Path.Combine(scriptRoot, relativePath)))
    {
        var match = ifDirective.Match(line);
        if (match.Success)
        {
            branches.Push(symbols.Contains(match.Groups[1].Value));
            continue;
        }

        if (elseDirective.IsMatch(line))
        {
            branches.Push(!branches.Pop());
            continue;
        }

        if (endIfDirective.IsMatch(line))
        {
            branches.Pop();
            continue;
        }

        if (branches.Contains(false))
        {
            continue;
        }

        match = include.Match(line);
        if (match.Success)
        {
            Emit(match.Groups[1].Value.Replace('\\', '/'), symbols, output);
            continue;
        }

        match = lineDirective.Match(line);
        if (match.Success)
        {
            if (symbols.Contains(match.Groups[2].Value))
            {
                output.Add(match.Groups[1].Value + match.Groups[3].Value);
            }

            continue;
        }

        output.Add(line);
    }
}
