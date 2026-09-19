using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace SplitSample.Verify;

internal static class SampleVerification
{
    private static readonly string[] AppFiles =
    [
        "App.dll", "Proj1.dll", "Proj2.dll", "Proj3.dll", "Newtonsoft.Json.dll", "System.Text.Json.dll",
        "NuGet.Versioning.dll", "Microsoft.Extensions.DependencyModel.dll", "fr/Proj3.resources.dll"
    ];

    public static async Task Run(string source)
    {
        source = Path.GetFullPath(source);
        Require(File.Exists(Path.Combine(source, "Host", "Host.csproj")), $"Sample source not found: {source}");
        var temporary = Path.Combine(Path.GetTempPath(), $"split graph {Guid.NewGuid():N}");
        var sample = Path.Combine(temporary, "source");
        try
        {
            CopyTree(source, sample, skipBuildDirectories: true);
            var output = Path.Combine(sample, "Host", "bin", "Debug", "net10.0");

            await Build(sample, "-p:SplitOutputEnabled=false");
            var baseline = await Check(output, temporary);
            var flatManifest = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(output, "Host.deps.json")))!;
            Console.WriteLine("PASS ordinary flat build: resolved versions and behavior");

            await Build(sample);
            AssertLayout(output);
            var split = await Check(output, temporary);
            AssertSameBehavior(baseline, split);
            AssertLocations(split);
            AssertManifest(output, flatManifest);
            Console.WriteLine("PASS split build: unified versions, shared types, nested dependencies, resources");

            await Build(sample, "--no-restore");
            AssertLayout(output);
            AssertSameBehavior(baseline, await Check(output, temporary));
            File.Delete(Path.Combine(output, "rehost_root", "bin", "Proj2.dll"));
            await Build(sample, "--no-restore");
            AssertLayout(output);
            Console.WriteLine("PASS incremental build and repair of deleted output");

            var deployment = Path.Combine(temporary, "deployment");
            CopyTree(output, deployment, skipBuildDirectories: false);
            var deployed = await Check(deployment, temporary);
            AssertSameBehavior(baseline, deployed);
            AssertLocations(deployed);
            await CheckHttp(deployment, temporary);
            Console.WriteLine("PASS copied output: executable startup and Kestrel HTTP, unrelated working directory");

            var depsPath = Path.Combine(deployment, "Host.deps.json");
            var broken = JsonNode.Parse(await File.ReadAllTextAsync(depsPath))!;
            RemoveLocalPaths(broken);
            await File.WriteAllTextAsync(depsPath, broken.ToJsonString());
            var failure = await Execute(Executable(deployment), ["--check"], temporary, expectSuccess: false);
            Require(failure.ExitCode != 0, "Removing localPath must break split-output startup.");
            Console.WriteLine("PASS mutation: removing localPath fails startup");

            await Build(sample, "-c", "Release");
            var release = Path.Combine(sample, "Host", "bin", "Release", "net10.0");
            AssertLayout(release);
            AssertSameBehavior(baseline, await Check(release, temporary));
            await Execute("dotnet", ["clean", "Host/Host.csproj", "-c", "Release", "--nologo", "-v:q"], sample);
            Require(!Directory.EnumerateFiles(release, "*.dll", SearchOption.AllDirectories).Any(),
                "dotnet clean left relocated assemblies behind.");
            Console.WriteLine("PASS Release build and SDK clean");

            var published = Path.Combine(temporary, "published");
            await Publish(sample, published);
            AssertLayout(published);
            var publishedProbe = await Check(published, temporary);
            AssertSameBehavior(baseline, publishedProbe);
            AssertLocations(publishedProbe);
            await Publish(sample, published);
            AssertLayout(published);
            Console.WriteLine("PASS publish: layout, startup from an unrelated working directory, repeat publish");

            var selfContained = Path.Combine(temporary, "self-contained");
            await Publish(sample, selfContained, "-r", RuntimeInformation.RuntimeIdentifier, "--self-contained", "true");
            AssertLayout(selfContained);
            AssertLocations(await Check(selfContained, temporary));
            Console.WriteLine($"PASS self-contained publish for {RuntimeInformation.RuntimeIdentifier}");

