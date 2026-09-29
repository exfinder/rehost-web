using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Web.UI;
using Shouldly;
using Xunit;

namespace Rehost.Web.Extensions.Tests;

public sealed class AjaxScriptResourceTests
{
    private static readonly Assembly Extensions = typeof(ScriptManager).Assembly;

    private static HashSet<string> DeclaredNames() => Extensions
        .GetCustomAttributes<WebResourceAttribute>()
        .Select(attribute => attribute.WebResource)
        .ToHashSet(StringComparer.Ordinal);

    private static string ReadResource(string name)
    {
        using var stream = Extensions.GetManifestResourceStream(name);
        stream.ShouldNotBeNull(name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void EveryDeclaredScriptIsEmbedded()
    {
        var declared = DeclaredNames();

        declared.Count.ShouldBe(13);

        var unresolved = declared
            .Where(name => Extensions.GetManifestResourceInfo(name) is null)
            .Order(StringComparer.Ordinal)
            .ToArray();

        unresolved.ShouldBeEmpty();
    }

    [Fact]
    public void EveryEmbeddedFileResourceIsDeclared()
    {
        var declared = DeclaredNames();

        var undeclared = Extensions
            .GetManifestResourceNames()
            .Where(name => !name.EndsWith(".resources", StringComparison.Ordinal))
            .Where(name => !declared.Contains(name))
            .Order(StringComparer.Ordinal)
            .ToArray();

        undeclared.ShouldBeEmpty();
    }

    [Fact]
    public void NoBuildDirectiveSurvivesGeneration()
    {
        var directive = new Regex(@"^\s*(#(include|if|else|endif)\b|##\w+)", RegexOptions.Multiline);

        var leaking = DeclaredNames()
            .Where(name => directive.IsMatch(ReadResource(name)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        leaking.ShouldBeEmpty();
    }

    [Fact]
    public void DebugOnlyCodeIsExcluded()
    {
        var core = ReadResource("MicrosoftAjaxCore.js");

        core.ShouldContain("Sys.Debug.isDebug=false", Case.Sensitive);
        core.ShouldNotContain("Sys.Res.notATypeName");
    }

    [Fact]
    public void NoMinifierPlaceholderSurvivesGeneration()
    {
        // AjaxMin renders any node it synthesized without source text as the literal
        // "[generated code]", which is a syntax error in the shipped script.
        var corrupted = DeclaredNames()
            .Where(name => ReadResource(name).Contains("[generated code]"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        corrupted.ShouldBeEmpty();
    }

    [Fact]
    public void ReleaseBooleanParseMatchesFrameworksOwnEmission()
    {
        // The one site the IfElseReturnToReturnConditional kill bit exists for: a
        // function whose ##DEBUG-stripped body ends in a bare if-return. Framework's
        // own release build keeps the two ifs.
        var core = ReadResource("MicrosoftAjaxCore.js");

        core.ShouldContain(
            "Boolean.parse=function(b){var a=b.trim().toLowerCase();"
            + "if(a===\"false\")return false;if(a===\"true\")return true}");
    }

    [Fact]
    public void NoDebugScriptIsShipped()
    {
        // ScriptMode.Auto falls back to the release script when the debug one is
        // absent, which is the supported shape; a hollow debug script is not.
        DeclaredNames().ShouldNotContain(name => name.EndsWith(".debug.js", StringComparison.Ordinal));
    }

    [Fact]
    public void WebResourceUtilInitializesOverTheDeclaredNames()
    {
        // Its table is built with First() over the declared names, so no declarations
        // turns every script URL into a TypeInitializationException.
        var type = Extensions.GetType("System.Web.UI.WebResourceUtil", throwOnError: true)!;

        Should.NotThrow(() => RuntimeHelpers.RunClassConstructor(type.TypeHandle));
    }

    [Fact]
    public void EachScriptCarriesTheSingleBanner()
    {
        var script = ReadResource("MicrosoftAjax.js");

        script.ShouldStartWith("//----");
        script.ShouldContain("// MicrosoftAjax.js\r\n", Case.Sensitive);
        Regex.Matches(script, "Copyright").Count.ShouldBe(1);
    }
}
