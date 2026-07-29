using System.CodeDom.Compiler;
using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

public sealed class RootConfigurationCompilerTests
{
    private static readonly XElement[] Compilers = LoadCompilers();

    [Fact]
    public void Declares_a_compiler_for_each_supported_language()
    {
        Compilers.Select(c => (string?)c.Attribute("extension")).ShouldBe([".cs", ".vb"]);
    }

    [Theory]
    [InlineData(".cs")]
    [InlineData(".vb")]
    public void Configured_provider_type_resolves_to_a_code_dom_provider(string extension)
    {
        var typeName = (string?)Compiler(extension).Attribute("type");

        var type = Type.GetType(typeName!, throwOnError: true)!;

        type.IsAssignableTo(typeof(CodeDomProvider)).ShouldBeTrue();
        type.GetConstructor(Type.EmptyTypes).ShouldNotBeNull();
        type.GetConstructor([typeof(IDictionary<string, string>)])
            .ShouldBeNull("an IDictionary constructor diverts creation to the built-in provider");
    }

    [Fact]
    public void Pins_the_language_version_for_csharp()
    {
        var compilerOptions = (string?)Compiler(".cs").Attribute("compilerOptions");

        compilerOptions.ShouldNotBeNull().ShouldContain("/langversion:7.3");
    }

    private static XElement Compiler(string extension) =>
        Compilers.Single(c => (string?)c.Attribute("extension") == extension);

    private static XElement[] LoadCompilers()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "configs", "rehost-webforms.web.config");

        return
        [
            .. XDocument.Load(path)
                .Root!
                .Elements("system.web")
                .Elements("compilation")
                .Elements("compilers")
                .Elements("compiler"),
        ];
    }
}
