using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using Microsoft.CSharp;
using Shouldly;
using System.Resources.Tools;
using Xunit;

namespace Rehost.Web.Tests;

public sealed class StronglyTypedResourceBuilderTests
{
    [Fact]
    public void VerifyResourceName_Matches_Framework_Identifier_Fixup()
    {
        using var provider = new CSharpCodeProvider();

        StronglyTypedResourceBuilder.VerifyResourceName("Order total!", provider).ShouldBe("Order_total_");
        StronglyTypedResourceBuilder.VerifyResourceName("class", provider).ShouldBe("_class");
        StronglyTypedResourceBuilder.VerifyResourceName("4", provider).ShouldBe("_4");
    }

    [Fact]
    public void Create_Preserves_Framework_Resource_Contract()
    {
        using var provider = new CSharpCodeProvider();
        var resources = new Hashtable
        {
            ["Greeting"] = "Hello",
            ["Order total"] = "42",
            ["Culture"] = "reserved",
            ["A B"] = "first",
            ["A-B"] = "collision",
            ["$this.Text"] = "design metadata"
        };

        var unit = StronglyTypedResourceBuilder.Create(
            resources,
            "SiteResources",
            "App_GlobalResources",
            provider,
            false,
            out var unmatchable);

        unmatchable.ShouldBe(new[] { "A B", "A-B", "Culture" }, ignoreOrder: true);

        var generatedType = unit.Namespaces.Cast<CodeNamespace>().Single().Types.Cast<CodeTypeDeclaration>().Single();
        generatedType.Name.ShouldBe("SiteResources");
        generatedType.TypeAttributes.ShouldBe(System.Reflection.TypeAttributes.Public);

        var memberNames = generatedType.Members.Cast<CodeTypeMember>().Select(member => member.Name).ToArray();
        memberNames.ShouldContain("Greeting");
        memberNames.ShouldContain("Order_total");
        memberNames.Count(name => name == "Culture").ShouldBe(1);
        memberNames.ShouldNotContain("A_B");

        var generatedCode = generatedType.CustomAttributes
            .Cast<CodeAttributeDeclaration>()
            .Single(attribute => attribute.Name.Contains(nameof(GeneratedCodeAttribute), StringComparison.Ordinal));
        generatedCode.Arguments[0].Value.ShouldBeOfType<CodePrimitiveExpression>().Value.ShouldBe("System.Resources.Tools.StronglyTypedResourceBuilder");
        generatedCode.Arguments[1].Value.ShouldBeOfType<CodePrimitiveExpression>().Value.ShouldBe("4.0.0.0");

        var resourceManager = generatedType.Members.Cast<CodeTypeMember>().OfType<CodeMemberProperty>()
            .Single(member => member.Name == "ResourceManager");
        var condition = resourceManager.GetStatements.OfType<CodeConditionStatement>().Single();
        var declaration = condition.TrueStatements.OfType<CodeVariableDeclarationStatement>().Single();
        var creation = declaration.InitExpression.ShouldBeOfType<CodeObjectCreateExpression>();
        creation.Parameters[0].ShouldBeOfType<CodePrimitiveExpression>().Value.ShouldBe("App_GlobalResources.SiteResources");
    }
}
