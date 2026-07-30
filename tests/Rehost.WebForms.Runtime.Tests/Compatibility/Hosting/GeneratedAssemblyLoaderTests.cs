using System.CodeDom.Compiler;
using System.Reflection;
using System.Runtime.Loader;
using System.Web.Compilation;
using System.Web.Util;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Hosting;

public sealed class GeneratedAssemblyLoaderTests
{
    [Fact]
    public void Loads_generated_output_into_the_default_context()
    {
        using var generated = GeneratedAssembly.Create("Loads_into_default");

        var assembly = GeneratedAssemblyLoader.Load(generated.Path);

        // Assembly.LoadFile, which CompilerResults.CompiledAssembly uses, would place this in a
        // context of its own, giving its types a second identity.
        AssemblyLoadContext.GetLoadContext(assembly).ShouldBe(AssemblyLoadContext.Default);
        assembly.GetType("Generated")!.GetProperty("Value")!
            .GetValue(Activator.CreateInstance(assembly.GetType("Generated")!))
            .ShouldBe(42);
    }

    [Fact]
    public void Reports_a_path_it_has_already_loaded()
    {
        using var generated = GeneratedAssembly.Create("Reports_loaded_path");
        using var other = GeneratedAssembly.Create("Reports_loaded_path_other");

        GeneratedAssemblyLoader.IsLoadedFrom(generated.Path).ShouldBeFalse();

        GeneratedAssemblyLoader.Load(generated.Path);

        GeneratedAssemblyLoader.IsLoadedFrom(generated.Path).ShouldBeTrue();
        GeneratedAssemblyLoader.IsLoadedFrom(other.Path).ShouldBeFalse();
        GeneratedAssemblyLoader.IsLoadedFrom(null).ShouldBeFalse();
    }

    [Fact]
    public void Probes_the_codegen_directory_before_the_application_bin_directory()
    {
        var candidates = GeneratedAssemblyLoader
            .CandidatePaths("/app/bin", "/codegen", new AssemblyName("App_Code.abc12345"))
            .ToArray();

        candidates.ShouldBe(
        [
            Path.Combine("/codegen", "App_Code.abc12345.dll"),
            Path.Combine("/app/bin", "App_Code.abc12345.dll"),
        ]);
    }

    [Fact]
    public void Probes_a_satellite_assembly_in_its_culture_subdirectory_only()
    {
        var name = new AssemblyName("App_GlobalResources.abc12345.resources")
        {
            CultureInfo = new System.Globalization.CultureInfo("fr"),
        };

        var candidates = GeneratedAssemblyLoader
            .CandidatePaths("/app/bin", "/codegen", name)
            .ToArray();

        // A satellite never lives beside the application's own binaries, so 'bin' is not probed.
        candidates.ShouldBe(
        [
            Path.Combine("/codegen", "fr", "App_GlobalResources.abc12345.resources.dll"),
        ]);
    }

    [Fact]
    public void Refuses_an_assembly_marked_for_deletion()
    {
        using var generated = GeneratedAssembly.Create("Refuses_marked");

        GeneratedAssemblyLoader.IsUsable(generated.Path).ShouldBeTrue();

        File.WriteAllText(generated.Path + ".delete", string.Empty);

        // The cache invalidated this build result and could not delete the file because it was
        // loaded; serving it again would resurrect it.
        GeneratedAssemblyLoader.IsUsable(generated.Path).ShouldBeFalse();
        GeneratedAssemblyLoader.IsUsable(Path.Combine(generated.Directory, "absent.dll"))
            .ShouldBeFalse();
    }

    private sealed class GeneratedAssembly : IDisposable
    {
        private readonly DirectoryInfo _directory;

        private GeneratedAssembly(DirectoryInfo directory, string path)
        {
            _directory = directory;
            Path = path;
        }

        internal string Path { get; }

        internal string Directory => _directory.FullName;

        internal static GeneratedAssembly Create(string name)
        {
            var directory = System.IO.Directory.CreateTempSubdirectory("rehost-loader-");
            var assemblyName = "App_Code." + name;
            var source = System.IO.Path.Combine(directory.FullName, assemblyName + ".0.cs");
            File.WriteAllText(source, "public class Generated { public int Value => 42; }");

            var parameters = new CompilerParameters
            {
                OutputAssembly = System.IO.Path.Combine(directory.FullName, assemblyName + ".dll"),
            };
            var results = new RoslynCSharpCodeProvider()
                .CompileAssemblyFromFile(parameters, [source]);
            results.Errors.HasErrors.ShouldBeFalse();

            return new GeneratedAssembly(directory, results.PathToAssembly);
        }

        // A loaded assembly cannot be unloaded, and on Windows its file stays locked, so the
        // directory is left behind rather than failing the test that produced it.
        public void Dispose()
        {
            try
            {
                _directory.Delete(recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
