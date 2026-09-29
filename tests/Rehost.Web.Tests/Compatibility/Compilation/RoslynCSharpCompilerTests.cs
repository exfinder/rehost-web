using System.CodeDom.Compiler;
using System.Web.Compilation;
using Rehost.Web.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests.Compatibility.Compilation;

public sealed class RoslynCSharpCompilerTests
{
    [Fact]
    public void Emits_An_Assembly_To_The_Requested_Output_Path()
    {
        using var scope = new CompilationScope();
        scope.AddSource("public class Page { public int Value => 42; }");

        var results = scope.Compile();

        results.Errors.HasErrors.ShouldBeFalse();
        results.NativeCompilerReturnValue.ShouldBe(0);
        results.PathToAssembly.ShouldBe(scope.OutputAssembly);
        File.Exists(scope.OutputAssembly).ShouldBeTrue();
    }

    [Fact]
    public void Maps_Diagnostics_Through_Line_Pragmas_To_The_Originating_Page()
    {
        using var scope = new CompilationScope();
        scope.AddSource(
            """
            public class Page
            {
                public void Render()
                {

            #line 42 "/app/Default.aspx"
                    this.Missing();
            #line default
                }
            }
            """);

        var results = scope.Compile();

        var error = results.Errors.Cast<CompilerError>().Single(e => !e.IsWarning);
        error.FileName.ShouldBe("/app/Default.aspx");
        error.Line.ShouldBe(42);
        error.ErrorNumber.ShouldBe("CS1061");
    }

    [Fact]
    public void Reports_Unresolved_Types_With_The_Compiler_Error_Number()
    {
        using var scope = new CompilationScope();
        scope.AddSource("public class Page { public Nonexistent Value; }");

        var results = scope.Compile();

        results.Errors.HasErrors.ShouldBeTrue();
        results.Errors.Cast<CompilerError>().ShouldContain(e => e.ErrorNumber == "CS0246");
        results.PathToAssembly.ShouldBeNull();
        File.Exists(scope.OutputAssembly).ShouldBeFalse();
    }

    [Fact]
    public void Reports_Warnings_Without_Failing_And_Without_Informational_Noise()
    {
        using var scope = new CompilationScope();
        scope.AddSource("public class Page { public void Render() { int unused = 1; } }");

        var results = scope.Compile();

        results.Errors.HasErrors.ShouldBeFalse();
        var warning = results.Errors.Cast<CompilerError>().ShouldHaveSingleItem();
        warning.IsWarning.ShouldBeTrue();
        warning.ErrorNumber.ShouldBe("CS0219");
    }

    [Fact]
    public void Configured_Warning_Level_Does_Not_Make_Warnings_Fatal()
    {
        using var scope = new CompilationScope();
        scope.AddSource("public class Page { public void Render() { int unused = 1; } }");

        // The <compiler> configuration element sets both of these together.
        scope.Parameters.WarningLevel = 4;
        scope.Parameters.TreatWarningsAsErrors = true;

        var results = scope.Compile();

        results.Errors.HasErrors.ShouldBeFalse();
        results.NativeCompilerReturnValue.ShouldBe(0);
    }

    [Fact]
    public void Honours_Warnaserror_From_Compiler_Options()
    {
        using var scope = new CompilationScope();
        scope.AddSource("public class Page { public void Render() { int unused = 1; } }");
        scope.Parameters.CompilerOptions = "/warnaserror+";

        var results = scope.Compile();

        results.Errors.HasErrors.ShouldBeTrue();
    }

    [Fact]
    public void Writes_Compiler_Formatted_Output_Lines()
    {
        using var scope = new CompilationScope();
        scope.AddSource(
            """
            public class Page
            {
            #line 7 "/app/Default.aspx"
                public Nonexistent Value;
            #line default
            }
            """);

        var results = scope.Compile();

        results.Output.Cast<string>().ShouldContain(
            line => line.StartsWith("/app/Default.aspx(7,", StringComparison.Ordinal)
                && line.Contains(": error CS0246: ", StringComparison.Ordinal));
    }

