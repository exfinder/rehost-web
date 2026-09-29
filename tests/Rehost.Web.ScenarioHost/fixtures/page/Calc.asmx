<%@ WebService Language="C#" Class="CalcService" %>

using System;
using System.Web.Services;
using System.Web.Services.Protocols;
using System.Xml.Serialization;

public class CalcToken : SoapHeader
{
    public string Value;
}

public class Pair
{
    public int First;
    public int Second;
}

[WebService(Namespace = "http://rehost.test/calc")]
[WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
public class CalcService : WebService
{
    public CalcToken Token;

    [WebMethod]
    public int Add(int a, int b)
    {
        return a + b;
    }

    [WebMethod]
    public int Sum(Pair pair)
    {
        return pair.First + pair.Second;
    }

    [WebMethod]
    [SoapHeader("Token")]
    public string EchoToken()
    {
        return Token == null ? "(none)" : Token.Value;
    }

    [WebMethod(EnableSession = true)]
    public int Bump()
    {
        object current = Session["calc-bump"];
        int next = current == null ? 1 : (int)current + 1;
        Session["calc-bump"] = next;
        return next;
    }

    [WebMethod]
    public string Fail()
    {
        throw new InvalidOperationException("calc-deliberate-failure");
    }

    [WebMethod]
    [SoapDocumentMethod(OneWay = true)]
    public void Notify()
    {
        Application["calc-notified"] = "yes";
    }

    [WebMethod]
    public string Notified()
    {
        object seen = Application["calc-notified"];
        return seen == null ? "no" : (string)seen;
    }
}
