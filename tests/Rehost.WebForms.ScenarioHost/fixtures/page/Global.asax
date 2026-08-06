<%@ Application Language="C#" %>
<%@ Import Namespace="Rehost.WebForms.ScenarioProbes" %>
<script runat="server">

    void Application_Start(object sender, EventArgs e)
    {
        PageProbe.Stages.Add("application-start");
    }

    // Stage names reach the witness only for requests carrying a wt token, so
    // untagged requests (and /witness itself) leave no trace.
    static void Stage(HttpRequest request, string stage)
    {
        var token = request.QueryString["wt"];
        if (token != null)
        {
            WitnessJournal.Record("stage:" + token + ":" + stage);
        }
    }

    void Application_BeginRequest(object sender, EventArgs e)
    {
        Stage(Request, "BeginRequest");
        if (Request.QueryString["module-end"] != null)
        {
            Response.Write("module|");
            Response.End();
        }
    }

    void Application_PreRequestHandlerExecute(object sender, EventArgs e)
    {
        Stage(Request, "PreRequestHandlerExecute");
    }

    void Application_PostRequestHandlerExecute(object sender, EventArgs e)
    {
        Stage(Request, "PostRequestHandlerExecute");
    }

    void Application_ReleaseRequestState(object sender, EventArgs e)
    {
        Stage(Request, "ReleaseRequestState");
    }

    void Application_UpdateRequestCache(object sender, EventArgs e)
    {
        Stage(Request, "UpdateRequestCache");
    }

    void Application_EndRequest(object sender, EventArgs e)
    {
        Stage(Request, "EndRequest");
        Stage(Request, Server.GetLastError() == null ? "LastError-null" : "LastError-set");
        if (Request.QueryString["stamp"] != null)
        {
            string note;
            try
            {
                Response.AppendHeader("X-After-End", "stamped");
                note = "append-ok";
            }
            catch (Exception headerError)
            {
                note = "append-threw:" + headerError.GetType().Name;
            }
            try
            {
                Response.Cookies.Add(new HttpCookie("late", "yes"));
                note += "|cookie-ok";
            }
            catch (Exception cookieError)
            {
                note += "|cookie-threw:" + cookieError.GetType().Name;
            }
            Stage(Request, "stamp:" + note);
        }
    }

    void Application_Error(object sender, EventArgs e)
    {
        var error = Server.GetLastError();
        Stage(Request, "ApplicationError:" + (error == null ? "null" : error.GetType().Name + ":" + error.Message));
    }

</script>
