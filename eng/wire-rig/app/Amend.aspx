<%@ Page Language="C#" %>
<script runat="server">

    void Page_Load(object sender, EventArgs e)
    {
        var mode = Request.QueryString["mode"];
        if (mode == "nobuffer")
        {
            Response.BufferOutput = false;
        }
        Response.Write("amend-start|");
        if (mode == "end")
        {
            Response.End();
        }
        else if (mode == "redirect")
        {
            Response.Redirect("/Default.aspx?value=r");
        }
        else if (mode == "flush")
        {
            Response.Flush();
        }
        Response.Write("amend-tail|");
    }

</script>
