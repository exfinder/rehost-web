<%@ Application Language="C#" %>
<%@ Import Namespace="Rehost.Web.ScenarioProbes" %>
<script runat="server">

    void Application_PostRequestHandlerExecute(object sender, EventArgs e)
    {
        Witness.Stage(Request, "PostRequestHandlerExecute");
    }

    void Application_EndRequest(object sender, EventArgs e)
    {
        Witness.Stage(Request, "EndRequest");
        Witness.Stage(Request, Server.GetLastError() == null ? "LastError-null" : "LastError-set");
    }

    void Application_Error(object sender, EventArgs e)
    {
        var error = Server.GetLastError();
        Witness.Stage(Request, "ApplicationError:" + (error == null ? "null" : error.GetType().Name + ":" + error.Message));
    }

</script>
