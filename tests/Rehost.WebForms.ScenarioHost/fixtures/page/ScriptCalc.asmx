<%@ WebService Language="C#" Class="ScriptCalcService" %>

using System.Web.Services;
using System.Web.Script.Services;

[WebService(Namespace = "http://rehost-webforms.test/scriptcalc")]
[ScriptService]
public class ScriptCalcService : WebService
{
    [WebMethod]
    public int JsonAdd(int a, int b)
    {
        return a + b;
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public string Greet(string name)
    {
        return "hello " + name;
    }
}
