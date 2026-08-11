using System.Reflection;
using System.Web.UI;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.UI.WebControls;

public sealed class WebResourceTests
{
    private static readonly Assembly Runtime = typeof(Page).Assembly;

    private static HashSet<string> DeclaredNames() => Runtime
        .GetCustomAttributes<WebResourceAttribute>()
        .Select(attribute => attribute.WebResource)
        .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void EveryDeclaredWebResourceIsEmbedded()
    {
        var declared = DeclaredNames();

        // Imported attributes, so an empty set means they stopped compiling.
        declared.Count.ShouldBeGreaterThan(70);

        var unresolved = declared
            .Where(name => Runtime.GetManifestResourceInfo(name) is null)
            .Order(StringComparer.Ordinal)
            .ToArray();

        unresolved.ShouldBeEmpty();
    }

    [Fact]
    public void EveryDeclaredWebResourceCarriesContent()
    {
        var empty = DeclaredNames()
            .Where(name =>
            {
                using var resource = Runtime.GetManifestResourceStream(name);
                return resource is null || resource.Length == 0;
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

        empty.ShouldBeEmpty();
    }

    [Fact]
    public void EveryEmbeddedFileResourceIsDeclared()
    {
        var declared = DeclaredNames();

        var undeclared = Runtime
            .GetManifestResourceNames()
            .Where(name => !name.EndsWith(".resources", StringComparison.Ordinal))
            .Where(name => !declared.Contains(name))
            .Order(StringComparer.Ordinal)
            .ToArray();

        undeclared.ShouldBeEmpty();
    }
}
