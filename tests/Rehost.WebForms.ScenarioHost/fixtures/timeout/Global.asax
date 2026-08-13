<%@ Application Language="C#" %>
<%@ Import Namespace="Rehost.WebForms.ScenarioProbes" %>
<script runat="server">

    void Application_PostRequestHandlerExecute(object sender, EventArgs e)
    {
        WitnessJournal.Stage(Request, "PostRequestHandlerExecute");
    }

    void Application_EndRequest(object sender, EventArgs e)
    {
        WitnessJournal.Stage(Request, "EndRequest");
        WitnessJournal.Stage(Request, Server.GetLastError() == null ? "LastError-null" : "LastError-set");
    }

    void Application_Error(object sender, EventArgs e)
    {
        var error = Server.GetLastError();
        WitnessJournal.Stage(Request, "ApplicationError:" + (error == null ? "null" : error.GetType().Name + ":" + error.Message));
    }

</script>
