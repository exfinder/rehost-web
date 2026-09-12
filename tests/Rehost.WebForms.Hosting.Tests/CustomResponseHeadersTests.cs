using System.Web.IisConfig;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// What the module did to a response block that already carried the application's own headers
// (CH5-CH7, CH15), over the dictionary Kestrel writes from.
public sealed class CustomResponseHeadersTests
{
    [Fact]
    public void A_Second_Value_Under_The_Same_Name_Is_A_Second_Entry()
    {
        var target = new HeaderDictionary { ["X-Custom"] = "app" };

        CustomResponseHeaders.Apply(target, Configured(("X-Custom", "one")));

        target["X-Custom"].ShouldBe(new[] { "app", "one" });
    }

    [Fact]
    public void Cache_Control_And_Content_Type_Join_The_Existing_Value_With_A_Comma()
    {
        var target = new HeaderDictionary
        {
            ["Cache-Control"] = "private",
            ["Content-Type"] = "text/html; charset=utf-8",
        };

        CustomResponseHeaders.Apply(
            target,
            Configured(("Cache-Control", "no-store"), ("Content-Type", "text/x-bogus")));

        target["Cache-Control"].ShouldBe(new[] { "private,no-store" });
        target["Content-Type"].ShouldBe(new[] { "text/html; charset=utf-8,text/x-bogus" });
    }

    [Fact]
    public void A_Coalescing_Name_The_Response_Does_Not_Carry_Is_Written_Alone()
    {
        var target = new HeaderDictionary();

        CustomResponseHeaders.Apply(target, Configured(("Cache-Control", "no-store")));

        target["Cache-Control"].ShouldBe(new[] { "no-store" });
    }

    [Fact]
    public void An_Empty_Value_Adds_Nothing()
    {
        var target = new HeaderDictionary();

        CustomResponseHeaders.Apply(target, Configured(("X-Empty", ""), ("X-Sent", "1")));

        target.ContainsKey("X-Empty").ShouldBeFalse();
        target["X-Sent"].ShouldBe(new[] { "1" });
    }

    private static CustomHeaders Configured(params (string Name, string Value)[] rows) =>
        new(rows.Select(row => new CustomHeader(row.Name, row.Value)).ToArray(), false);
}
