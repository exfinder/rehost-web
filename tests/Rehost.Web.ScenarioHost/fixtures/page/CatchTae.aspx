<%@ Page Language="C#" %>
<%@ Import Namespace="Rehost.Web.ScenarioProbes" %>
<script runat="server">

    void Page_Load(object sender, EventArgs e)
    {
        Response.Write("before|");
        try
        {
            Response.End();
        }
        catch (System.Threading.ThreadAbortException)
        {
            Witness.Stage(Request, "tae-caught");
        }
        Witness.Stage(Request, "after-catch");
        Response.Write("tail|");
    }

</script>
