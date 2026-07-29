using System.CodeDom.Compiler;
using System.Web.Compilation;
using Rehost.WebForms.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

public sealed class RoslynCSharpCompilerTests
{
    [Fact]
    public void Emits_an_assembly_to_the_requested_output_path()
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
    public void Maps_diagnostics_through_line_pragmas_to_the_originating_page()
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
    public void Reports_unresolved_types_with_the_compiler_error_number()
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
    public void Reports_warnings_without_failing_and_without_informational_noise()
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
    public void Configured_warning_level_does_not_make_warnings_fatal()
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
    public void Honours_warnaserror_from_compiler_options()
    {
        using var scope = new CompilationScope();
        scope.AddSource("public class Page { public void Render() { int unused = 1; } }");
        scope.Parameters.CompilerOptions = "/warnaserror+";

        var results = scope.Compile();

        results.Errors.HasErrors.ShouldBeTrue();
    }

    [Fact]
    public void Writes_compiler_formatted_output_lines()
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
    public void Honours_preprocessor_symbols_from_compiler_options()
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
    public void Honours_suppressed_warnings_from_compiler_options()
    {
        using var scope = new CompilationScope();
        scope.AddSource("public class Page { public void Render() { int unused = 1; } }");
        scope.Parameters.CompilerOptions = "/nowarn:219";

        scope.Compile().Errors.Count.ShouldBe(0);
    }

    [Fact]
    public void Honours_the_configured_language_version()
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
    public void Implicit_references_cover_the_framework_surface_a_page_uses()
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
    public void Honours_referenced_assemblies()
    {
        using var scope = new CompilationScope();
        scope.AddSource(
            """
            using Rehost.WebForms.Hosting;

            public class Page
            {
                public WebFormsApplicationOptions Options = new WebFormsApplicationOptions();
            }
            """);

        // Nothing outside the shared framework is referenced implicitly.
        scope.Compile().Errors.Cast<CompilerError>().ShouldContain(e => e.ErrorNumber == "CS0246");

        scope.Parameters.ReferencedAssemblies.Add(typeof(WebFormsApplicationOptions).Assembly.Location);

        scope.Compile().Errors.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void Reports_a_referenced_assembly_that_does_not_exist()
    {
        using var scope = new CompilationScope();
        scope.AddSource("public class Page { }");
        scope.Parameters.ReferencedAssemblies.Add(Path.Combine(scope.DirectoryPath, "Absent.dll"));

        var results = scope.Compile();

        results.Errors.HasErrors.ShouldBeTrue();
        results.Errors.Cast<CompilerError>().ShouldContain(e => e.ErrorNumber == "CS0006");
    }

    [Fact]
    public void Debug_information_emits_symbols_and_defines_the_debug_symbol()
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
    public void Visual_basic_compilation_is_explicitly_unsupported()
    {
        var provider = new UnsupportedVBCodeProvider();

        var exception = Should.Throw<PlatformNotSupportedException>(
            () => provider.CompileAssemblyFromFile(new CompilerParameters(), "Default.aspx.vb"));

        exception.Message.ShouldBe(UnsupportedVBCodeProvider.UnsupportedMessage);
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
