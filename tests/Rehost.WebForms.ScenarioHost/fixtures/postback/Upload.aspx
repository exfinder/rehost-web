<%@ Page Language="C#" %>
<%@ Import Namespace="System.IO" %>
<%@ Import Namespace="System.Web" %>
<script runat="server">

    protected void Save_Click(object sender, EventArgs e)
    {
        Result.Text = "saved";
    }

    protected string Report
    {
        get
        {
            HttpPostedFile file = Request.Files.Count == 0 ? null : Request.Files["Picked"];

            return "postback=" + IsPostBack
                + "|note=" + Request.Form["Note"]
                + "|files=" + Request.Files.Count
                + "|key=" + string.Join(",", Request.Files.AllKeys)
                + "|name=" + (file == null ? "" : file.FileName)
                + "|type=" + (file == null ? "" : file.ContentType)
                + "|length=" + (file == null ? -1 : file.ContentLength)
                + "|content=" + (file == null ? "" : ReadAll(file))
                + "|result=" + Result.Text;
        }
    }

    private static string ReadAll(HttpPostedFile file)
    {
        using (StreamReader reader = new StreamReader(file.InputStream, Encoding.UTF8))
        {
            return reader.ReadToEnd();
        }
    }

</script>
<html>
<head><title>Upload fixture</title></head>
<body>
<form id="upload" runat="server" enctype="multipart/form-data">
<asp:TextBox ID="Note" runat="server" />
<asp:FileUpload ID="Picked" runat="server" />
<asp:Button ID="Save" runat="server" Text="Save" OnClick="Save_Click" />
<asp:Label ID="Result" runat="server" />
<p id="upload-report"><%= Report %></p>
</form>
</body>
</html>
