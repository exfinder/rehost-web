<%@ Application Language="C#" %>
<%@ Import Namespace="Rehost.WebForms.ScenarioProbes" %>
<script runat="server">

    void Application_Start(object sender, EventArgs e)
    {
        PageProbe.Stages.Add("application-start");
    }

    // The IV6 map, staged only under the flag so the order scenarios keep their own lists.
    private void Note(string name)
    {
        if (Request.QueryString["cn"] == null)
        {
            return;
        }

        Witness.Stage(Request, "cn|" + name + "=" + Context.CurrentNotification + "/" + Context.IsPostNotification);
    }

    void Application_BeginRequest(object sender, EventArgs e)
    {
        Witness.Stage(Request, "BeginRequest");
        Note("BeginRequest");
        if (Request.QueryString["cn"] != null)
        {
            Context.AddOnRequestCompleted(completed => Witness.Stage(completed.Request,
                "cn-completed=" + completed.CurrentNotification + "/" + completed.IsPostNotification));
        }
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
            try
            {
                ((HttpApplication)sender).EndRequest -= Application_EndRequest;
                Witness.Stage(Request, "unbind:ok");
            }
            catch (Exception unbindError)
            {
                Witness.Stage(Request, "unbind:" + unbindError.GetType().Name + ":" + unbindError.Message);
            }
            try
            {
                ((HttpApplication)sender).OnExecuteRequestStep((context, next) => next());
                Witness.Stage(Request, "wrap-late:ok");
            }
            catch (Exception wrapError)
            {
                Witness.Stage(Request, "wrap-late:" + wrapError.GetType().Name + ":" + wrapError.Message);
            }
        }
        if (Request.QueryString["ws-begin"] != null)
        {
            try { Response.AppendHeader("X-Ws-Begin", "ok:" + Context.IsWebSocketRequest); }
            catch (Exception ex) { Response.AppendHeader("X-Ws-Begin", ex.GetType().Name + ":" + ex.Message); }
        }
    }

    void Application_AuthenticateRequest(object sender, EventArgs e)
    {
        Note("AuthenticateRequest");
    }

    void Application_PostAuthenticateRequest(object sender, EventArgs e)
    {
        Note("PostAuthenticateRequest");
    }

    void Application_AuthorizeRequest(object sender, EventArgs e)
    {
        Note("AuthorizeRequest");
    }

    void Application_PostAuthorizeRequest(object sender, EventArgs e)
    {
        Note("PostAuthorizeRequest");
    }

    void Application_ResolveRequestCache(object sender, EventArgs e)
    {
        Note("ResolveRequestCache");
    }

    void Application_PostResolveRequestCache(object sender, EventArgs e)
    {
        Note("PostResolveRequestCache");
        if (Request.QueryString["remap"] == "early")
        {
            Remap();
        }
    }

    void Application_MapRequestHandler(object sender, EventArgs e)
    {
        Witness.Stage(Request, "MapRequestHandler");
        Note("MapRequestHandler");
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

    void Application_PostMapRequestHandler(object sender, EventArgs e)
    {
        Note("PostMapRequestHandler");
    }

    void Application_AcquireRequestState(object sender, EventArgs e)
    {
        Note("AcquireRequestState");
    }

    void Application_PostAcquireRequestState(object sender, EventArgs e)
    {
        Note("PostAcquireRequestState");
    }

    void Application_LogRequest(object sender, EventArgs e)
    {
        Witness.Stage(Request, "LogRequest:" + Response.StatusCode);
        Note("LogRequest");
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
        Note("PostLogRequest");
    }

    void Application_PreRequestHandlerExecute(object sender, EventArgs e)
    {
        Witness.Stage(Request, "PreRequestHandlerExecute");
        Note("PreRequestHandlerExecute");
    }

    void Application_PostRequestHandlerExecute(object sender, EventArgs e)
    {
        Witness.Stage(Request, "PostRequestHandlerExecute");
        Note("PostRequestHandlerExecute");
    }

    void Application_ReleaseRequestState(object sender, EventArgs e)
    {
        Witness.Stage(Request, "ReleaseRequestState");
        Note("ReleaseRequestState");
    }

    void Application_PostReleaseRequestState(object sender, EventArgs e)
    {
        Note("PostReleaseRequestState");
    }

    void Application_UpdateRequestCache(object sender, EventArgs e)
    {
        Witness.Stage(Request, "UpdateRequestCache");
        Note("UpdateRequestCache");
    }

    void Application_PostUpdateRequestCache(object sender, EventArgs e)
    {
        Note("PostUpdateRequestCache");
    }

    void Application_EndRequest(object sender, EventArgs e)
    {
        Witness.Stage(Request, "EndRequest");
        Note("EndRequest");
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

    void Application_PreSendRequestHeaders(object sender, EventArgs e)
    {
        NotePreSend(sender, "PreSendRequestHeaders");
    }

    void Application_PreSendRequestContent(object sender, EventArgs e)
    {
        NotePreSend(sender, "PreSendRequestContent");
    }

    private void NotePreSend(object sender, string name)
    {
        var live = ((HttpApplication)sender).Context;
        if (live.Request.QueryString["cn"] == null)
        {
            return;
        }

        Witness.Stage(live.Request, name + ":" + (HttpContext.Current == null ? "null" : "set"));
        Note(name);
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
