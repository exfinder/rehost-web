using System.Collections;
using System.Resources;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static class Program
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private static readonly string[] ArtifactNames =
    [
        "CacheExpires.g.cs",
        "CacheUsage.g.cs",
        "ModName.g.cs",
        "RegularExpressions.g.cs",
        "SR.g.cs",
        "System.Web.resources",
    ];

    private static readonly string[] ServicesArtifactNames =
    [
        "Res.g.cs",
        "System.Web.Services.resources",
    ];

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 3 && args[0] == "generate")
            {
                Generate(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
                return 0;
            }

            if (args.Length == 3 && args[0] == "generate-services")
            {
                GenerateServices(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
                return 0;
            }

            if (args.Length == 2 && args[0] == "verify")
            {
                Verify(Path.GetFullPath(args[1]));
                return 0;
            }

            Console.Error.WriteLine(
                "Usage: Rehost.Web.GeneratedInputs generate <repository-root> <output-directory>\n" +
                "       Rehost.Web.GeneratedInputs generate-services <repository-root> <output-directory>\n" +
                "       Rehost.Web.GeneratedInputs verify <repository-root>");
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void Generate(string repositoryRoot, string outputDirectory)
    {
        string sourceRoot = Path.Combine(repositoryRoot, "src", "System.Web.ReferenceSource");
        string inputRoot = Path.Combine(repositoryRoot, "eng", "Rehost.Web.GeneratedInputs", "inputs");

        IdentityManifest identities = ReadJson<IdentityManifest>(Path.Combine(inputRoot, "assembly-identities.json"));
        NativeNamesManifest nativeNames = ReadJson<NativeNamesManifest>(Path.Combine(inputRoot, "native-names.json"));
        RegularExpressionsManifest regularExpressions = ReadJson<RegularExpressionsManifest>(
            Path.Combine(inputRoot, "regular-expressions.json"));
        SortedDictionary<string, string> resources = ReadResources(Path.Combine(sourceRoot, "System.Web.txt"));

        ValidateManifests(identities, nativeNames, regularExpressions);
        Directory.CreateDirectory(outputDirectory);

        WriteText(Path.Combine(outputDirectory, "SR.g.cs"), GenerateSr(resources, identities.Resource.BaseName));
        WriteResources(Path.Combine(outputDirectory, "System.Web.resources"), resources);
        WriteText(
            Path.Combine(outputDirectory, "ModName.g.cs"),
            Preprocess(
                Path.Combine(sourceRoot, "Names.cspp"),
                nativeNames.Macros,
                "Names.cspp + explicit names.h macro manifest",
                nativeNames.Symbols));
        WriteText(
            Path.Combine(outputDirectory, "CacheUsage.g.cs"),
            Preprocess(
                Path.Combine(sourceRoot, "cacheusage.cspp"),
                new Dictionary<string, string>(StringComparer.Ordinal),
                "cacheusage.cspp",
                conditionalSymbols: null));
        WriteText(
            Path.Combine(outputDirectory, "CacheExpires.g.cs"),
            Preprocess(
                Path.Combine(sourceRoot, "cacheexpires.cspp"),
                new Dictionary<string, string>(StringComparer.Ordinal),
                "cacheexpires.cspp",
                conditionalSymbols: null));
        WriteText(
            Path.Combine(outputDirectory, "RegularExpressions.g.cs"),
            GenerateRegularExpressions(regularExpressions));
    }

    private static void GenerateServices(string repositoryRoot, string outputDirectory)
    {
        string sourceRoot = Path.Combine(repositoryRoot, "src", "System.Web.Services.ReferenceSource");
        SortedDictionary<string, string> resources = ReadResources(Path.Combine(sourceRoot, "System.Web.Services.txt"));

        Directory.CreateDirectory(outputDirectory);
        WriteText(Path.Combine(outputDirectory, "Res.g.cs"), GenerateRes(resources));
        WriteResources(Path.Combine(outputDirectory, "System.Web.Services.resources"), resources);
    }

    private static void Verify(string repositoryRoot)
    {
        string temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "rehost-generated-inputs-" + Guid.NewGuid().ToString("N"));
        string first = Path.Combine(temporaryRoot, "first");
        string second = Path.Combine(temporaryRoot, "second");

        try
        {
            Generate(repositoryRoot, first);
            Generate(repositoryRoot, second);
            GenerateServices(repositoryRoot, first);
            GenerateServices(repositoryRoot, second);

            foreach (string artifactName in ArtifactNames.Concat(ServicesArtifactNames))
            {
                byte[] firstBytes = File.ReadAllBytes(Path.Combine(first, artifactName));
                byte[] secondBytes = File.ReadAllBytes(Path.Combine(second, artifactName));
                Require(firstBytes.AsSpan().SequenceEqual(secondBytes), $"Repeatability failed for {artifactName}.");
            }

            string inputRoot = Path.Combine(repositoryRoot, "eng", "Rehost.Web.GeneratedInputs", "inputs");
            string sourceRoot = Path.Combine(repositoryRoot, "src", "System.Web.ReferenceSource");
            IdentityManifest identities = ReadJson<IdentityManifest>(Path.Combine(inputRoot, "assembly-identities.json"));
            RegularExpressionsManifest regularExpressions = ReadJson<RegularExpressionsManifest>(
                Path.Combine(inputRoot, "regular-expressions.json"));
            SortedDictionary<string, string> expectedResources = ReadResources(Path.Combine(sourceRoot, "System.Web.txt"));

            VerifyResources(Path.Combine(first, "System.Web.resources"), expectedResources);
            VerifySr(Path.Combine(first, "SR.g.cs"), expectedResources, identities);
            VerifyAssemblyRef(
                Path.Combine(repositoryRoot, "eng", "Rehost.Web.ReferenceSource.BuildInputs", "AssemblyRef.cs"),
                sourceRoot,
                identities);
            VerifyModName(Path.Combine(first, "ModName.g.cs"));
            VerifyCacheSources(
                Path.Combine(first, "CacheUsage.g.cs"),
                Path.Combine(first, "CacheExpires.g.cs"));
            VerifyRegularExpressions(Path.Combine(first, "RegularExpressions.g.cs"), regularExpressions);

            SortedDictionary<string, string> expectedServicesResources = ReadResources(
                Path.Combine(repositoryRoot, "src", "System.Web.Services.ReferenceSource", "System.Web.Services.txt"));
            VerifyServicesResources(Path.Combine(first, "System.Web.Services.resources"), expectedServicesResources);
            VerifyRes(Path.Combine(first, "Res.g.cs"), expectedServicesResources);

            Console.WriteLine($"Verified {ArtifactNames.Length + ServicesArtifactNames.Length} deterministic generated artifacts.");
            foreach (string artifactName in ArtifactNames.Concat(ServicesArtifactNames))
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(first, artifactName));
                Console.WriteLine($"{artifactName}\t{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}");
            }
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static T ReadJson<T>(string path)
    {
        T? result = JsonSerializer.Deserialize<T>(
            File.ReadAllText(path, Encoding.UTF8),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return result ?? throw new InvalidDataException($"Could not deserialize {path}.");
    }

    private static void ValidateManifests(
        IdentityManifest identities,
        NativeNamesManifest nativeNames,
        RegularExpressionsManifest regularExpressions)
    {
        Require(identities.OutputAssembly.Name == "Rehost.Web", "Unexpected output assembly name.");
        Require(!identities.OutputAssembly.StrongNamed, "The generator must not claim Microsoft's strong-name identity.");
        Require(identities.OutputAssembly.PublicKeyToken is null, "An unsigned output must have a null public-key token.");
        Require(identities.Resource.BaseName == "System.Web", "The SR resource base name must be System.Web.");
        Require(identities.Resource.LogicalName == "System.Web.resources", "The neutral resource name must be System.Web.resources.");
        Require(identities.AssemblyRefConstants["SystemWeb"] == "Rehost.Web", "AssemblyRef.SystemWeb must match the runtime identity.");
        Require(!identities.AssemblyRefConstants.Values.Any(value => value.Contains("Portable.", StringComparison.Ordinal)),
            "Portable.* identities are not valid generator inputs.");
        Require(nativeNames.Include == "names.h", "Names.cspp must be resolved from the explicit names.h manifest.");
        Require(nativeNames.TargetProfile == "desktop-framework-windows", "The ModName profile must be explicit.");
        Require(nativeNames.Symbols.TryGetValue("FEATURE_PAL", out bool featurePal) && !featurePal,
            "The desktop-framework-windows ModName profile must explicitly disable FEATURE_PAL.");
        Require(regularExpressions.Namespace == "System.Web.RegularExpressions", "Unexpected regex namespace.");
        Require(regularExpressions.Regexes.Count == 27, "The original System.Web regex input contains 27 expressions.");
        Require(regularExpressions.Regexes.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() == 27,
            "Regular-expression type names must be unique.");
    }

    private static SortedDictionary<string, string> ReadResources(string path)
    {
        SortedDictionary<string, string> resources = new(StringComparer.Ordinal);
        int lineNumber = 0;
        foreach (string line in File.ReadLines(path, Encoding.UTF8))
        {
            lineNumber++;
            string trimmedStart = line.TrimStart();
            if (trimmedStart.Length == 0 || trimmedStart[0] == ';')
            {
                continue;
            }

            int separator = line.IndexOf('=');
            if (separator <= 0)
            {
                throw new InvalidDataException($"Invalid resource entry at {path}:{lineNumber}.");
            }

            string name = line[..separator];
            // ResGen ignores whitespace immediately following the separator.
            string value = DecodeResourceEscapes(line[(separator + 1)..].TrimStart(), path, lineNumber);
            if (!resources.TryAdd(name, value))
            {
                throw new InvalidDataException($"Duplicate resource key '{name}' at {path}:{lineNumber}.");
            }
        }

        return resources;
    }

    private static string DecodeResourceEscapes(string value, string path, int lineNumber)
    {
        StringBuilder result = new(value.Length);
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (current != '\\')
            {
                result.Append(current);
                continue;
            }

            if (++index == value.Length)
            {
                throw new InvalidDataException($"Trailing resource escape at {path}:{lineNumber}.");
            }

            char escaped = value[index];
            result.Append(escaped switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                '\\' => '\\',
                '\"' => '\"',
                '\'' => '\'',
                _ => throw new InvalidDataException(
                    $"Unsupported resource escape '\\{escaped}' at {path}:{lineNumber}."),
            });
        }

        return result.ToString();
    }

    private static string GenerateSr(SortedDictionary<string, string> resources, string resourceBaseName)
    {
        StringBuilder output = GeneratedHeader("System.Web.txt -> SR.g.cs");
        output.AppendLine("using System.Globalization;");
        output.AppendLine("using System.Resources;");
        output.AppendLine("using System.Threading;");
        output.AppendLine();
        output.AppendLine("namespace System.Web {");
        output.AppendLine("    internal sealed class SR {");

        foreach (string name in resources.Keys)
        {
            Require(Regex.IsMatch(name, @"^[_\p{L}][\p{L}\p{Nd}_]*$", RegexOptions.CultureInvariant),
                $"Resource key '{name}' is not a valid C# identifier.");
            output.Append("        internal const string ").Append(name).Append(" = ")
                .Append(CSharpString(name)).AppendLine(";");
        }

        output.AppendLine();
        output.AppendLine("        private static SR loader;");
        output.AppendLine("        private readonly ResourceManager resources;");
        output.AppendLine("        private static CultureInfo Culture => null;");
        output.AppendLine("        public static ResourceManager Resources => GetLoader().resources;");
        output.AppendLine();
        output.AppendLine("        internal SR() {");
        output.Append("            resources = new ResourceManager(").Append(CSharpString(resourceBaseName))
            .AppendLine(", typeof(SR).Assembly);");
        output.AppendLine("        }");
        output.AppendLine();
        output.AppendLine("        private static SR GetLoader() {");
        output.AppendLine("            if (loader == null) {");
        output.AppendLine("                SR value = new SR();");
        output.AppendLine("                Interlocked.CompareExchange(ref loader, value, null);");
        output.AppendLine("            }");
        output.AppendLine("            return loader;");
        output.AppendLine("        }");
        output.AppendLine();
        output.AppendLine("        public static string GetString(string name, params object[] args) {");
        output.AppendLine("            string value = GetLoader().resources.GetString(name, Culture);");
        output.AppendLine("            if (args != null && args.Length != 0) {");
        output.AppendLine("                for (int index = 0; index < args.Length; index++) {");
        output.AppendLine("                    string text = args[index] as string;");
        output.AppendLine("                    if (text != null && text.Length > 1024) {");
        output.AppendLine("                        args[index] = text.Substring(0, 1021) + \"...\";");
        output.AppendLine("                    }");
        output.AppendLine("                }");
        output.AppendLine("                return string.Format(CultureInfo.CurrentCulture, value, args);");
        output.AppendLine("            }");
        output.AppendLine("            return value;");
        output.AppendLine("        }");
        output.AppendLine();
        output.AppendLine("        public static string GetString(string name) {");
        output.AppendLine("            return GetLoader().resources.GetString(name, Culture);");
        output.AppendLine("        }");
        output.AppendLine();
        output.AppendLine("        public static string GetString(string name, out bool usedFallback) {");
        output.AppendLine("            usedFallback = false;");
        output.AppendLine("            return GetString(name);");
        output.AppendLine("        }");
        output.AppendLine();
        output.AppendLine("        public static object GetObject(string name) {");
        output.AppendLine("            return GetLoader().resources.GetObject(name, Culture);");
        output.AppendLine("        }");
        output.AppendLine("    }");
        output.AppendLine("}");
        return output.ToString();
    }

    private static string GenerateRes(SortedDictionary<string, string> resources)
    {
        StringBuilder output = GeneratedHeader("System.Web.Services.txt -> Res.g.cs");
        output.AppendLine("using System.Globalization;");
        output.AppendLine("using System.Resources;");
        output.AppendLine("using System.Threading;");
        output.AppendLine();
        output.AppendLine("namespace System.Web.Services {");
        output.AppendLine("    internal sealed class Res {");

        foreach (string name in resources.Keys)
        {
            Require(Regex.IsMatch(name, @"^[_\p{L}][\p{L}\p{Nd}_]*$", RegexOptions.CultureInvariant),
                $"Resource key '{name}' is not a valid C# identifier.");
            output.Append("        internal const string ").Append(name).Append(" = ")
                .Append(CSharpString(name)).AppendLine(";");
        }

        output.AppendLine();
        output.AppendLine("        private static Res loader;");
        output.AppendLine("        private readonly ResourceManager resources;");
        output.AppendLine("        private static CultureInfo Culture => null;");
        output.AppendLine();
        output.AppendLine("        internal Res() {");
        output.AppendLine("            resources = new ResourceManager(\"System.Web.Services\", typeof(Res).Assembly);");
        output.AppendLine("        }");
        output.AppendLine();
        output.AppendLine("        private static Res GetLoader() {");
        output.AppendLine("            if (loader == null) {");
        output.AppendLine("                Res value = new Res();");
        output.AppendLine("                Interlocked.CompareExchange(ref loader, value, null);");
        output.AppendLine("            }");
        output.AppendLine("            return loader;");
        output.AppendLine("        }");
        output.AppendLine();
        output.AppendLine("        public static string GetString(string name, params object[] args) {");
        output.AppendLine("            string value = GetLoader().resources.GetString(name, Culture);");
        output.AppendLine("            if (args != null && args.Length != 0) {");
        output.AppendLine("                return string.Format(CultureInfo.CurrentCulture, value, args);");
        output.AppendLine("            }");
        output.AppendLine("            return value;");
        output.AppendLine("        }");
        output.AppendLine();
        output.AppendLine("        public static string GetString(string name) {");
        output.AppendLine("            return GetLoader().resources.GetString(name, Culture);");
        output.AppendLine("        }");
        output.AppendLine("    }");
        output.AppendLine("}");
        return output.ToString();
    }

    private static void WriteResources(string path, SortedDictionary<string, string> resources)
    {
        using ResourceWriter writer = new(path);
        foreach ((string name, string value) in resources)
        {
            writer.AddResource(name, value);
        }
        writer.Generate();
    }

    private static string Preprocess(
        string path,
        IReadOnlyDictionary<string, string> initialObjectMacros,
        string provenance,
        IReadOnlyDictionary<string, bool>? conditionalSymbols)
    {
        Dictionary<string, string> objectMacros = new(initialObjectMacros, StringComparer.Ordinal);
        Dictionary<string, FunctionMacro> functionMacros = new(StringComparer.Ordinal);
        List<ConditionalFrame> conditionalFrames = [];
        StringBuilder output = GeneratedHeader(provenance);

        foreach (string logicalLine in ReadLogicalLines(path))
        {
            string trimmed = logicalLine.TrimStart();
            if (conditionalSymbols is not null && TryHandleConditional(
                    trimmed, conditionalSymbols, conditionalFrames))
            {
                continue;
            }

            bool active = conditionalFrames.Count == 0 || conditionalFrames[^1].CurrentActive;
            if (!active)
            {
                continue;
            }

            if (trimmed.StartsWith("#include", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.StartsWith("#define", StringComparison.Ordinal))
            {
                ParseMacro(trimmed, objectMacros, functionMacros, path);
                continue;
            }

            string rewrittenLine = RewriteConditionalDirective(logicalLine, trimmed);
            string expanded = ExpandFunctionMacros(rewrittenLine, functionMacros);
            expanded = ExpandObjectMacros(expanded, objectMacros);
            output.AppendLine(expanded);
        }

        Require(conditionalFrames.Count == 0, $"Unterminated conditional directive in {path}.");
        return output.ToString();
    }

    private static bool TryHandleConditional(
        string directive,
        IReadOnlyDictionary<string, bool> symbols,
        IList<ConditionalFrame> frames)
    {
        string? expression = null;
        if (directive.StartsWith("#ifdef ", StringComparison.Ordinal))
        {
            expression = directive["#ifdef ".Length..].Split(' ', 2)[0];
        }
        else if (directive.StartsWith("#ifndef ", StringComparison.Ordinal))
        {
            expression = "!" + directive["#ifndef ".Length..].Split(' ', 2)[0];
        }
        else if (directive.StartsWith("#if ", StringComparison.Ordinal))
        {
            expression = StripDirectiveComment(directive["#if ".Length..]);
        }

        if (expression is not null)
        {
            bool parentActive = frames.Count == 0 || frames[^1].CurrentActive;
            bool condition = EvaluateCondition(expression, symbols);
            frames.Add(new ConditionalFrame(parentActive, condition, parentActive && condition));
            return true;
        }

        if (directive.StartsWith("#elif ", StringComparison.Ordinal))
        {
            Require(frames.Count != 0, "#elif has no matching #if.");
            ConditionalFrame frame = frames[^1];
            bool condition = EvaluateCondition(
                StripDirectiveComment(directive["#elif ".Length..]), symbols);
            frame.CurrentActive = frame.ParentActive && !frame.BranchTaken && condition;
            frame.BranchTaken |= condition;
            return true;
        }

        if (directive.StartsWith("#else", StringComparison.Ordinal))
        {
            Require(frames.Count != 0, "#else has no matching #if.");
            ConditionalFrame frame = frames[^1];
            frame.CurrentActive = frame.ParentActive && !frame.BranchTaken;
            frame.BranchTaken = true;
            return true;
        }

        if (directive.StartsWith("#endif", StringComparison.Ordinal))
        {
            Require(frames.Count != 0, "#endif has no matching #if.");
            frames.RemoveAt(frames.Count - 1);
            return true;
        }

        return false;
    }

    private static string StripDirectiveComment(string expression)
    {
        int comment = expression.IndexOf("//", StringComparison.Ordinal);
        return (comment >= 0 ? expression[..comment] : expression).Trim();
    }

    private static bool EvaluateCondition(string expression, IReadOnlyDictionary<string, bool> symbols)
    {
        string trimmed = expression.Trim();
        string[] alternatives = trimmed.Split("||", StringSplitOptions.TrimEntries);
        return alternatives.Any(alternative =>
            alternative.Split("&&", StringSplitOptions.TrimEntries).All(term => EvaluateConditionTerm(term, symbols)));
    }

    private static bool EvaluateConditionTerm(string term, IReadOnlyDictionary<string, bool> symbols)
    {
        string trimmed = term.Trim().Trim('(', ')').Trim();
        bool negate = trimmed.StartsWith('!');
        string symbol = negate ? trimmed[1..].Trim() : trimmed;
        bool value = symbols.TryGetValue(symbol, out bool configured) && configured;
        return negate ? !value : value;
    }

    private static string RewriteConditionalDirective(string line, string trimmed)
    {
        int indentationLength = line.Length - trimmed.Length;
        string indentation = line[..indentationLength];
        if (trimmed.StartsWith("#ifdef ", StringComparison.Ordinal))
        {
            return indentation + "#if " + trimmed["#ifdef ".Length..];
        }
        if (trimmed.StartsWith("#ifndef ", StringComparison.Ordinal))
        {
            return indentation + "#if !" + trimmed["#ifndef ".Length..];
        }
        return line;
    }

    private static IEnumerable<string> ReadLogicalLines(string path)
    {
        StringBuilder? pending = null;
        foreach (string physicalLine in File.ReadLines(path, Encoding.UTF8))
        {
            string trimmedEnd = physicalLine.TrimEnd();
            bool continued = trimmedEnd.EndsWith('\\');
            string fragment = continued ? trimmedEnd[..^1] : physicalLine;
            if (pending is null && !continued)
            {
                yield return fragment;
                continue;
            }

            pending ??= new StringBuilder();
            pending.Append(fragment).Append(' ');
            if (!continued)
            {
                yield return pending.ToString().TrimEnd();
                pending = null;
            }
        }

        if (pending is not null)
        {
            throw new InvalidDataException($"Unterminated macro continuation in {path}.");
        }
    }

    private static void ParseMacro(
        string directive,
        IDictionary<string, string> objectMacros,
        IDictionary<string, FunctionMacro> functionMacros,
        string path)
    {
        string definition = directive["#define".Length..].TrimStart();
        Match function = Regex.Match(definition, @"^(?<name>[A-Za-z_]\w*)\((?<parameters>[^)]*)\)\s*(?<body>.*)$");
        if (function.Success)
        {
            string[] parameters = function.Groups["parameters"].Value
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            functionMacros.Add(
                function.Groups["name"].Value,
                new FunctionMacro(parameters, function.Groups["body"].Value));
            return;
        }

        Match value = Regex.Match(definition, @"^(?<name>[A-Za-z_]\w*)\s*(?<body>.*)$");
        if (!value.Success)
        {
            throw new InvalidDataException($"Unsupported macro in {path}: {directive}");
        }
        objectMacros[value.Groups["name"].Value] = value.Groups["body"].Value;
    }

    private static string ExpandFunctionMacros(string line, IReadOnlyDictionary<string, FunctionMacro> macros)
    {
        string result = line;
        for (int pass = 0; pass < 32; pass++)
        {
            bool changed = false;
            foreach ((string name, FunctionMacro macro) in macros.OrderByDescending(pair => pair.Key.Length))
            {
                int searchFrom = 0;
                while (TryFindInvocation(result, name, searchFrom, out int nameStart, out int openParenthesis))
                {
                    int closeParenthesis = FindMatchingParenthesis(result, openParenthesis);
                    string[] arguments = SplitArguments(result[(openParenthesis + 1)..closeParenthesis]);
                    if (arguments.Length != macro.Parameters.Length)
                    {
                        throw new InvalidDataException(
                            $"Macro {name} expects {macro.Parameters.Length} arguments but received {arguments.Length}.");
                    }

                    string replacement = macro.Body;
                    for (int index = 0; index < arguments.Length; index++)
                    {
                        replacement = Regex.Replace(
                            replacement,
                            $@"\b{Regex.Escape(macro.Parameters[index])}\b",
                            _ => arguments[index],
                            RegexOptions.CultureInvariant);
                    }

                    result = result[..nameStart] + replacement + result[(closeParenthesis + 1)..];
                    searchFrom = nameStart + replacement.Length;
                    changed = true;
                }
            }

            if (!changed)
            {
                return result;
            }
        }

        throw new InvalidDataException("Function macro expansion exceeded 32 passes.");
    }

    private static bool TryFindInvocation(
        string text,
        string name,
        int startIndex,
        out int nameStart,
        out int openParenthesis)
    {
        for (int index = startIndex; index <= text.Length - name.Length; index++)
        {
            if (!text.AsSpan(index, name.Length).SequenceEqual(name.AsSpan()) ||
                (index > 0 && IsIdentifierCharacter(text[index - 1])) ||
                (index + name.Length < text.Length && IsIdentifierCharacter(text[index + name.Length])))
            {
                continue;
            }

            int cursor = index + name.Length;
            while (cursor < text.Length && char.IsWhiteSpace(text[cursor]))
            {
                cursor++;
            }
            if (cursor < text.Length && text[cursor] == '(')
            {
                nameStart = index;
                openParenthesis = cursor;
                return true;
            }
        }

        nameStart = -1;
        openParenthesis = -1;
        return false;
    }

    private static int FindMatchingParenthesis(string text, int openParenthesis)
    {
        int depth = 0;
        bool inString = false;
        bool inCharacter = false;
        bool escaped = false;
        for (int index = openParenthesis; index < text.Length; index++)
        {
            char current = text[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }
            if ((inString || inCharacter) && current == '\\')
            {
                escaped = true;
                continue;
            }
            if (!inCharacter && current == '\"')
            {
                inString = !inString;
                continue;
            }
            if (!inString && current == '\'')
            {
                inCharacter = !inCharacter;
                continue;
            }
            if (inString || inCharacter)
            {
                continue;
            }
            if (current == '(')
            {
                depth++;
            }
            else if (current == ')' && --depth == 0)
            {
                return index;
            }
        }

        throw new InvalidDataException("Unbalanced macro invocation.");
    }

    private static string[] SplitArguments(string arguments)
    {
        if (arguments.Trim().Length == 0)
        {
            return [];
        }

        List<string> result = [];
        int start = 0;
        int depth = 0;
        bool inString = false;
        bool inCharacter = false;
        bool escaped = false;
        for (int index = 0; index < arguments.Length; index++)
        {
            char current = arguments[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }
            if ((inString || inCharacter) && current == '\\')
            {
                escaped = true;
                continue;
            }
            if (!inCharacter && current == '\"')
            {
                inString = !inString;
                continue;
            }
            if (!inString && current == '\'')
            {
                inCharacter = !inCharacter;
                continue;
            }
            if (inString || inCharacter)
            {
                continue;
            }
            if (current == '(')
            {
                depth++;
            }
            else if (current == ')')
            {
                depth--;
            }
            else if (current == ',' && depth == 0)
            {
                result.Add(arguments[start..index].Trim());
                start = index + 1;
            }
        }
        result.Add(arguments[start..].Trim());
        return result.ToArray();
    }

    private static string ExpandObjectMacros(string line, IReadOnlyDictionary<string, string> macros)
    {
        string result = line;
        foreach ((string name, string value) in macros.OrderByDescending(pair => pair.Key.Length))
        {
            result = Regex.Replace(
                result,
                $@"\b{Regex.Escape(name)}\b",
                _ => value,
                RegexOptions.CultureInvariant);
        }
        return result;
    }

    private static string GenerateRegularExpressions(RegularExpressionsManifest manifest)
    {
        StringBuilder output = GeneratedHeader(
            "pinned microsoft/referencesource regcomp/RegexPreCompiler.cs -> RegularExpressions.g.cs");
        output.AppendLine("using System;");
        output.AppendLine("using System.Text.RegularExpressions;");
        output.AppendLine();
        output.Append("namespace ").Append(manifest.Namespace).AppendLine(" {");
        foreach (RegularExpressionDefinition definition in manifest.Regexes)
        {
            _ = new Regex(definition.Pattern, ParseRegexOptions(definition.Options), Regex.InfiniteMatchTimeout);
            string visibility = definition.IsPublic ? "public" : "internal";
            string options = string.Join(
                " | ",
                definition.Options.Select(option => "RegexOptions." + option));
            string compiledOptions = options + " | RegexOptions.Compiled";
            output.Append("    ").Append(visibility).Append(" class ").Append(definition.Name)
                .AppendLine(" : Regex {");
            output.Append("        public ").Append(definition.Name).Append("() : base(")
                .Append(CSharpString(definition.Pattern)).Append(", ").Append(compiledOptions)
                .AppendLine(", Regex.InfiniteMatchTimeout) { }");
            output.Append("        public ").Append(definition.Name).Append("(TimeSpan matchTimeout) : base(")
                .Append(CSharpString(definition.Pattern)).Append(", ").Append(compiledOptions)
                .AppendLine(", matchTimeout) { }");
            output.AppendLine("    }");
            output.AppendLine();
        }
        output.AppendLine("}");
        return output.ToString();
    }

    private static RegexOptions ParseRegexOptions(IEnumerable<string> options)
    {
        RegexOptions result = RegexOptions.None;
        foreach (string option in options)
        {
            result |= Enum.Parse<RegexOptions>(option, ignoreCase: false);
        }
        return result;
    }

    private static void VerifyResources(string path, SortedDictionary<string, string> expected)
    {
        Dictionary<string, string> actual = new(StringComparer.Ordinal);
        using ResourceReader reader = new(path);
        foreach (DictionaryEntry entry in reader)
        {
            actual.Add((string)entry.Key, (string)entry.Value!);
        }
        Require(actual.Count == expected.Count, "Resource count differs from System.Web.txt.");
        foreach ((string name, string value) in expected)
        {
            Require(actual.TryGetValue(name, out string? actualValue) && actualValue == value,
                $"Resource '{name}' does not match System.Web.txt.");
        }
        Require(actual["Invalid_file_name_for_monitoring"].Contains("\r\n- The filename", StringComparison.Ordinal),
            "Resource newline escapes were not decoded.");
        Require(actual["ADMembership_UPN_contains_backslash"].Contains("'\\'", StringComparison.Ordinal),
            "Resource backslash escapes were not decoded.");
        Require(actual["Invalid_status_string"] == "HTTP status string is not valid.",
            "ResGen-compatible leading whitespace was not removed from Invalid_status_string.");
        Require(actual["DataList_TemplateTableNotFound"].StartsWith("A Table control", StringComparison.Ordinal),
            "ResGen-compatible leading whitespace was not removed from DataList_TemplateTableNotFound.");
        Require(actual["TableCell_AssociatedHeaderCellID"].StartsWith("Lists the header", StringComparison.Ordinal),
            "ResGen-compatible leading whitespace was not removed from TableCell_AssociatedHeaderCellID.");
    }

    private static void VerifySr(
        string path,
        SortedDictionary<string, string> resources,
        IdentityManifest identities)
    {
        string source = File.ReadAllText(path, Encoding.UTF8);
        int constantCount = Regex.Matches(source, "internal const string \\w+ = \\\"", RegexOptions.CultureInvariant).Count;
        Require(constantCount == resources.Count, "SR constant count does not match the resource key count.");
        Require(source.Contains($"new ResourceManager(\"{identities.Resource.BaseName}\"", StringComparison.Ordinal),
            "SR uses an unexpected resource base name.");
        Require(!source.Contains("Portable.System.Web", StringComparison.Ordinal), "SR inherited the POC resource identity.");
    }

    private static void VerifyServicesResources(string path, SortedDictionary<string, string> expected)
    {
        Dictionary<string, string> actual = new(StringComparer.Ordinal);
        using ResourceReader reader = new(path);
        foreach (DictionaryEntry entry in reader)
        {
            actual.Add((string)entry.Key, (string)entry.Value!);
        }
        Require(actual.Count == expected.Count, "Resource count differs from System.Web.Services.txt.");
        foreach ((string name, string value) in expected)
        {
            Require(actual.TryGetValue(name, out string? actualValue) && actualValue == value,
                $"Resource '{name}' does not match System.Web.Services.txt.");
        }
    }

    private static void VerifyRes(string path, SortedDictionary<string, string> resources)
    {
        string source = File.ReadAllText(path, Encoding.UTF8);
        int constantCount = Regex.Matches(source, "internal const string \\w+ = \\\"", RegexOptions.CultureInvariant).Count;
        Require(constantCount == resources.Count, "Res constant count does not match the resource key count.");
        Require(source.Contains("new ResourceManager(\"System.Web.Services\"", StringComparison.Ordinal),
            "Res uses an unexpected resource base name.");
    }

    private static void VerifyAssemblyRef(string path, string sourceRoot, IdentityManifest identities)
    {
        string generated = File.ReadAllText(path, Encoding.UTF8);
        foreach ((string name, string value) in identities.AssemblyRefConstants)
        {
            Require(generated.Contains($"const string {name} = {CSharpString(value)};", StringComparison.Ordinal),
                $"AssemblyRef.{name} does not match its manifest value.");
        }
        Require(!generated.Contains("Portable.", StringComparison.Ordinal), "AssemblyRef inherited a POC identity.");

        HashSet<string> referencedConstants = [];
        foreach (string sourcePath in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match match in Regex.Matches(
                         File.ReadAllText(sourcePath, Encoding.UTF8),
                         @"AssemblyRef\.(?<name>[A-Za-z_]\w*)",
                         RegexOptions.CultureInvariant))
            {
                referencedConstants.Add(match.Groups["name"].Value);
            }
        }
        Require(referencedConstants.SetEquals(identities.AssemblyRefConstants.Keys),
            "The assembly identity manifest does not exactly cover Reference Source AssemblyRef usages.");
    }

    private static void VerifyModName(string path)
    {
        string source = File.ReadAllText(path, Encoding.UTF8);
        string[] required =
        [
            "ENGINE_FULL_NAME      = \"webengine4.dll\"",
            "ISAPI_FULL_NAME      = \"aspnet_isapi.dll\"",
            "STATE_FULL_NAME      = \"aspnet_state.exe\"",
            "WEB_FULL_NAME        = \"System.Web.dll\"",
            "MGDENG_FULL_NAME     = \"webengine4.dll\"",
            "REG_MACHINE_APP      = \"Software\\\\Microsoft\\\\ASP.NET\"",
        ];
        foreach (string expected in required)
        {
            Require(source.Contains(expected, StringComparison.Ordinal), $"ModName is missing '{expected}'.");
        }
        Require(!source.Contains("ROTORTODO", StringComparison.Ordinal),
            "The desktop-framework-windows ModName output must not retain FEATURE_PAL placeholders.");
        Require(!source.Contains("#include", StringComparison.Ordinal), "ModName still contains an include directive.");
        Require(!source.Contains("#if", StringComparison.Ordinal), "ModName target-profile conditionals were not resolved.");
    }

    private static void VerifyCacheSources(string usagePath, string expiresPath)
    {
        string usage = File.ReadAllText(usagePath, Encoding.UTF8);
        string expires = File.ReadAllText(expiresPath, Encoding.UTF8);
        string[] usageSymbols = ["struct UsageEntryRef", "sealed class UsageBucket", "class CacheUsage"];
        string[] expiresSymbols = ["struct ExpiresEntryRef", "sealed class ExpiresBucket", "sealed class CacheExpires"];
        foreach (string symbol in usageSymbols)
        {
            Require(usage.Contains(symbol, StringComparison.Ordinal), $"CacheUsage output is missing {symbol}.");
        }
        foreach (string symbol in expiresSymbols)
        {
            Require(expires.Contains(symbol, StringComparison.Ordinal), $"CacheExpires output is missing {symbol}.");
        }
        foreach (string macro in new[]
                 {
                     "#define", "EntriesI(", "EntriesR(", "PagePrev(", "PageNext(", "FreeEntryHead(",
                     "FreeEntryCount(", "CreateRef1(", "CreateRef2(", "SetLastRefNext(", "SetLastRefPrev(",
                 })
        {
            Require(!usage.Contains(macro, StringComparison.Ordinal), $"CacheUsage output retains macro {macro}.");
            Require(!expires.Contains(macro, StringComparison.Ordinal), $"CacheExpires output retains macro {macro}.");
        }
    }

    private static void VerifyRegularExpressions(string path, RegularExpressionsManifest manifest)
    {
        string source = File.ReadAllText(path, Encoding.UTF8);
        foreach (RegularExpressionDefinition definition in manifest.Regexes)
        {
            string visibility = definition.IsPublic ? "public" : "internal";
            string options = string.Join(
                " | ",
                definition.Options.Select(option => "RegexOptions." + option));
            string compiledOptions = options + " | RegexOptions.Compiled";
            Require(source.Contains($"{visibility} class {definition.Name} : Regex", StringComparison.Ordinal),
                $"Regex type {definition.Name} has an unexpected visibility or base type.");
            Require(source.Contains(
                    $"public {definition.Name}() : base({CSharpString(definition.Pattern)}, {compiledOptions}, Regex.InfiniteMatchTimeout)",
                    StringComparison.Ordinal),
                $"Regex type {definition.Name} has an unexpected default constructor.");
            Require(source.Contains(
                    $"public {definition.Name}(TimeSpan matchTimeout) : base({CSharpString(definition.Pattern)}, {compiledOptions}, matchTimeout)",
                    StringComparison.Ordinal),
                $"Regex type {definition.Name} has an unexpected timeout constructor.");
            Regex compiled = new(definition.Pattern, ParseRegexOptions(definition.Options), TimeSpan.FromSeconds(1));
            Require(compiled.ToString() == definition.Pattern, $"Regex pattern {definition.Name} did not round-trip.");
        }
        Require(!source.Contains("GeneratedRegex", StringComparison.Ordinal),
            "The generated sources must preserve the original constructible regex type symbols.");
    }

    private static StringBuilder GeneratedHeader(string provenance)
    {
        StringBuilder output = new();
        output.AppendLine("// <auto-generated>");
        output.Append("// Deterministic Rehost build input generated from ").Append(provenance).AppendLine(".");
        output.AppendLine("// Do not edit this intermediate output.");
        output.AppendLine("// </auto-generated>");
        output.AppendLine();
        return output;
    }

    private static string CSharpString(string value)
    {
        StringBuilder output = new(value.Length + 2);
        output.Append('\"');
        foreach (char current in value)
        {
            output.Append(current switch
            {
                '\\' => "\\\\",
                '\"' => "\\\"",
                '\r' => "\\r",
                '\n' => "\\n",
                '\t' => "\\t",
                _ => current.ToString(),
            });
        }
        output.Append('\"');
        return output.ToString();
    }

    private static bool IsIdentifierCharacter(char value) => value == '_' || char.IsLetterOrDigit(value);

    private static void WriteText(string path, string content) => File.WriteAllText(path, content, Utf8WithoutBom);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }

    private sealed record FunctionMacro(string[] Parameters, string Body);

    private sealed class ConditionalFrame(bool parentActive, bool branchTaken, bool currentActive)
    {
        public bool ParentActive { get; } = parentActive;
        public bool BranchTaken { get; set; } = branchTaken;
        public bool CurrentActive { get; set; } = currentActive;
    }

    private sealed record IdentityManifest(
        AssemblyIdentity OutputAssembly,
        AssemblyIdentity LegacyMicrosoftIdentity,
        ResourceIdentity Resource,
        Dictionary<string, string> AssemblyRefConstants);

    private sealed record AssemblyIdentity(
        string Name,
        string? Version,
        string Culture,
        string? PublicKeyToken,
        bool StrongNamed = true);

    private sealed record ResourceIdentity(string BaseName, string LogicalName, string Culture);

    private sealed record NativeNamesManifest(
        string Include,
        string TargetProfile,
        Dictionary<string, bool> Symbols,
        Dictionary<string, string> Macros);

    private sealed record RegularExpressionsManifest(
        string Namespace,
        string SourceType,
        List<RegularExpressionDefinition> Regexes);

    private sealed record RegularExpressionDefinition(
        string Name,
        string Pattern,
        List<string> Options,
        bool IsPublic);
}
