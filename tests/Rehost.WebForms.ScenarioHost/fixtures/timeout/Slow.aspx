<%@ Page Language="C#" %>
<%@ Import Namespace="Rehost.WebForms.ScenarioProbes" %>
<script runat="server">

    void Page_Load(object sender, EventArgs e)
    {
        var token = Request.QueryString["wt"];
        var ms = Request.QueryString["ms"];
        System.Threading.Thread.Sleep(ms == null ? 0 : int.Parse(ms));
        if (token != null)
        {
            WitnessJournal.Record("stage:" + token + ":after-sleep-ran");
        }
        Response.Write("slow-done|");
    }

</script>
