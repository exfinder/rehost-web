<%@ Page Language="C#" %>
<%@ Import Namespace="Rehost.WebForms.ScenarioProbes" %>
<script runat="server">

    void Page_Load(object sender, EventArgs e)
    {
        var token = Request.QueryString["wt"];
        Response.Write("before|");
        try
        {
            Response.End();
        }
        catch (System.Threading.ThreadAbortException)
        {
            WitnessJournal.Record("stage:" + token + ":tae-caught");
        }
    }

</script>
