using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Web.UI;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Extensions.Tests;

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

        declared.Count.ShouldBe(26);

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
    public void ReleaseAndDebugSelectOppositeBranches()
    {
        ReadResource("MicrosoftAjaxCore.js").ShouldContain("Sys.Debug.isDebug = false");
        ReadResource("MicrosoftAjaxCore.debug.js").ShouldContain("Sys.Debug.isDebug = true");
    }

    [Fact]
    public void ReleaseDropsDebugOnlyValidation()
    {
        // Type.js guards registerNamespace with #if DEBUG; release keeps only the assignment.
        ReadResource("MicrosoftAjaxCore.debug.js").ShouldContain("Sys.Res.notATypeName");
        ReadResource("MicrosoftAjaxCore.js").ShouldNotContain("Sys.Res.notATypeName");
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
    public void PerFileCopyrightHeadersAreExcluded()
    {
        var script = ReadResource("MicrosoftAjax.js");

        script.ShouldStartWith("//!");
        Regex.Matches(script, "copyright file=").Count.ShouldBe(0);
    }
}
