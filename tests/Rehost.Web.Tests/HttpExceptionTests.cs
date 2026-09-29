using System.Web;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests;

public sealed class HttpExceptionTests
{
    [Fact]
    public void A_Null_Message_Reads_As_On_Framework()
    {
        HttpException[] exceptions =
        [
            new(null),
            new(null, 5),
            new(null, new InvalidOperationException()),
            new(404, null),
            new(404, null, new InvalidOperationException()),
            new(404, null, 5),
        ];

        exceptions.Select(e => e.Message).ShouldBe(
            Enumerable.Repeat("Exception of type 'System.Web.HttpException' was thrown.", exceptions.Length));
    }

    [Fact]
    public void A_Subclass_With_A_Null_Message_Names_Its_Own_Type() =>
        new HttpUnhandledException(null).Message
            .ShouldBe("Exception of type 'System.Web.HttpUnhandledException' was thrown.");

    [Fact]
    public void The_Parameterless_Constructor_Keeps_The_External_Component_Text() =>
        new HttpException().Message.ShouldBe("External component has thrown an exception.");

    [Fact]
    public void A_Given_Message_Is_Kept() =>
        new HttpException(404, "gone").Message.ShouldBe("gone");
}
