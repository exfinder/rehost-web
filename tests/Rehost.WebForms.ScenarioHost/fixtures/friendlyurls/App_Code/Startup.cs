using System;
using System.Threading.Tasks;
using Microsoft.Owin;
using Microsoft.Owin.Security.Cookies;
using Owin;

[assembly: OwinStartup("FixtureStartup", typeof(FixtureOwinStartup))]

// Only the owin:AppStartup appSetting can reach this class: the friendly name rules out
// attribute auto-discovery and the type name rules out the Startup convention.
public class FixtureOwinStartup
{
    public void Configuration(IAppBuilder app)
    {
        app.UseCookieAuthentication(new CookieAuthenticationOptions
        {
            AuthenticationType = "ApplicationCookie",
            LoginPath = new PathString("/Login"),
        });

        app.Use(delegate(IOwinContext context, Func<Task> next)
        {
            if (context.Request.Query.Get("compression") == "off")
            {
                ((Action)context.Environment["systemweb.DisableResponseCompression"])();
            }

            if (context.Request.Query.Get("cancel") == "read")
            {
                context.Response.Headers.Set(
                    "X-Call-Cancelled",
                    context.Request.CallCancelled.IsCancellationRequested ? "yes" : "no");
            }

            return next();
        });
    }
}
