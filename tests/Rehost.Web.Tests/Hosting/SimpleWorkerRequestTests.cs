using System.Web.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests.Hosting;

public sealed class SimpleWorkerRequestTests
{
    // Framework appended '\\' to the physical root and composed the translated path with it,
    // which off Windows is one bogus name. (MapPath is null by contract without runtime info.)
    [Fact]
    public void Translates_The_Page_Path_With_The_Platform_Separator()
    {
        var root = Directory.CreateTempSubdirectory("rehost-swr-");
        try
        {
            var request = new SimpleWorkerRequest(
                "/app", root.FullName, "page.aspx", null, TextWriter.Null);

            request.GetFilePathTranslated().ShouldBe(Path.Combine(root.FullName, "page.aspx"));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
