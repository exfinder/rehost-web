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

    private static CustomHeaders Configured(params (string Name, string Value)[] rows) =>
        new(rows.Select(row => new CustomHeader(row.Name, row.Value)).ToArray(), false);
}
