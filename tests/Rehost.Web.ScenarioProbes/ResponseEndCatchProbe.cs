using System.Web;

namespace Rehost.Web.ScenarioProbes;

public sealed class ResponseEndCatchProbe : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        var response = context.Response;
        var shape = context.Request.QueryString["case"];
        if (shape == "error")
        {
            throw new InvalidOperationException("real");
        }

        response.Write("before|");
        try
        {
            response.End();
        }
        catch (Exception exception)
        {
            switch (shape)
            {
                case "wrap":
                    throw new InvalidOperationException("wrapped", exception);
                case "rethrow":
                    throw new Exception("failed");
            }

            response.Write($"in-catch:{exception.GetType().Name}|");
        }

        response.Write("after-try|");
    }
}
