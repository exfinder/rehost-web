<%@ WebHandler Language="C#" Class="EchoHandler" %>

using System.Web;
using Microsoft.AspNet.FriendlyUrls;

public sealed class EchoHandler : IHttpHandler
{
    public bool IsReusable { get { return true; } }

    public void ProcessRequest(HttpContext context)
    {
        context.Response.ContentType = "text/plain";
        context.Response.Write(context.Request.GetFriendlyUrlFileVirtualPath());
        context.Response.Write("|");
        context.Response.Write(string.Join("|", context.Request.GetFriendlyUrlSegments()));
    }
}
