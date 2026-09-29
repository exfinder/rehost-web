using System.Collections.Immutable;

namespace Rehost.Web.Tests.Compatibility.Compilation;

public sealed class CodegenSubstrateFixture : IDisposable
{
    private readonly BatchApplication _application = BatchApplication.Create();

    public CodegenSubstrateFixture()
    {
        FirstTrace = _application.Run().ToImmutableArray();
        var segment = _application.Segment;
        AppCodeCount = Directory.GetFiles(segment, "App_Code.*.dll").Length;
        SubCodeCount = Directory.GetFiles(segment, "App_SubCode_Shared.*.dll").Length;
        GlobalResourcesCount = Directory.GetFiles(segment, "App_GlobalResources.*.dll").Length;
        SatelliteCount = Directory.GetFiles(Path.Combine(segment, "fr"), "*.resources.dll").Length;
    }

    internal BatchApplication Application => _application;

    internal ImmutableArray<string> FirstTrace { get; }
    internal int AppCodeCount { get; }
    internal int SubCodeCount { get; }
    internal int GlobalResourcesCount { get; }
    internal int SatelliteCount { get; }
    public void Dispose() => _application.Dispose();
}
