<%@ Application Language="C#" %>
<%@ Import Namespace="Rehost.WebForms.ScenarioProbes" %>
<script runat="server">

    void Application_Start(object sender, EventArgs e)
    {
        PageProbe.Stages.Add("application-start");
    }

    void Application_BeginRequest(object sender, EventArgs e)
    {
        Witness.Stage(Request, "BeginRequest");
        if (Request.QueryString["module-end"] != null)
        {
            Response.Write("module|");
            Response.End();
        }
        if (Request.QueryString["bind"] == "1")
        {
            try
            {
                ((HttpApplication)sender).EndRequest += delegate { };
                Witness.Stage(Request, "bind:ok");
            }
            catch (Exception bindError)
            {
                Witness.Stage(Request, "bind:" + bindError.GetType().Name + ":" + bindError.Message);
            }
        }
        if (Request.QueryString["ws-begin"] != null)
        {
            try { Response.AppendHeader("X-Ws-Begin", "ok:" + Context.IsWebSocketRequest); }
            catch (Exception ex) { Response.AppendHeader("X-Ws-Begin", ex.GetType().Name + ":" + ex.Message); }
        }
    }

    void Application_PostResolveRequestCache(object sender, EventArgs e)
    {
        if (Request.QueryString["remap"] == "early")
        {
            Remap();
        }
    }

    void Application_MapRequestHandler(object sender, EventArgs e)
    {
        Witness.Stage(Request, "MapRequestHandler");
        if (Request.QueryString["remap"] == "late")
        {
            Remap();
        }
    }

    private void Remap()
    {
        try
        {
            Context.RemapHandler(new RemapTarget());
            Witness.Stage(Request, "remap:ok");
        }
        catch (Exception remapError)
        {
            Witness.Stage(Request, "remap:threw:" + remapError.GetType().Name);
        }
    }

    void Application_LogRequest(object sender, EventArgs e)
    {
        Witness.Stage(Request, "LogRequest:" + Response.StatusCode);
        if (Request.QueryString["lw"] != null)
        {
            try
            {
                Response.Write("|log");
                Response.AppendHeader("X-At-LogRequest", "1");
            }
            catch (Exception logError)
            {
                Witness.Stage(Request, "lw:threw:" + logError.GetType().Name);
            }
        }
    }

    void Application_PostLogRequest(object sender, EventArgs e)
    {
        Witness.Stage(Request, "PostLogRequest");
    }

    void Application_PreRequestHandlerExecute(object sender, EventArgs e)
    {
        Witness.Stage(Request, "PreRequestHandlerExecute");
    }

    void Application_PostRequestHandlerExecute(object sender, EventArgs e)
    {
        Witness.Stage(Request, "PostRequestHandlerExecute");
    }

    void Application_ReleaseRequestState(object sender, EventArgs e)
    {
        Witness.Stage(Request, "ReleaseRequestState");
    }

    void Application_UpdateRequestCache(object sender, EventArgs e)
    {
        Witness.Stage(Request, "UpdateRequestCache");
    }

    void Application_EndRequest(object sender, EventArgs e)
    {
        Witness.Stage(Request, "EndRequest");
        Witness.Stage(Request, Server.GetLastError() == null ? "LastError-null" : "LastError-set");
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
            Witness.Stage(Request, "stamp:" + note);
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
            Witness.Stage(Request, "fae:" + note);
        }
    }

    void Application_Error(object sender, EventArgs e)
    {
        var error = Server.GetLastError();
        Witness.Stage(Request, "ApplicationError:" + (error == null ? "null" : error.GetType().Name + ":" + error.Message));
        if (Request.QueryString["xferr"] != null)
        {
            Server.Transfer("~/xfer/ErrorPage.aspx");
        }
    }

</script>
