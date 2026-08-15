<%@ Page Language="C#" EnableSessionState="ReadOnly" %>
<script runat="server">

    void Page_Load(object sender, EventArgs e)
    {
        Response.ContentType = "text/plain";
        Response.Write("readonly=" + Session.IsReadOnly + "\n");
        Response.Write("id=" + Session.SessionID + "\n");
        Response.Write("v=" + (Session["v"] == null ? "null" : Session["v"].ToString()) + "\n");

        if (Request.QueryString["write"] != null)
        {
            try
            {
                Session["v"] = Request.QueryString["write"];
                Response.Write("write=ok\n");
            }
            catch (Exception error)
            {
                Response.Write("write=threw:" + error.GetType().Name + "\n");
            }
        }
    }

</script>
