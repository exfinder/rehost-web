using System.Text;

using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// First reach of the ASMX pipeline over the port: the .asmx build provider through the
// port-owned compilation substrate, ScriptHandlerFactory's hand-off to
// WebServiceHandlerFactory, the SOAP 1.1/1.2 server protocols over XmlSerializer, and the
// documentation protocols (?wsdl, ?disco, help page) that Framework served from IIS.
public sealed class AsmxOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    private const string Ns = "http://rehost-webforms.test/calc";

    private static byte[] Soap11(string body) => Encoding.UTF8.GetBytes($"""
        <?xml version="1.0" encoding="utf-8"?>
        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
          <soap:Body>{body}</soap:Body>
        </soap:Envelope>
        """);

    private static byte[] Soap12(string body) => Encoding.UTF8.GetBytes($"""
        <?xml version="1.0" encoding="utf-8"?>
        <soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope">
          <soap:Body>{body}</soap:Body>
        </soap:Envelope>
        """);

    private Task<ScenarioResponse> InvokeSoap11Async(string methodBody, string action) =>
        scenario.Client.PostWithHeadersAsync(
            "/Calc.asmx", Soap11(methodBody), "text/xml; charset=utf-8",
            ("SOAPAction", $"\"{Ns}/{action}\""));

    [Fact]
    public async Task Soap11_Invoke_Returns_The_Wrapped_Result()
    {
        var response = await InvokeSoap11Async(
            $"<Add xmlns=\"{Ns}\"><a>3</a><b>4</b></Add>", "Add");

        response.StatusCode.ShouldBe(200);
        response.Headers["Content-Type"].ShouldBe("text/xml; charset=utf-8");
        response.Text.ShouldContain($"<AddResponse xmlns=\"{Ns}\">");
        response.Text.ShouldContain("<AddResult>7</AddResult>");
    }

    [Fact]
    public async Task Soap12_Invoke_Answers_With_The_Soap12_Envelope()
    {
        var response = await scenario.Client.PostWithHeadersAsync(
            "/Calc.asmx",
            Soap12($"<Add xmlns=\"{Ns}\"><a>20</a><b>22</b></Add>"),
            $"application/soap+xml; charset=utf-8; action=\"{Ns}/Add\"");

        response.StatusCode.ShouldBe(200);
        response.Headers["Content-Type"].ShouldBe("application/soap+xml; charset=utf-8");
        response.Text.ShouldContain("http://www.w3.org/2003/05/soap-envelope");
        response.Text.ShouldContain("<AddResult>42</AddResult>");
    }

    [Fact]
    public async Task Complex_Parameter_Deserializes_Through_XmlSerializer()
    {
        var response = await InvokeSoap11Async(
            $"<Sum xmlns=\"{Ns}\"><pair><First>19</First><Second>23</Second></pair></Sum>",
            "Sum");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("<SumResult>42</SumResult>");
    }

    [Fact]
    public async Task Soap_Header_Reaches_The_Method()
    {
        var body = $"""
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Header><CalcToken xmlns="{Ns}"><Value>tok-421</Value></CalcToken></soap:Header>
              <soap:Body><EchoToken xmlns="{Ns}" /></soap:Body>
            </soap:Envelope>
            """;
        var response = await scenario.Client.PostWithHeadersAsync(
            "/Calc.asmx", Encoding.UTF8.GetBytes(body), "text/xml; charset=utf-8",
            ("SOAPAction", $"\"{Ns}/EchoToken\""));

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("<EchoTokenResult>tok-421</EchoTokenResult>");
    }

    [Fact]
    public async Task Unhandled_Exception_Becomes_A_Soap_Fault()
    {
        var response = await InvokeSoap11Async($"<Fail xmlns=\"{Ns}\" />", "Fail");

        response.StatusCode.ShouldBe(500);
        response.Text.ShouldContain("<soap:Fault>");
        response.Text.ShouldContain("soap:Server");
        response.Text.ShouldContain("calc-deliberate-failure");
    }

    [Fact]
    public async Task Session_Enabled_Method_Keeps_State_Across_The_Cookie()
    {
        var bump = $"<Bump xmlns=\"{Ns}\" />";
        var first = await InvokeSoap11Async(bump, "Bump");

        first.StatusCode.ShouldBe(200);
        first.Text.ShouldContain("<BumpResult>1</BumpResult>");
        var sessionCookie = first.SetCookies
            .Single(cookie => cookie.StartsWith("ASP.NET_SessionId=", StringComparison.Ordinal));

        var second = await scenario.Client.PostWithHeadersAsync(
            "/Calc.asmx", Soap11(bump), "text/xml; charset=utf-8",
            ("SOAPAction", $"\"{Ns}/Bump\""),
            ("Cookie", sessionCookie.Split(';')[0]));

        second.Text.ShouldContain("<BumpResult>2</BumpResult>");
    }

    [Fact]
    public async Task OneWay_Method_Answers_202_And_Runs_Detached()
    {
        var response = await InvokeSoap11Async($"<Notify xmlns=\"{Ns}\" />", "Notify");

        response.StatusCode.ShouldBe(202);
        response.Bytes.ShouldBeEmpty();

        // The method body runs on a thread-pool post after the 202; poll the observing
        // method until the application-state write lands.
        var seen = "no";
        for (var attempt = 0; attempt < 50 && seen == "no"; attempt++)
        {
            var check = await InvokeSoap11Async($"<Notified xmlns=\"{Ns}\" />", "Notified");
            if (check.Text.Contains("<NotifiedResult>yes</NotifiedResult>"))
            {
                seen = "yes";
                break;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        seen.ShouldBe("yes");
    }

    [Fact]
    public async Task Localhost_Form_Post_Is_Enabled_By_Default()
    {
        var response = await scenario.Client.PostFormAsync("/Calc.asmx/Add", "a=40&b=2");

        response.StatusCode.ShouldBe(200);
        response.Headers["Content-Type"].ShouldBe("text/xml; charset=utf-8");
        response.Text.ShouldContain($"<int xmlns=\"{Ns}\">42</int>");
    }

    [Fact]
    public async Task Wsdl_Query_Serves_The_Generated_Contract()
    {
        var response = await scenario.Client.GetAsync("/Calc.asmx?wsdl");

        response.StatusCode.ShouldBe(200);
        response.Headers["Content-Type"].ShouldBe("text/xml; charset=utf-8");
        response.Text.ShouldContain("<wsdl:definitions");
        response.Text.ShouldContain($"targetNamespace=\"{Ns}\"");
        response.Text.ShouldContain("<wsdl:operation name=\"Add\">");
        response.Text.ShouldContain("<soap:address location=");
        response.Text.ShouldContain("<soap12:address location=");
    }

    [Fact]
    public async Task Disco_Query_Points_At_The_Contract()
    {
        var response = await scenario.Client.GetAsync("/Calc.asmx?disco");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("<discovery");
        response.Text.ShouldContain("contractRef");
        response.Text.ShouldContain("Calc.asmx?wsdl");
    }

    [Fact]
    public async Task Help_Page_Lists_The_Operations()
    {
        var response = await scenario.Client.GetAsync("/Calc.asmx");

        response.StatusCode.ShouldBe(200);
        response.Headers["Content-Type"].ShouldStartWith("text/html");
        response.Text.ShouldContain("The following operations are supported");
        response.Text.ShouldContain("Add");
        response.Text.ShouldContain("EchoToken");
    }

    [Fact]
    public async Task Json_Script_Service_Answers_In_The_D_Wrapper()
    {
        var response = await scenario.Client.PostWithHeadersAsync(
            "/ScriptCalc.asmx/JsonAdd",
            Encoding.UTF8.GetBytes("{\"a\":3,\"b\":4}"),
            "application/json; charset=utf-8");

        response.StatusCode.ShouldBe(200);
        response.Headers["Content-Type"].ShouldBe("application/json; charset=utf-8");
        response.Text.ShouldBe("{\"d\":7}");
    }

    [Fact]
    public async Task Json_Proxy_Script_Renders_For_The_Script_Service()
    {
        var response = await scenario.Client.GetAsync("/ScriptCalc.asmx/js");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("ScriptCalcService");
        response.Text.ShouldContain("JsonAdd");
    }
}
