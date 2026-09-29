<%@ Page Language="C#" %>
<%@ Import Namespace="Rehost.Web.ScenarioProbes" %>
<script runat="server">

    void Page_Load(object sender, EventArgs e)
    {
        Response.Write("gone|");
        Response.Redirect("/Default.aspx?value=r");
        Witness.Stage(Request, "after-redirect");
    }

</script>
