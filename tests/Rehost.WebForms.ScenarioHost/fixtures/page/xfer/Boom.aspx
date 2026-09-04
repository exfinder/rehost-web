<%@ Page Language="C#" %>
<%@ Import Namespace="Rehost.WebForms.ScenarioProbes" %>
<script runat="server">
    protected void Page_Load(object sender, EventArgs e) {
        if (Request.QueryString["filter"] != null) {
            Response.Write("page-output");
            Response.Filter = new UpperCaseFilter(Response.Filter);
        }
        throw new InvalidOperationException("boom-from-page");
    }
</script>
