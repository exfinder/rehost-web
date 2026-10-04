<%@ Page Language="C#" %>
<%@ Import Namespace="Rehost.Web.ScenarioProbes" %>
<script runat="server">
    protected void Page_Load(object sender, EventArgs e) {
        Response.Write("parent-before|");
        switch (Request.QueryString["m"]) {
            case "t":
                Server.Transfer("Child.aspx");
                break;
            case "tq":
                Server.Transfer("Child.aspx?from=override");
                break;
            case "tf":
                Server.Transfer("Child.aspx", false);
                break;
            case "th": {
                var handler = System.Web.UI.PageParser.GetCompiledPageInstance(
                    "~/xfer/Child.aspx", Server.MapPath("Child.aspx"), Context);
                Server.Transfer(handler, false);
                break;
            }
            case "e":
                Server.Execute("Child.aspx");
                break;
            case "ew": {
                var sw = new System.IO.StringWriter();
                Server.Execute("Child.aspx", sw);
                Response.Write("captured[" + sw.ToString() + "]");
                break;
            }
            case "ef":
                Server.Execute("Child.aspx", null, false);
                break;
            case "eh": {
                var handler = System.Web.UI.PageParser.GetCompiledPageInstance(
                    "~/xfer/Child.aspx", Server.MapPath("Child.aspx"), Context);
                Server.Execute(handler, null, false);
                break;
            }
            case "ee":
                Server.Execute("EndChild.aspx");
                Witness.Stage(Request, "after-execute");
                break;
            case "es":
                Server.Execute("probe.css");
                break;
            case "tr":
                try {
                    Server.TransferRequest("Child.aspx");
                }
                catch (Exception ex) {
                    Response.Write("tr-caught:" + ex.GetType().Name + ":" + ex.Message + "|");
                }
                break;
        }
        Response.Write("parent-after|");
    }
</script>
