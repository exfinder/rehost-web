using System.Globalization;
using System.Text;
using System.Web;

namespace Rehost.WebForms.ScenarioProbes;

// Members an integrated pool answered and a classic one refused: the WEBSOCKET_VERSION server
// variable (IV23), Response.SubStatusCode (IV26) and Request.InsertEntityBody (IV28).
public sealed class IntegratedMembersProbe : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        var response = context.Response;
        response.ContentType = "text/plain";

        switch (context.Request.QueryString["mode"])
        {
            case "vars":
                Variables(context, response);
                break;

            case "substatus":
                SubStatus(context, response);
                break;

            // Whatever the substatus is set to, this arm writes the same response, so two
            // requests to it can be compared byte for byte.
            case "substatus-wire":
                SetSubStatus(context, response);
                response.Write("substatus-body");
                break;

            case "ieb":
                InsertEntityBody(context, response);
                break;

            default:
                response.Write("unknown-mode");
                break;
        }
    }

    // The value first, then the enumerated shape: reading the name must not add it. The Count read
    // in the middle populates the collection, so the auth reads either side of it are the two
    // states an empty-valued variable is answered from.
    private static void Variables(HttpContext context, HttpResponse response)
    {
        var variables = context.Request.ServerVariables;
        var dump = new StringBuilder();

        Line(dump, "websocket-version", variables["WEBSOCKET_VERSION"]);
        Line(dump, "auth-type-before", variables["AUTH_TYPE"]);
        Line(dump, "count", variables.Count.ToString(CultureInfo.InvariantCulture));
        Line(dump, "in-allkeys", Array.IndexOf(variables.AllKeys, "WEBSOCKET_VERSION") >= 0
            ? "true"
            : "false");
        Line(dump, "unknown", variables["REHOST_NOT_A_VARIABLE"]);
        Line(dump, "auth-type-after", variables["AUTH_TYPE"]);
        Line(dump, "remote-user-after", variables["REMOTE_USER"]);
        Line(dump, "logon-user-after", variables["LOGON_USER"]);

        response.Write(dump.ToString());
    }

    private static void SubStatus(HttpContext context, HttpResponse response)
    {
        var dump = new StringBuilder();

        Line(dump, "default", response.SubStatusCode.ToString(CultureInfo.InvariantCulture));
        SetSubStatus(context, response);
        Line(dump, "after", response.SubStatusCode.ToString(CultureInfo.InvariantCulture));

        response.Write(dump.ToString());
    }

    private static void SetSubStatus(HttpContext context, HttpResponse response)
    {
        if (context.Request.QueryString["status"] == "404")
        {
            response.StatusCode = 404;
        }

        var requested = context.Request.QueryString["sub"];
        if (requested != null)
        {
            response.SubStatusCode = int.Parse(requested, CultureInfo.InvariantCulture);
        }
    }

    private static void InsertEntityBody(HttpContext context, HttpResponse response)
    {
        var request = context.Request;
        var dump = new StringBuilder();

        Line(dump, "form", request.Form["field"]);
        Line(dump, "no-args", Attempt(() => request.InsertEntityBody()));
        Line(dump, "three-args", Attempt(() => request.InsertEntityBody([1, 2, 3], 0, 3)));
        Line(dump, "null-buffer", Attempt(() => request.InsertEntityBody(null, 0, 0)));
        Line(dump, "negative-offset", Attempt(() => request.InsertEntityBody([1, 2, 3], -1, 1)));
        Line(dump, "bad-range", Attempt(() => request.InsertEntityBody([1, 2, 3], 1, 3)));
        Line(dump, "form-after", request.Form["field"]);

        response.Write(dump.ToString());
    }

    private static string Attempt(Action call)
    {
        try
        {
            call();
            return "ok";
        }
        catch (Exception refusal)
        {
            return refusal.GetType().Name;
        }
    }

    private static void Line(StringBuilder dump, string label, string? value) =>
        dump.Append(label).Append('=').Append(value ?? "null").Append('\n');
}
