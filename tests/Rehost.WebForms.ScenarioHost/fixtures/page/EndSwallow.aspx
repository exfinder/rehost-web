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
        catch (Exception ex)
        {
            WitnessJournal.Record("stage:" + token + ":swallowed:" + ex.GetType().Name);
            Response.Write("swallowed-write|");
            try { Response.End(); }
            catch (Exception) { }
            WitnessJournal.Record("stage:" + token + ":after-second-end");
        }
        Response.Write("tail|");
    }

</script>
