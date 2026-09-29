<%@ Page Language="C#" %>
<%@ Import Namespace="Rehost.Web.ScenarioProbes" %>
<script runat="server">

    // captured during Load: Request is hidden while Unload runs
    string _token;

    void Page_Load(object sender, EventArgs e)
    {
        _token = Witness.Token(Request);
        Response.AppendHeader("X-End-Probe", "set");
        Response.Write("before-end|");
        try
        {
            Response.End();
            Witness.Stage(_token, "after-end-ran");
            Response.Write("after-end|");
        }
        finally
        {
            Witness.Stage(_token, "page-finally");
        }
    }

    void Page_Unload(object sender, EventArgs e)
    {
        Witness.Stage(_token, "page-unload");
    }

</script>
