using System.Web;
using System.Web.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests;

// WebSockets ride IIS's native module through IIS7WorkerRequest, which no port host is; the
// imported checks refuse every other worker request with Framework's own message rather than a
// silent false. Pinned so the refusal stays explicit until the WebSockets follow-up lands.
public sealed class HttpContextWebSocketsTests
{
    [Fact]
    public void Is_Web_Socket_Request_Refuses_Explicitly_Off_The_Integrated_Pipeline()
    {
        var root = Directory.CreateTempSubdirectory("rehost-ws-");
        try
        {
            var context = new HttpContext(
                new SimpleWorkerRequest("/", root.FullName, "page.aspx", null, TextWriter.Null));

            Should.Throw<PlatformNotSupportedException>(() => context.IsWebSocketRequest)
                .Message.ShouldBe("This operation requires IIS integrated pipeline mode.");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
