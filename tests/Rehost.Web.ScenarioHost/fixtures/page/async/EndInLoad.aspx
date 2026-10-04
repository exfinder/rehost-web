<%@ Page Language="C#" Async="true" %>
<%@ Import Namespace="Rehost.Web.ScenarioProbes" %>
<script runat="server">
    protected void Page_Load(object sender, EventArgs e) {
        Response.Write("before-end|");
        Response.End();
    }

    protected void Page_PreRenderComplete(object sender, EventArgs e) {
        Witness.Stage(Request, "prerender-complete-ran");
    }
</script>
