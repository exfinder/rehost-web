<%@ Page Language="C#" %>
<%@ Import Namespace="Rehost.WebForms.ScenarioProbes" %>
<script runat="server">

    // captured during Load: Request is hidden while Unload runs
    string _token;

    void Page_Load(object sender, EventArgs e)
    {
        _token = Request.QueryString["wt"];
        Response.AppendHeader("X-End-Probe", "set");
        Response.Write("before-end|");
        try
        {
            Response.End();
            WitnessJournal.Record("stage:" + _token + ":after-end-ran");
            Response.Write("after-end|");
        }
        finally
        {
            WitnessJournal.Record("stage:" + _token + ":page-finally");
        }
    }

    void Page_Unload(object sender, EventArgs e)
    {
        WitnessJournal.Record("stage:" + _token + ":page-unload");
    }

</script>
