<%@ Page Language="C#" %>
<script runat="server">
    protected void Page_Load(object sender, EventArgs e) {
        var last = Server.GetLastError();
        Response.Write("error-page[last=" + (last == null
            ? "null"
            : last.GetType().Name + ":" + (last.InnerException == null
                ? last.Message
                : last.InnerException.Message)) + "]");
    }
</script>
