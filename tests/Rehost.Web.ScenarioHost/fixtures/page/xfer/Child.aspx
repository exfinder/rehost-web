<%@ Page Language="C#" %>
<script runat="server">
    protected void Page_Load(object sender, EventArgs e) {
        Response.Write("child[q=" + Request.QueryString
            + ";prev=" + (PreviousPage == null ? "null" : PreviousPage.AppRelativeVirtualPath)
            + ";cur=" + Request.CurrentExecutionFilePath
            + ";fp=" + Request.FilePath + "]");
    }
</script>
