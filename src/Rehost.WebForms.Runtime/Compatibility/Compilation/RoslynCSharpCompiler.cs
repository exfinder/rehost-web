using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace System.Web.Compilation;

internal sealed class RoslynCSharpCompiler : ICodeCompiler
{
    private const string SourceExtension = ".cs";
    private const string MetadataFileNotFound = "CS0006";

    // AssemblyBuilder.FixUpCompilerParameters adds these, but only when the provider type is
    // exactly Microsoft.CSharp.CSharpCodeProvider, which a derived provider never matches.
    // They are prepended so an application's own compilerOptions still win.
    private static readonly string[] AspNetCompilerOptions = ["/nowarn:1659;1699;1701;612;618"];

    private static readonly Lazy<MetadataReference[]> SharedFrameworkReferences =
        new(LoadSharedFrameworkReferences);

    private readonly CodeDomProvider _codeDomProvider;

    internal RoslynCSharpCompiler(CodeDomProvider codeDomProvider)
    {
        _codeDomProvider = codeDomProvider;
    }

    public CompilerResults CompileAssemblyFromDom(CompilerParameters options, CodeCompileUnit compilationUnit)
    {
        ArgumentNullException.ThrowIfNull(compilationUnit);

        return CompileAssemblyFromDomBatch(options, [compilationUnit]);
    }

    public CompilerResults CompileAssemblyFromDomBatch(CompilerParameters options, CodeCompileUnit[] compilationUnits)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(compilationUnits);

        var sources = new string[compilationUnits.Length];
        for (var i = 0; i < compilationUnits.Length; i++)
        {
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            _codeDomProvider.GenerateCodeFromCompileUnit(compilationUnits[i], writer, new CodeGeneratorOptions());
            sources[i] = writer.ToString();
        }

