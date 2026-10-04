<%@ Page Language="C#" %>
<script runat="server">

    static string Describe(System.Security.Principal.IPrincipal principal)
    {
        return principal == null
            ? "null"
            : principal.Identity.IsAuthenticated + "/" + principal.Identity.AuthenticationType;
    }

    void Page_Load(object sender, EventArgs e)
    {
        if (Request.QueryString["set"] == "1")
        {
            AppDomain.CurrentDomain.SetPrincipalPolicy(
                System.Security.Principal.PrincipalPolicy.WindowsPrincipal);
            Response.Write("policy-set|");
            return;
        }

        Response.Write("user=" + Describe(User) + "|");
        try
        {
            Response.Write("thread=" + Describe(System.Threading.Thread.CurrentPrincipal) + "|");
        }
        catch (Exception ex)
        {
            Response.Write("thread-threw:" + ex.GetType().Name + "|");
        }
    }

</script>
