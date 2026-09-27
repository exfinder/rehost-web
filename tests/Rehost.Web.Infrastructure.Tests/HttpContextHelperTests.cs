using System.Web;
using Microsoft.Web.Infrastructure;
using Shouldly;
using Xunit;

namespace Rehost.Web.Infrastructure.Tests;

public sealed class HttpContextHelperTests
{
    [Fact]
    public void The_Action_Runs_Without_A_Context_And_The_Original_Returns() =>
        WithContext(original =>
        {
            var inside = original;

            HttpContextHelper.ExecuteInNullContext(() => inside = HttpContext.Current);

            inside.ShouldBeNull();
            HttpContext.Current.ShouldBeSameAs(original);
        });

    [Fact]
    public void A_Throwing_Action_Reaches_The_Caller_Unchanged_And_The_Context_Returns() =>
        WithContext(original =>
        {
            var thrown = new InvalidOperationException("from the action");

            Should.Throw<InvalidOperationException>(
                () => HttpContextHelper.ExecuteInNullContext(() => throw thrown))
                .ShouldBeSameAs(thrown);
            HttpContext.Current.ShouldBeSameAs(original);
        });

    [Fact]
    public void A_Nested_Call_Also_Runs_Without_A_Context() =>
        WithContext(original =>
        {
            var nested = original;
            var between = original;

            HttpContextHelper.ExecuteInNullContext(() =>
            {
                HttpContextHelper.ExecuteInNullContext(() => nested = HttpContext.Current);
                between = HttpContext.Current;
            });

            nested.ShouldBeNull();
            between.ShouldBeNull();
            HttpContext.Current.ShouldBeSameAs(original);
        });

    [Fact]
    public void A_Null_Action_Throws_Null_Reference_And_The_Context_Returns() =>
        WithContext(original =>
        {
            Should.Throw<NullReferenceException>(
                () => HttpContextHelper.ExecuteInNullContext(null!));
            HttpContext.Current.ShouldBeSameAs(original);
        });

    private static void WithContext(Action<HttpContext> test)
    {
        var context = new HttpContext(
            new HttpRequest("default.aspx", "http://localhost/default.aspx", ""),
            new HttpResponse(TextWriter.Null));
        HttpContext.Current = context;
        try
        {
            test(context);
        }
        finally
        {
            HttpContext.Current = null;
        }
    }
}
