using System.Web;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests;

// The classic step list is where the notification pair comes from (IV6), so a context with no
// executing step has no value to report and says which boundary it hit rather than inventing one.
public sealed class CurrentNotificationTests
{
    [Fact]
    public void A_Context_Outside_The_Pipeline_Refuses_Both_Reads()
    {
        var context = NewContext();

        Should.Throw<InvalidOperationException>(() => context.CurrentNotification)
            .Message.ShouldBe(
                "'HttpContext.CurrentNotification' is available only while a pipeline step is"
                + " executing; this request is outside the managed pipeline.");
        Should.Throw<InvalidOperationException>(() => context.IsPostNotification)
            .Message.ShouldBe(
                "'HttpContext.IsPostNotification' is available only while a pipeline step is"
                + " executing; this request is outside the managed pipeline.");
    }

    private static HttpContext NewContext() =>
        new(
            new HttpRequest("/probe", "http://localhost/probe", ""),
            new HttpResponse(TextWriter.Null));
}
