using System.Web;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests;

// The server-variable fallback the port added reaches HttpRequest.FetchServerVariable, and two
// reachable shapes have no worker request behind it: the public HttpRequest constructor never
// takes one, and the collection drops its request at the end of a request. Both answered null
// before the fallback existed.
public sealed class HttpServerVarsCollectionTests
{
    [Fact]
    public void A_Request_Without_A_Worker_Request_Reads_Variables_As_Null()
    {
        var request = new HttpRequest("page.aspx", "http://localhost/page.aspx", "");

        var variables = request.ServerVariables;

        variables["WEBSOCKET_VERSION"].ShouldBeNull();
        variables["SERVER_NAME"].ShouldBeNull();
        variables[null].ShouldBeNull();
    }

    [Fact]
    public void A_Collection_Whose_Request_Is_Gone_Reads_Variables_As_Null()
    {
        var request = new HttpRequest("page.aspx", "http://localhost/page.aspx", "");
        var variables = (HttpServerVarsCollection)request.ServerVariables;

        variables.Dispose();

        variables["WEBSOCKET_VERSION"].ShouldBeNull();
        variables["SERVER_NAME"].ShouldBeNull();
    }
}
