<%@ Page Language="C#" %>
<%@ Import Namespace="Rehost.Web.ScenarioProbes" %>
<script runat="server">

    void Page_Load(object sender, EventArgs e)
    {
        var ms = Request.QueryString["ms"];
        System.Threading.Thread.Sleep(ms == null ? 0 : int.Parse(ms));
        Witness.Stage(Request, "after-sleep-ran");
        Response.Write("slow-done|");
    }

</script>
