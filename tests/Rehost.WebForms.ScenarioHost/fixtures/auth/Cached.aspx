<%@ Page Language="C#" %>
<%@ OutputCache Duration="30" VaryByParam="none" %>
<script runat="server">
protected void Page_Load(object sender, EventArgs e)
{
    Response.ContentType = "text/plain";
    Response.Write("stamp:" + Guid.NewGuid().ToString("N"));
}
</script>
