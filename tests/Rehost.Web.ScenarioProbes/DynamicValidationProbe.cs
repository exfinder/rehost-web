using System.Web;
using Microsoft.Web.Infrastructure.DynamicValidationHelper;

namespace Rehost.Web.ScenarioProbes;

public sealed class DynamicValidationProbe : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        var request = context.Request;
        var lines = new List<string>
        {
            $"enabled-before={ValidationUtility.IsValidationEnabled(context)}",
        };

        ValidationUtility.EnableDynamicValidation(context);
        lines.Add($"enabled-after={ValidationUtility.IsValidationEnabled(context)}");

        ValidationUtility.GetUnvalidatedCollections(
            context, out var formGetter, out var queryStringGetter);
        var form = formGetter();
        lines.Add($"form-getter={form["x"]}");
        lines.Add($"query-getter={queryStringGetter()["q"]}");
        lines.Add($"form-getter-same={ReferenceEquals(form, formGetter())}");
        lines.Add($"form-getter-unvalidated={ReferenceEquals(form, request.Unvalidated.Form)}");
        lines.Add($"form={Attempt(() => request.Form["x"])}");
        lines.Add($"query={Attempt(() => request.QueryString["q"])}");
        lines.Add($"safe={Attempt(() => request.Form["safe"])}");

        context.Response.ContentType = "text/plain";
        context.Response.Write(string.Join("\n", lines));
    }

    private static string Attempt(Func<string?> read)
    {
        try
        {
            return read() ?? "null";
        }
        catch (Exception refusal)
        {
            return refusal.GetType().Name;
        }
    }
}