        return CompileAssemblyFromSourceBatch(options, sources);
    }

    public CompilerResults CompileAssemblyFromFile(CompilerParameters options, string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        return CompileAssemblyFromFileBatch(options, [fileName]);
    }

    public CompilerResults CompileAssemblyFromFileBatch(CompilerParameters options, string[] fileNames)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(fileNames);

        try
        {
            return Compile(options, fileNames);
        }
        finally
        {
            options.TempFiles.Delete();
        }
    }

    public CompilerResults CompileAssemblyFromSource(CompilerParameters options, string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return CompileAssemblyFromSourceBatch(options, [source]);
    }

    public CompilerResults CompileAssemblyFromSourceBatch(CompilerParameters options, string[] sources)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(sources);

        try
        {
            var fileNames = new string[sources.Length];
            for (var i = 0; i < sources.Length; i++)
            {
                fileNames[i] = options.TempFiles.AddExtension(i + SourceExtension);
                File.WriteAllText(fileNames[i], sources[i], Encoding.UTF8);
            }

            return Compile(options, fileNames);
        }
        finally
        {
            options.TempFiles.Delete();
        }
    }

    private static CompilerResults Compile(CompilerParameters options, string[] fileNames)
    {
        var results = new CompilerResults(options.TempFiles);

        if (string.IsNullOrEmpty(options.OutputAssembly))
        {
            options.OutputAssembly = options.TempFiles.AddExtension("dll", !options.GenerateInMemory);
        }

        var outputAssembly = options.OutputAssembly;
        var arguments = ParseCompilerOptions(options, fileNames);

        foreach (var diagnostic in arguments.Errors)
        {
            AddDiagnostic(results, diagnostic);
        }

        if (results.Errors.HasErrors)
        {
            results.NativeCompilerReturnValue = 1;
            return results;
        }

        var syntaxTrees = CreateSyntaxTrees(options, arguments, fileNames);
        var references = CreateReferences(options, results);

        if (results.Errors.HasErrors)
        {
            results.NativeCompilerReturnValue = 1;
            return results;
        }

        var compilation = CSharpCompilation.Create(
            Path.GetFileNameWithoutExtension(outputAssembly),
            syntaxTrees,
            references,
            arguments.CompilationOptions
                .WithOutputKind(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(
                    options.IncludeDebugInformation ? OptimizationLevel.Debug : OptimizationLevel.Release));

        var outputDirectory = Path.GetDirectoryName(outputAssembly);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var symbolFile = options.IncludeDebugInformation ? Path.ChangeExtension(outputAssembly, ".pdb") : null;
        EmitResult emitResult;

        using (var peStream = File.Create(outputAssembly))
        using (var pdbStream = symbolFile is null ? null : File.Create(symbolFile))
        using (var win32Resources = OpenWin32Resources(options))
        {
            emitResult = compilation.Emit(
                peStream,
                pdbStream,
                win32Resources: win32Resources,
                manifestResources: CreateManifestResources(options));
        }

        foreach (var diagnostic in emitResult.Diagnostics)
        {
            AddDiagnostic(results, diagnostic);
        }

        results.NativeCompilerReturnValue = emitResult.Success ? 0 : 1;

        if (emitResult.Success)
        {
            results.PathToAssembly = outputAssembly;
        }
        else
        {
            // A failed emit leaves an unloadable file behind, which the next request would
            // otherwise find and treat as a usable build result.
            Delete(outputAssembly);
            Delete(symbolFile);
        }

        return results;
    }

    private static CSharpCommandLineArguments ParseCompilerOptions(CompilerParameters options, string[] fileNames)
    {
        var arguments = new List<string>();

        if (options.WarningLevel >= 0)
        {
            arguments.Add("/warn:" + options.WarningLevel.ToString(CultureInfo.InvariantCulture));
        }

        arguments.AddRange(AspNetCompilerOptions);

        if (!string.IsNullOrWhiteSpace(options.CompilerOptions))
        {
            arguments.AddRange(CommandLineParser.SplitCommandLineIntoArguments(
                options.CompilerOptions,
                removeHashComments: false));
        }

        // CompilerParameters.TreatWarningsAsErrors is deliberately not honoured: the <compiler>
        // configuration element sets it from warningLevel > 0, so the configured warningLevel="4"
        // would make every warning fatal. Framework corrected this in
        // AssemblyBuilder.FixTreatWarningsAsErrors, which only runs for the built-in provider
        // types. Warn-as-error therefore comes from compilerOptions alone, as csc treats it.
        arguments.AddRange(fileNames);

        return CSharpCommandLineParser.Default.Parse(
            arguments,
            baseDirectory: Path.GetDirectoryName(options.OutputAssembly) ?? AppContext.BaseDirectory,
            sdkDirectory: null);
    }

    private static SyntaxTree[] CreateSyntaxTrees(
        CompilerParameters options,
        CSharpCommandLineArguments arguments,
        string[] fileNames)
    {
        var parseOptions = arguments.ParseOptions;

        if (options.IncludeDebugInformation)
        {
            parseOptions = parseOptions.WithPreprocessorSymbols(
                parseOptions.PreprocessorSymbolNames.Append("DEBUG").Distinct(StringComparer.Ordinal));
        }

        var syntaxTrees = new SyntaxTree[fileNames.Length];
        for (var i = 0; i < fileNames.Length; i++)
        {
            // The encoding must be supplied for the emitted debug information to be usable.
            var text = SourceText.From(File.ReadAllText(fileNames[i]), Encoding.UTF8);
            syntaxTrees[i] = CSharpSyntaxTree.ParseText(text, parseOptions, fileNames[i]);
        }

        return syntaxTrees;
    }

    private static List<MetadataReference> CreateReferences(CompilerParameters options, CompilerResults results)
    {
        var references = new List<MetadataReference>(SharedFrameworkReferences.Value);
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var reference in references)
        {
            if (reference is PortableExecutableReference { FilePath: { Length: > 0 } path })
            {
                resolved.Add(path);
            }
        }

        foreach (string reference in options.ReferencedAssemblies)
        {
            if (string.IsNullOrEmpty(reference) || !resolved.Add(reference))
            {
                continue;
            }

            if (!File.Exists(reference))
            {
                results.Errors.Add(new CompilerError
                {
                    ErrorNumber = MetadataFileNotFound,
                    ErrorText = string.Format(
                        CultureInfo.CurrentCulture,
                        "Metadata file '{0}' could not be found.",
                        reference),
                });
                continue;
            }

            references.Add(MetadataReference.CreateFromFile(reference));
        }

        return references;
    }

    private static MetadataReference[] LoadSharedFrameworkReferences()
    {
        // The shared framework of the running runtime stands in for the framework assemblies a
        // Framework application inherited through the machine <compilation><assemblies> list.
        var directory = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (string.IsNullOrEmpty(directory))
        {
            return [];
        }

        var references = new List<MetadataReference>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.dll"))
        {
            if (HasManagedMetadata(path))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        return [.. references];
    }

    // The shared framework ships native libraries beside managed assemblies, and only on Windows
    // do they carry the .dll extension. MetadataReference.CreateFromFile defers reading the image,
    // so an unmanaged file is rejected at compilation instead, as CS0009 against every reference.
    private static bool HasManagedMetadata(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var peReader = new PEReader(stream);

            return peReader.HasMetadata;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static IEnumerable<ResourceDescription> CreateManifestResources(CompilerParameters options)
    {
        var resources = new List<ResourceDescription>();

        foreach (string resource in options.EmbeddedResources)
        {
            if (string.IsNullOrEmpty(resource) || !File.Exists(resource))
            {
                continue;
            }

            var path = resource;
            resources.Add(new ResourceDescription(Path.GetFileName(path), () => File.OpenRead(path), isPublic: true));
        }

        return resources;
    }

    private static Stream OpenWin32Resources(CompilerParameters options) =>
        string.IsNullOrEmpty(options.Win32Resource) || !File.Exists(options.Win32Resource)
            ? null
            : File.OpenRead(options.Win32Resource);

    private static void AddDiagnostic(CompilerResults results, Diagnostic diagnostic)
    {
        if (diagnostic.Severity < DiagnosticSeverity.Warning)
        {
            return;
        }

        results.Errors.Add(CreateError(diagnostic));
        results.Output.Add(diagnostic.ToString());
    }

    private static CompilerError CreateError(Diagnostic diagnostic)
    {
        // The mapped span applies the #line pragmas the code generators emit, so the error
        // identifies the .aspx the developer wrote rather than the generated source.
        var span = diagnostic.Location.GetMappedLineSpan();
        var located = !string.IsNullOrEmpty(span.Path);

        return new CompilerError
        {
            FileName = located ? span.Path : string.Empty,
            Line = located ? span.StartLinePosition.Line + 1 : 0,
            Column = located ? span.StartLinePosition.Character + 1 : 0,
            ErrorNumber = diagnostic.Id,
            ErrorText = diagnostic.GetMessage(CultureInfo.CurrentCulture),
            IsWarning = diagnostic.Severity == DiagnosticSeverity.Warning,
        };
    }

    private static void Delete(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
