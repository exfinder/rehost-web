using System.Globalization;
using System.Text;
using System.Web;
using System.Web.SessionState;

namespace Rehost.Web.ScenarioProbes;

public static class SessionReport
{
    public const string StartCountKey = "starts";

    public static void Write(HttpContext context, HttpSessionState? session)
    {
        var text = new StringBuilder();

        if (session == null)
        {
            text.Append("session=null\n");
        }
        else
        {
            text.Append("session=present\n");
            text.Append("id=").Append(session.SessionID).Append('\n');
            text.Append("isnew=").Append(session.IsNewSession).Append('\n');
            text.Append("count=").Append(
                session.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            text.Append("readonly=").Append(session.IsReadOnly).Append('\n');
            text.Append("cookieless=").Append(session.IsCookieless).Append('\n');
            text.Append("mode=").Append(session.Mode).Append('\n');
            text.Append("timeout=").Append(
                session.Timeout.ToString(CultureInfo.InvariantCulture)).Append('\n');
            text.Append("v=").Append(session["v"] ?? "null").Append('\n');
            text.Append("starts=").Append(session[StartCountKey] ?? "none").Append('\n');
        }

        context.Response.ContentType = "text/plain";
        context.Response.Write(text.ToString());
    }
}
