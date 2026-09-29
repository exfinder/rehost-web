<%@ Application Language="C#" %>
<%@ Import Namespace="Rehost.Web.ScenarioProbes" %>
<script runat="server">

    // Staged only under the flag so the module-order scenario keeps its exact list.
    void Application_LogRequest(object sender, EventArgs e)
    {
        if (Request.QueryString["gax"] != null)
        {
            Witness.Stage(Request, "gax|LogRequest:" + Response.StatusCode);
        }
    }

</script>