            var projectPath = Path.Combine(sample, "Host", "Host.csproj");
            var project = XDocument.Load(projectPath);
            project.Root!.Element("ItemGroup")!.Add(new XElement("PackageReference",
                new XAttribute("Include", "Newtonsoft.Json"), new XAttribute("Version", "13.0.3")));
            project.Save(projectPath);
            var downgrade = await Execute("dotnet", ["restore", "Host/Host.csproj", "--disable-parallel", "-v:q"],
                sample, expectSuccess: false);
            Require(downgrade.ExitCode != 0 && $"{downgrade.Output}{downgrade.Errors}".Contains("NU1605", StringComparison.Ordinal),
                "A direct lower Newtonsoft.Json reference must fail with NU1605.");
            Console.WriteLine("PASS mutation: direct lower Newtonsoft.Json reference fails with NU1605");
        }
        finally
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, recursive: true);
            }
        }
    }

    private static Task<CommandResult> Build(string sample, params string[] arguments) => Execute("dotnet",
        ["build", "Host/Host.csproj", "--nologo", "--maxcpucount:1", "--disable-build-servers",
            "-p:UseSharedCompilation=false", "-v:q", .. arguments], sample);

    private static Task<CommandResult> Publish(string sample, string destination, params string[] arguments) => Execute("dotnet",
        ["publish", "Host/Host.csproj", "-o", destination, "--nologo", "--maxcpucount:1", "--disable-build-servers",
            "-p:UseSharedCompilation=false", "-v:q", .. arguments], sample);

    private static async Task<JsonNode> Check(string output, string workingDirectory)
    {
        var result = await Execute(Executable(output), ["--check"], workingDirectory);
        var probe = JsonNode.Parse(result.Output)!;
        Require(probe["HostNewtonsoft"]!.GetValue<int>() == 17, "Host Newtonsoft call failed.");
        Require(probe["HostTextJson"]!.GetValue<int>() == 31, "Host System.Text.Json call failed.");
        Require(probe["HostPackages"]!.GetValue<string>() == "forty-two", "Host-only package call failed.");
        Require(probe["SharedResult"]!.GetValue<int>() == 46 && probe["SharedTypeIdentity"]!.GetValue<bool>(),
            "Host and App do not share Proj1's type identity.");
        Require(probe["App"]!["Newtonsoft"]!.GetValue<int>() == 19, "App Newtonsoft call failed.");
        Require(probe["App"]!["TextJson"]!.GetValue<int>() == 37, "App System.Text.Json call failed.");
        Require(probe["App"]!["NestedProject"]!.GetValue<string>() == "2.3.0", "Nested project/package call failed.");
        Require(probe["App"]!["DependencyModel"]!.GetValue<string>() == "nested-package/1.2.3", "DependencyModel call failed.");
        Require(probe["App"]!["FrenchResource"]!.GetValue<string>() == "Bonjour", "French satellite did not load.");
        return probe;
    }

    private static void AssertSameBehavior(JsonNode baseline, JsonNode actual)
    {
        var expected = baseline.DeepClone();
        var observed = actual.DeepClone();
        expected.AsObject().Remove("Assemblies");
        observed.AsObject().Remove("Assemblies");
        Require(JsonNode.DeepEquals(expected, observed), "Split behavior differs from the flat build.");
        var expectedAssemblies = baseline["Assemblies"]!.AsArray().ToDictionary(item => item!["Name"]!.GetValue<string>());
        foreach (var assembly in actual["Assemblies"]!.AsArray())
        {
            var name = assembly!["Name"]!.GetValue<string>();
            Require(expectedAssemblies.TryGetValue(name, out var original), $"Unexpected assembly: {name}");
            Require(JsonNode.DeepEquals(original!["InformationalVersion"], assembly["InformationalVersion"]),
                $"Different resolved version for {name}.");
            Require(assembly["Context"]!.GetValue<string>() == "Default", $"Separate load context for {name}.");
        }
    }

    private static void AssertLayout(string output)
    {
        foreach (var relative in AppFiles)
        {
            Require(File.Exists(Path.Combine(output, "rehost_root", "bin", relative)), $"Missing App asset: {relative}");
            Require(!File.Exists(Path.Combine(output, relative)), $"Duplicate App asset beside Host: {relative}");
        }

        foreach (var relative in new[] { "Host.dll", "HostPackages.dll", "Humanizer.dll" })
        {
            Require(File.Exists(Path.Combine(output, relative)), $"Missing Host asset: {relative}");
            Require(!File.Exists(Path.Combine(output, "rehost_root", "bin", relative)), $"Host asset moved: {relative}");
        }
    }

    private static void AssertLocations(JsonNode probe)
    {
        var assemblies = probe["Assemblies"]!.AsArray().ToDictionary(item => item!["Name"]!.GetValue<string>());
        var output = Path.GetDirectoryName(assemblies["Host"]!["Path"]!.GetValue<string>())!;
        foreach (var relative in AppFiles)
        {
            var name = Path.GetFileNameWithoutExtension(relative);
            Require(assemblies.TryGetValue(name, out var assembly), $"Assembly was not exercised: {name}");
            var actual = assembly!["Path"]!.GetValue<string>();
            if (name == "System.Text.Json" && !actual.StartsWith(output, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Require(Path.GetFullPath(actual) == Path.GetFullPath(Path.Combine(output, "rehost_root", "bin", relative)),
                $"{name} loaded from {actual} instead of the relocated output.");
        }
    }

    private static void AssertManifest(string output, JsonNode baseline)
    {
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(output, "Host.deps.json")))!;
        var libraries = manifest["libraries"]!.AsObject();
        Require(libraries.ContainsKey("Newtonsoft.Json/13.0.4") && libraries.ContainsKey("System.Text.Json/10.0.12"),
            "Host did not unify the intended package versions.");
        Require(!libraries.ContainsKey("Newtonsoft.Json/13.0.3") && !libraries.ContainsKey("System.Text.Json/9.0.20"),
            "App or helper project's independently resolved versions leaked into Host.");
        RemoveLocalPaths(manifest);
        Require(JsonNode.DeepEquals(baseline, manifest), "Relocation changed Host's dependency graph.");
    }

    private static void RemoveLocalPaths(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            obj.Remove("localPath");
            foreach (var property in obj)
            {
                if (property.Value is not null)
                {
                    RemoveLocalPaths(property.Value);
                }
            }
        }
    }

    private static async Task CheckHttp(string output, string workingDirectory)
    {
        using var host = Start(Executable(output), ["--urls", "http://127.0.0.1:0"], workingDirectory);
        var errors = host.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            while (await host.StandardOutput.ReadLineAsync(timeout.Token) is { } line)
            {
                var addressStart = line.IndexOf("http://127.0.0.1:", StringComparison.Ordinal);
                if (addressStart < 0)
                {
                    continue;
                }

                using var client = new HttpClient();
                var json = JsonNode.Parse(await client.GetStringAsync(line[addressStart..].Trim(), timeout.Token))!;
                Require(json["sharedTypeIdentity"]!.GetValue<bool>() &&
                    json["app"]!["frenchResource"]!.GetValue<string>() == "Bonjour", "Kestrel returned incorrect probe data.");
                return;
            }

            throw new InvalidOperationException($"Host exited before listening: {await errors}");
        }
        finally
        {
            if (!host.HasExited)
            {
                host.Kill(entireProcessTree: true);
            }
            await host.WaitForExitAsync();
        }
    }

    private static async Task<CommandResult> Execute(string executable, string[] arguments, string directory, bool expectSuccess = true)
    {
        using var process = Start(executable, arguments, directory);
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw;
        }

        var result = new CommandResult(process.ExitCode, await output, await errors);
        Require(!expectSuccess || result.ExitCode == 0,
            $"{executable} {string.Join(' ', arguments)} failed ({result.ExitCode}):{Environment.NewLine}{result.Output}{result.Errors}");
        return result;
    }

    private static Process Start(string executable, string[] arguments, string directory)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        return Process.Start(start) ?? throw new InvalidOperationException($"Failed to start {executable}.");
    }

    private static string Executable(string output) => Path.Combine(output, OperatingSystem.IsWindows() ? "Host.exe" : "Host");

    private static void CopyTree(string source, string destination, bool skipBuildDirectories)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }
        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            var name = Path.GetFileName(directory);
            if (skipBuildDirectories && name is "bin" or "obj" or ".git")
            {
                continue;
            }
            CopyTree(directory, Path.Combine(destination, name), skipBuildDirectories);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record CommandResult(int ExitCode, string Output, string Errors);
}
