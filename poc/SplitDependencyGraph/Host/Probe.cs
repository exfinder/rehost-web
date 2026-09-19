using System.Reflection;
using System.Runtime.Loader;
using Newtonsoft.Json;
using SplitSample.App;
using SplitSample.HostPackages;
using SplitSample.Proj1;

namespace SplitSample.Host;

public static class Probe
{
    public static object Read()
    {
        var hostJson = JsonConvert.DeserializeObject<Dictionary<string, int>>("""{"host":17}""")!;
        var app = ApplicationProbe.Read();
        var shared = new SharedValue(23);
        var hostPackages = HostProbe.Read();
        var textJson = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>("""{"text":31}""")!;

        return new
        {
            HostNewtonsoft = hostJson["host"],
            HostTextJson = textJson["text"],
            HostPackages = hostPackages,
            SharedResult = ApplicationProbe.Accept(shared),
            SharedTypeIdentity = ReferenceEquals(typeof(SharedValue), ApplicationProbe.SharedType),
            App = app,
            Assemblies = AssemblyLoadContext.Default.Assemblies
                .Where(assembly => !assembly.IsDynamic &&
                    (assembly.Location.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase) ||
                     assembly.GetName().Name == "System.Text.Json"))
                .OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal)
                .Select(assembly => new
                {
                    Name = assembly.GetName().Name,
                    Version = assembly.GetName().Version?.ToString(),
                    InformationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                    Path = assembly.Location,
                    Context = AssemblyLoadContext.GetLoadContext(assembly)?.Name
                }).ToArray()
        };
    }
}