    [Fact]
    public void Honours_Preprocessor_Symbols_From_Compiler_Options()
    {
        using var scope = new CompilationScope();
        scope.AddSource(
            """
            public class Page
            {
            #if !FEATURE_ON
            #error FEATURE_ON was not defined
            #endif
            }
            """);
        scope.Parameters.CompilerOptions = "/define:FEATURE_ON";

        scope.Compile().Errors.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void Honours_Suppressed_Warnings_From_Compiler_Options()
    {
        using var scope = new CompilationScope();
        scope.AddSource("public class Page { public void Render() { int unused = 1; } }");
        scope.Parameters.CompilerOptions = "/nowarn:219";

        scope.Compile().Errors.Count.ShouldBe(0);
    }

    [Fact]
    public void Honours_The_Configured_Language_Version()
    {
        const string switchExpression =
            """
            public class Page
            {
                public string Describe(int value) => value switch { 1 => "one", _ => "other" };
            }
            """;

        using (var scope = new CompilationScope())
        {
            scope.AddSource(switchExpression);
            scope.Parameters.CompilerOptions = "/langversion:7.3";

            scope.Compile().Errors.HasErrors.ShouldBeTrue();
        }

        using (var scope = new CompilationScope())
        {
            scope.AddSource(switchExpression);
            scope.Parameters.CompilerOptions = "/langversion:latest";

            scope.Compile().Errors.HasErrors.ShouldBeFalse();
        }
    }

    [Fact]
    public void Implicit_References_Cover_The_Framework_Surface_A_Page_Uses()
    {
        using var scope = new CompilationScope();
        scope.AddSource(
            """
            using System;
            using System.Collections;
            using System.Collections.Generic;
            using System.Collections.Specialized;
            using System.ComponentModel;
            using System.ComponentModel.DataAnnotations;
            using System.Data;
            using System.Globalization;
            using System.IO;
            using System.Linq;
            using System.Text;
            using System.Text.RegularExpressions;
            using System.Threading.Tasks;
            using System.Xml;
            using System.Xml.Linq;

            public class Page
            {
                [Required] public string Name { get; set; }

                public object Render()
                {
                    var table = new DataTable("t");
                    table.Columns.Add("c", typeof(int));
                    var values = new NameValueCollection { { "a", "b" } };
                    var numbers = new List<int> { 1, 2, 3 }.Where(i => i > 1).ToArray();
                    var document = XDocument.Parse("<r/>");
                    var xml = new XmlDocument();
                    var text = new StringBuilder().AppendFormat(
                        CultureInfo.InvariantCulture, "{0}", numbers.Length).ToString();
                    var matched = Regex.IsMatch(text, "[0-9]");
                    var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
                    var task = Task.FromResult(table.TableName);
                    var converter = TypeDescriptor.GetConverter(typeof(int));
                    IEnumerable sequence = numbers;

                    return new object[]
                    {
                        table, values, document, xml, matched, stream, task, converter, sequence, Name,
                    };
                }
            }
            """);

        var results = scope.Compile();

        results.Errors.HasErrors.ShouldBeFalse(
            string.Join(Environment.NewLine, results.Output.Cast<string>()));
    }

    [Fact]
    public void Honours_Referenced_Assemblies()
    {
        using var scope = new CompilationScope();
        scope.AddSource(
            """
            using Rehost.Web.Hosting;

            public class Page
            {
                public RehostWebOptions Options = new RehostWebOptions();
            }
            """);

        // Nothing outside the shared framework is referenced implicitly.
        scope.Compile().Errors.Cast<CompilerError>().ShouldContain(e => e.ErrorNumber == "CS0246");

        scope.Parameters.ReferencedAssemblies.Add(typeof(RehostWebOptions).Assembly.Location);

        scope.Compile().Errors.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void The_Port_Is_The_Only_Supplier_Of_The_System_Web_Namespaces()
    {
        const string source =
            """
            public class Page
            {
                public string Encoded => System.Web.HttpUtility.HtmlEncode("a<b");
            }
            """;

        // Without the port referenced, HttpUtility must be unresolved: CS1069 is the facades'
        // dangling type-forward, proving the shared framework's System.Web.HttpUtility.dll is
        // out of the set — with it referenced, this compiles.
        using (var scope = new CompilationScope())
        {
            scope.AddSource(source);

            scope.Compile().Errors.Cast<CompilerError>()
                .ShouldContain(e => e.ErrorNumber == "CS1069");
        }

        // With the port referenced there is exactly one supplier; two would be CS0433.
        using (var scope = new CompilationScope())
        {
            scope.AddSource(source);
            scope.Parameters.ReferencedAssemblies.Add(typeof(System.Web.HttpContext).Assembly.Location);

            scope.Compile().Errors.HasErrors.ShouldBeFalse();
        }
    }

    // Guards sufficiency of the exclusion list in LoadSharedFrameworkReferences: a shared
    // framework update that adds a type the port also defines, or a port type whose name lands
    // in a shared assembly, reintroduces CS0433 for every page naming it. Measured on net10,
    // System.Web.HttpUtility.dll is the only definer (System.Web.dll only forwards to it).
    [Fact]
    public void No_Unexcluded_Shared_Framework_Assembly_Defines_A_Type_The_Port_Defines()
    {
        string[] excluded = ["System.Web.dll", "System.Web.HttpUtility.dll"];

        var portTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assembly in Directory.GetFiles(AppContext.BaseDirectory, "Rehost.*.dll"))
        {
            portTypes.UnionWith(PublicTypeNames(assembly));
        }
        portTypes.ShouldNotBeEmpty();

        var sharedFramework = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var collisions = Directory.GetFiles(sharedFramework, "*.dll")
            .Where(path => !excluded.Contains(Path.GetFileName(path)))
            .SelectMany(path => PublicTypeNames(path)
                .Where(portTypes.Contains)
                .Select(type => Path.GetFileName(path) + ": " + type))
            .ToList();

        collisions.ShouldBeEmpty();
    }

    private static IEnumerable<string> PublicTypeNames(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new System.Reflection.PortableExecutable.PEReader(stream);
        if (!peReader.HasMetadata)
        {
            yield break;
        }

        var metadata = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(peReader);
        foreach (var handle in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(handle);
            if ((type.Attributes & System.Reflection.TypeAttributes.VisibilityMask)
                != System.Reflection.TypeAttributes.Public)
            {
                continue;
            }

            var typeNamespace = metadata.GetString(type.Namespace);
            if (typeNamespace.Length > 0)
            {
                yield return typeNamespace + "." + metadata.GetString(type.Name);
            }
        }
    }

    [Fact]
    public void Reports_A_Referenced_Assembly_That_Does_Not_Exist()
    {
        using var scope = new CompilationScope();
        scope.AddSource("public class Page { }");
        scope.Parameters.ReferencedAssemblies.Add(Path.Combine(scope.DirectoryPath, "Absent.dll"));

        var results = scope.Compile();

        results.Errors.HasErrors.ShouldBeTrue();
        results.Errors.Cast<CompilerError>().ShouldContain(e => e.ErrorNumber == "CS0006");
    }

    [Fact]
    public void Debug_Information_Emits_Symbols_And_Defines_The_Debug_Symbol()
    {
        const string requiresDebug =
            """
            public class Page
            {
            #if !DEBUG
            #error DEBUG was not defined
            #endif
            }
            """;

        using (var scope = new CompilationScope())
        {
            scope.AddSource(requiresDebug);
            scope.Parameters.IncludeDebugInformation = true;

            scope.Compile().Errors.HasErrors.ShouldBeFalse();
            File.Exists(Path.ChangeExtension(scope.OutputAssembly, ".pdb")).ShouldBeTrue();
        }

        using (var scope = new CompilationScope())
        {
            scope.AddSource(requiresDebug);

            scope.Compile().Errors.HasErrors.ShouldBeTrue();
            File.Exists(Path.ChangeExtension(scope.OutputAssembly, ".pdb")).ShouldBeFalse();
        }
    }

    [Fact]
    public void Win32_Resources_Are_Reported_Unsupported_So_Literals_Stay_In_Metadata()
    {
        var provider = new RoslynCSharpCodeProvider();

        provider.Supports(GeneratorSupport.Win32Resources).ShouldBeFalse();

        // Everything else the built-in provider offers must still be reported.
        foreach (var capability in Enum.GetValues<GeneratorSupport>())
        {
            if (capability != GeneratorSupport.Win32Resources)
            {
                provider.Supports(capability)
                    .ShouldBe(new Microsoft.CSharp.CSharpCodeProvider().Supports(capability), capability.ToString());
            }
        }
    }

    [Fact]
    public void Visual_Basic_Compilation_Is_Explicitly_Unsupported()
    {
        var provider = new UnsupportedVBCodeProvider();

        var exception = Should.Throw<PlatformNotSupportedException>(
            () => provider.CompileAssemblyFromFile(new CompilerParameters(), "Default.aspx.vb"));

        exception.Message.ShouldContain("Visual Basic", Case.Sensitive);
        exception.Message.ShouldContain("not supported");
    }

    private sealed class CompilationScope : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("rehost-compiler-");
        private readonly List<string> _sourceFiles = [];

        internal CompilationScope()
        {
            Parameters = new CompilerParameters
            {
                OutputAssembly = Path.Combine(_directory.FullName, "App_Web_test.dll"),
            };
        }

        internal CompilerParameters Parameters { get; }

        internal string DirectoryPath => _directory.FullName;

        internal string OutputAssembly => Parameters.OutputAssembly;

        internal void AddSource(string source)
        {
            var path = Path.Combine(_directory.FullName, $"App_Web_test.{_sourceFiles.Count}.cs");
            File.WriteAllText(path, source);
            _sourceFiles.Add(path);
        }

        internal CompilerResults Compile() =>
            new RoslynCSharpCodeProvider().CompileAssemblyFromFile(Parameters, [.. _sourceFiles]);

        public void Dispose() => _directory.Delete(recursive: true);
    }
}
