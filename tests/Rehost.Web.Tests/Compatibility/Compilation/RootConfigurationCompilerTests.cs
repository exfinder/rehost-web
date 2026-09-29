using System.CodeDom.Compiler;
using System.Xml.Linq;
using Shouldly;
using Rehost.Web.Hosting;
using Xunit;

namespace Rehost.Web.Tests.Compatibility.Compilation;

public sealed class RootConfigurationCompilerTests
{
    private static readonly XElement[] Compilers = LoadCompilers();

    [Fact]
    public void Declares_A_Compiler_For_Each_Supported_Language()
    {
        Compilers.Select(c => (string?)c.Attribute("extension")).ShouldBe([".cs", ".vb"]);
    }

    [Theory]
    [InlineData(".cs")]
    [InlineData(".vb")]
    public void Configured_Provider_Type_Resolves_To_A_Code_Dom_Provider(string extension)
    {
        var typeName = (string?)Compiler(extension).Attribute("type");

        var type = Type.GetType(typeName!, throwOnError: true)!;

        type.IsAssignableTo(typeof(CodeDomProvider)).ShouldBeTrue();
        type.GetConstructor(Type.EmptyTypes).ShouldNotBeNull();
        type.GetConstructor([typeof(IDictionary<string, string>)])
            .ShouldBeNull("an IDictionary constructor diverts creation to the built-in provider");
    }

    [Fact]
    public void Pins_The_Language_Version_For_Csharp()
    {
        var compilerOptions = (string?)Compiler(".cs").Attribute("compilerOptions");

        compilerOptions.ShouldNotBeNull().ShouldContain("/langversion:7.3");
    }

    private static XElement Compiler(string extension) =>
        Compilers.Single(c => (string?)c.Attribute("extension") == extension);

    private static XElement[] LoadCompilers()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "configs", RehostWebOptions.DefaultRootWebConfigurationFileName);

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
