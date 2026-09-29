<%@ Page Language="C#" %>
<%@ Import Namespace="Rehost.Web.ScenarioProbes" %>
<script runat="server">
    protected void Page_Load(object sender, EventArgs e) {
        if (Request.QueryString["filter"] != null) {
            Response.Write("page-output");
            Response.Filter = new UpperCaseFilter(Response.Filter);
        }
        if (Request.QueryString["nothrow"] != null) {
            return;
        }
        throw new InvalidOperationException("boom-from-page");
    }
</script>
