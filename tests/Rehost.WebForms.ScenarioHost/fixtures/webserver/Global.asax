<%@ Application Language="C#" %>
<%@ Import Namespace="Rehost.WebForms.ScenarioProbes" %>
<script runat="server">

    void Application_BeginRequest(object sender, EventArgs e)
    {
        Witness.Stage(Request, "BeginRequest");
    }

</script>
