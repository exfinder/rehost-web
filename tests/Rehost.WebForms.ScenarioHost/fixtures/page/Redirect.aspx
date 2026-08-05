<%@ Page Language="C#" %>
<%@ Import Namespace="Rehost.WebForms.ScenarioProbes" %>
<script runat="server">

    void Page_Load(object sender, EventArgs e)
    {
        var token = Request.QueryString["wt"];
        Response.Write("gone|");
        Response.Redirect("/Default.aspx?value=r");
        WitnessJournal.Record("stage:" + token + ":after-redirect");
    }

</script>
