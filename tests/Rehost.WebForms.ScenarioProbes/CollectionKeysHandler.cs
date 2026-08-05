using System.Web;

namespace Rehost.WebForms.ScenarioProbes;

public sealed class CollectionKeysHandler : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        try
        {
            var count = context.Request.QueryString["collection"] switch
            {
                "form" => context.Request.Form.Count,
                "files" => context.Request.Files.Count,
                _ => context.Request.QueryString.Count,
            };

            RequestBodyHandler.WriteResult(context, "count:" + count);
        }
        catch (Exception exception)
        {
            // The urlencoded parser wraps whatever it caught in one "not valid" HttpException, so
            // the outer message alone would not say which failure this was.
            var inner = exception.InnerException == null
                ? ""
                : "|inner:" + exception.InnerException.GetType().FullName
                    + ":" + exception.InnerException.Message;

            RequestBodyHandler.WriteResult(
                context,
                "error:" + exception.GetType().FullName + ":" + exception.Message + inner);
        }
    }
}
