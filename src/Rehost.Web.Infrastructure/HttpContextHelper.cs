using System.Web;

namespace Microsoft.Web.Infrastructure;

public static class HttpContextHelper
{
    public static void ExecuteInNullContext(Action action)
    {
        var context = HttpContext.Current;
        HttpContext.Current = null;
        try
        {
            action();
        }
        finally
        {
            HttpContext.Current = context;
        }
    }
}
