using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
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
            if (context.Request.PathBase.Value == "/owin-ws"
                || context.Request.Path.Value == "/owin-ws")
            {
                var accept = context.Get<Action<IDictionary<string, object>,
                    Func<IDictionary<string, object>, Task>>>("websocket.Accept");
                if (accept == null)
                {
                    context.Response.StatusCode = 422;
                    return Task.CompletedTask;
                }

                accept(null, async delegate(IDictionary<string, object> ws)
                {
                    var send = (Func<ArraySegment<byte>, int, bool, CancellationToken, Task>)
                        ws["websocket.SendAsync"];
                    var receive = (Func<ArraySegment<byte>, CancellationToken,
                        Task<Tuple<int, bool, int>>>)ws["websocket.ReceiveAsync"];
                    var close = (Func<int, string, CancellationToken, Task>)
                        ws["websocket.CloseAsync"];
                    var cancelled = (CancellationToken)ws["websocket.CallCancelled"];

                    var buffer = new ArraySegment<byte>(new byte[4096]);
                    var frame = await receive(buffer, cancelled);
                    var text = "owin-echo:" + frame.Item1 + ":"
                        + Encoding.UTF8.GetString(buffer.Array, 0, frame.Item3);
                    var payload = Encoding.UTF8.GetBytes(text);
                    await send(new ArraySegment<byte>(payload), 0x1, true, cancelled);
                    await close(1000, "owin-bye", cancelled);
                    await receive(buffer, cancelled);
                });
                return Task.CompletedTask;
            }

            if (context.Request.Query.Get("compression") == "off")
            {
                ((Action)context.Environment["systemweb.DisableResponseCompression"])();
            }

            if (context.Request.Query.Get("cancel") == "read")
            {
                var token = context.Request.CallCancelled;
                context.Response.Headers.Set(
                    "X-Call-Cancelled",
                    (token.CanBeCanceled ? "armed-" : "none-")
                        + (token.IsCancellationRequested ? "yes" : "no"));
            }

            return next();
        });
    }
}
