<%@ Page Language="C#" %>
<%@ Import Namespace="System.Security.Claims" %>
<script runat="server">

    protected override void OnLoad(EventArgs e)
    {
        Response.ContentType = "text/plain";
        if (Request.QueryString["signin"] != null)
        {
            var identity = new ClaimsIdentity("ApplicationCookie");
            identity.AddClaim(new Claim(ClaimTypes.Name, "fixture-user"));
            Context.GetOwinContext().Authentication.SignIn(identity);
            Response.Write("signed-in");
        }
        else if (Request.QueryString["signout"] != null)
        {
            Context.GetOwinContext().Authentication.SignOut("ApplicationCookie");
            Response.Write("signed-out");
        }
        else
        {
            Response.Write("login");
        }
    }

</script>
