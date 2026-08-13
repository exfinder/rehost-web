<%@ Application Language="C#" %>
<%@ Import Namespace="Rehost.WebForms.ScenarioProbes" %>
<script runat="server">

    void Application_Start(object sender, EventArgs e)
    {
        PageProbe.Stages.Add("application-start");
    }

    void Application_BeginRequest(object sender, EventArgs e)
    {
        WitnessJournal.Stage(Request, "BeginRequest");
        if (Request.QueryString["module-end"] != null)
        {
            Response.Write("module|");
            Response.End();
        }
    }

    void Application_PreRequestHandlerExecute(object sender, EventArgs e)
    {
        WitnessJournal.Stage(Request, "PreRequestHandlerExecute");
    }

    void Application_PostRequestHandlerExecute(object sender, EventArgs e)
    {
        WitnessJournal.Stage(Request, "PostRequestHandlerExecute");
    }

    void Application_ReleaseRequestState(object sender, EventArgs e)
    {
        WitnessJournal.Stage(Request, "ReleaseRequestState");
    }

    void Application_UpdateRequestCache(object sender, EventArgs e)
    {
        WitnessJournal.Stage(Request, "UpdateRequestCache");
    }

    void Application_EndRequest(object sender, EventArgs e)
    {
        WitnessJournal.Stage(Request, "EndRequest");
        WitnessJournal.Stage(Request, Server.GetLastError() == null ? "LastError-null" : "LastError-set");
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
            WitnessJournal.Stage(Request, "stamp:" + note);
        }
        if (Request.QueryString["fae"] != null)
        {
            string note;
            try
            {
                Response.Flush();
                note = "flush-ok";
            }
            catch (Exception flushError)
            {
                note = "flush-threw:" + flushError.GetType().Name;
            }
            try
            {
                Response.AppendHeader("X-Late-2", "yes");
                note += "|late2-ok";
            }
            catch (Exception lateError)
            {
                note += "|late2-threw:" + lateError.GetType().Name;
            }
            WitnessJournal.Stage(Request, "fae:" + note);
        }
    }

    void Application_Error(object sender, EventArgs e)
    {
        var error = Server.GetLastError();
        WitnessJournal.Stage(Request, "ApplicationError:" + (error == null ? "null" : error.GetType().Name + ":" + error.Message));
        if (Request.QueryString["xferr"] != null)
        {
            Server.Transfer("~/xfer/ErrorPage.aspx");
        }
    }

</script>
