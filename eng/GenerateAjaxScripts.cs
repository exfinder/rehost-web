#:property ManagePackageVersionsCentrally=false
#:package AjaxMin@4.12.4057.21792
using System.Text.RegularExpressions;
using Microsoft.Ajax.Utilities;

const string Copyright = "// Copyright (C) Microsoft Corporation. All rights reserved.";

var repo = Directory.GetCurrentDirectory();
var scriptRoot = Path.Combine(repo, "src", "System.Web.Extensions.ReferenceSource", "Script");
var outputRoot = Path.Combine(repo, "src", "Rehost.WebForms.Extensions", "Scripts");

if (!Directory.Exists(scriptRoot))
{
    Console.Error.WriteLine($"Run from the repository root; '{scriptRoot}' does not exist.");
    return 1;
}

Directory.CreateDirectory(outputRoot);

// Framework's own output keeps hex literals but not exponent notation, plain
// booleans, unfolded `if`, and `var` outside the `for` initializer.
var settings = new CodeSettings
{
    LocalRenaming = LocalRenaming.CrunchAll,
    OutputMode = OutputMode.SingleLine,
    KillSwitch = (TreeModifications)(0x400000000UL | 0x200000UL | 0x2000UL | 0x400UL),
};

var written = 0;
foreach (var jsa in Directory.GetFiles(scriptRoot, "*.jsa").OrderBy(path => path, StringComparer.Ordinal))
{
    // COPYRIGHT stays undefined: the shipped script carries one banner, not the 62
    // per-file headers. DEBUG builds need a generator this repository does not have,
    // so no .debug.js is produced and ScriptMode.Auto falls back to these.
    var preprocessed = Build(Path.GetFileName(jsa), []);
    var name = Path.GetFileNameWithoutExtension(jsa) + ".js";

    var minifier = new Minifier();
    var code = minifier.MinifyJavaScript(preprocessed, settings).TrimEnd('\r', '\n');
    if (minifier.Errors.Count > 0)
    {
        Console.Error.WriteLine($"{name}: {minifier.Errors.First()}");
        return 1;
    }

    if (!code.EndsWith(';'))
    {
        code += ";";
    }

    var rule = "//" + new string('-', Copyright.Length - 2);
    var banner = string.Join("\r\n", [rule, Copyright, rule, "// " + name]) + "\r\n";

    File.WriteAllText(Path.Combine(outputRoot, name), banner + code);
    written++;
}

Console.WriteLine($"Wrote {written} scripts to {outputRoot}");
return 0;

string Build(string jsa, string[] symbols)
{
    var lines = new List<string>();
    Emit(jsa, new HashSet<string>(symbols, StringComparer.Ordinal), lines);
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
