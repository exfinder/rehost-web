<%@ Page Language="C#" %>
<%@ Import Namespace="System.IO" %>
<%@ Import Namespace="System.Web" %>
<script runat="server">

    private string _saveReport = "";

    // Saving happens in the event, before anything renders: Report reads the posted stream to the
    // end through a StreamReader that closes it, and a closed HttpInputStream drops the content
    // it would have written.
    protected void Save_Click(object sender, EventArgs e)
    {
        Result.Text = "saved";
        _saveReport = SaveUpload();
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

    protected string SaveReport
    {
        get { return _saveReport; }
    }

    // The target is a directory the test owns; ?to= is absent for the read-only probes.
    private string SaveUpload()
    {
        string target = Request.QueryString["to"];
        if (string.IsNullOrEmpty(target))
        {
            return "";
        }

        HttpPostedFile file = Request.Files["Picked"];
        if (file == null)
        {
            return "no-file";
        }

        try
        {
            file.SaveAs(target);
            return "saved";
        }
        catch (Exception exception)
        {
            return "error:" + exception.GetType().FullName + ":" + exception.Message;
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
<p id="save-report"><%= SaveReport %></p>
</form>
</body>
</html>
