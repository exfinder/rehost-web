using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using Rehost.WebForms.TestSupport;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

public sealed class PageCompilationFixture : IDisposable
{
    private const string Request = PageRequests.Canonical;

    private readonly PageApplication _application = PageApplication.Create();

    public PageCompilationFixture()
    {
        FirstTrace = _application.Run(Request).ToImmutableArray();
        FirstResponse = _application.ReadResponse(0).ToImmutableArray();
        FirstResponseText = _application.ReadResponseText(0);
        ExpectedResponse = _application.ExpectedResponse.ToImmutableArray();
        AssemblyEvidence = ReadAssemblyEvidence(_application.PageAssemblyPath());
    }

    internal PageApplication Application => _application;

    internal ImmutableArray<string> FirstTrace { get; }
    internal ImmutableArray<byte> FirstResponse { get; }
    internal string FirstResponseText { get; }
    internal ImmutableArray<byte> ExpectedResponse { get; }
    internal PageAssemblyEvidence AssemblyEvidence { get; }

    public void Dispose() => _application.Dispose();

    private static PageAssemblyEvidence ReadAssemblyEvidence(string assembly)
    {
        using var stream = File.OpenRead(assembly);
        using var portableExecutable = new PEReader(stream);
        var metadata = portableExecutable.GetMetadataReader();
        var referenced = metadata.MemberReferences
            .Select(handle => metadata.GetString(metadata.GetMemberReference(handle).Name))
            .ToImmutableHashSet(StringComparer.Ordinal);
        // Without this the other evidence would also hold for a page carrying no long literal,
        // which would silently stop covering P39.
        var containsLongLiteral = File.ReadAllBytes(assembly).AsSpan()
            .IndexOf(Encoding.Unicode.GetBytes("moves markup into a Win32 string resource")) >= 0;

        return new PageAssemblyEvidence(
            referenced,
            portableExecutable.PEHeaders.PEHeader!.ResourceTableDirectory.Size,
            containsLongLiteral);
    }

    internal sealed record PageAssemblyEvidence(
        ImmutableHashSet<string> Referenced,
        int ResourceTableSize,
        bool ContainsLongLiteral);
}
