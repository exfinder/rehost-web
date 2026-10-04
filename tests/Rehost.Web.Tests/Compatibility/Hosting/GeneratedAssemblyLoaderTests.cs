using System.CodeDom.Compiler;
using System.Reflection;
using System.Runtime.Loader;
using System.Web.Compilation;
using System.Web.Util;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests.Compatibility.Hosting;

public sealed class GeneratedAssemblyLoaderTests
{
    [Fact]
    public void Loads_Generated_Output_Into_The_Default_Context()
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
    public void Reports_A_Path_It_Has_Already_Loaded()
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
    public void Probes_The_Codegen_Directory_Before_The_Application_Bin_Directory()
    {
        var candidates = GeneratedAssemblyLoader
            .CandidatePaths("/app/bin", "/codegen", [], new AssemblyName("App_Code.abc12345"))
            .ToArray();

        candidates.ShouldBe(
        [
            Path.Combine("/codegen", "App_Code.abc12345.dll"),
            Path.Combine("/app/bin", "App_Code.abc12345.dll"),
        ]);
    }

    [Fact]
    public void Probes_A_Satellite_Assembly_In_Its_Culture_Subdirectory_Only()
    {
        var name = new AssemblyName("App_GlobalResources.abc12345.resources")
        {
            CultureInfo = new System.Globalization.CultureInfo("fr"),
        };

        var candidates = GeneratedAssemblyLoader
            .CandidatePaths("/app/bin", "/codegen", ["/app/bin/Providers"], name)
            .ToArray();

        // A satellite never lives beside the application's own binaries, so neither 'bin' nor a
        // probing folder is probed.
        candidates.ShouldBe(
        [
            Path.Combine("/codegen", "fr", "App_GlobalResources.abc12345.resources.dll"),
        ]);
    }

    [Fact]
    public void Probes_The_Probing_Folders_After_The_Application_Bin_Directory_In_Written_Order()
    {
        var candidates = GeneratedAssemblyLoader
            .CandidatePaths(
                "/app/bin",
                "/codegen",
                ["/app/bin/Providers", "/app/modules"],
                new AssemblyName("Contoso.Providers.Caching"))
            .ToArray();

        candidates.ShouldBe(
        [
            Path.Combine("/codegen", "Contoso.Providers.Caching.dll"),
            Path.Combine("/app/bin", "Contoso.Providers.Caching.dll"),
            Path.Combine("/app/bin/Providers", "Contoso.Providers.Caching.dll"),
            Path.Combine("/app/modules", "Contoso.Providers.Caching.dll"),
        ]);
    }

    [Fact]
    public void Refuses_An_Assembly_Marked_For_Deletion()
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
