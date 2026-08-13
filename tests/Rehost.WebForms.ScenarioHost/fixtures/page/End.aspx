<%@ Page Language="C#" %>
<%@ Import Namespace="Rehost.WebForms.ScenarioProbes" %>
<script runat="server">

    // captured during Load: Request is hidden while Unload runs
    string _token;

    void Page_Load(object sender, EventArgs e)
    {
        _token = WitnessJournal.Token(Request);
        Response.AppendHeader("X-End-Probe", "set");
        Response.Write("before-end|");
        try
        {
            Response.End();
            WitnessJournal.Stage(_token, "after-end-ran");
            Response.Write("after-end|");
        }
        finally
        {
            WitnessJournal.Stage(_token, "page-finally");
        }
    }

    void Page_Unload(object sender, EventArgs e)
    {
        WitnessJournal.Stage(_token, "page-unload");
    }

</script>
