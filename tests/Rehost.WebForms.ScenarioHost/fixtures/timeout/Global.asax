<%@ Application Language="C#" %>
<%@ Import Namespace="Rehost.WebForms.ScenarioProbes" %>
<script runat="server">

    static void Stage(HttpRequest request, string stage)
    {
        var token = request.QueryString["wt"];
        if (token != null)
        {
            WitnessJournal.Record("stage:" + token + ":" + stage);
        }
    }

    void Application_PostRequestHandlerExecute(object sender, EventArgs e)
    {
        Stage(Request, "PostRequestHandlerExecute");
    }

    void Application_EndRequest(object sender, EventArgs e)
    {
        Stage(Request, "EndRequest");
        Stage(Request, Server.GetLastError() == null ? "LastError-null" : "LastError-set");
    }

    void Application_Error(object sender, EventArgs e)
    {
        var error = Server.GetLastError();
        Stage(Request, "ApplicationError:" + (error == null ? "null" : error.GetType().Name + ":" + error.Message));
    }

</script>
