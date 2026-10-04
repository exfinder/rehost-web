<%@ Page Language="C#" %>
<%@ Import Namespace="Rehost.Web.ScenarioProbes" %>
<script runat="server">

    void Page_Load(object sender, EventArgs e)
    {
        try
        {
            Response.Redirect("/Default.aspx?value=r", true);
        }
        catch (System.Threading.ThreadAbortException)
        {
            Witness.Stage(Request, "tae-arm");
        }
        catch (Exception)
        {
            Witness.Stage(Request, "general-arm");
            Response.StatusCode = 404;
            Response.AppendHeader("X-From-Catch", "1");
        }
    }

</script>
