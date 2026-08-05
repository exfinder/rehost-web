<%@ Page Language="C#" MasterPageFile="~/Site.master" %>
<script runat="server">

    protected void Page_Load(object sender, EventArgs e)
    {
        Sample.SampleTrace.Record(Context, "Upload.Page_Load");
    }

    protected void Save_Click(object sender, EventArgs e)
    {
        Sample.SampleTrace.Record(Context, "Save_Click");

        if (!Picker.HasFile)
        {
            Result.Text = "Pick a file first.";
            return;
        }

        var posted = Picker.PostedFile;
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rehost-sample-uploads");
        System.IO.Directory.CreateDirectory(directory);
        var target = System.IO.Path.Combine(
            directory, System.IO.Path.GetFileName(posted.FileName));

        posted.SaveAs(target);

        string hash;
        using (var sha = System.Security.Cryptography.SHA256.Create())
        using (var stream = System.IO.File.OpenRead(target))
        {
            hash = Convert.ToHexString(sha.ComputeHash(stream));
        }

        Result.Text = "Saved <code>" + Server.HtmlEncode(posted.FileName) + "</code> ("
            + posted.ContentLength + " bytes, " + Server.HtmlEncode(posted.ContentType)
            + ") to <code>" + Server.HtmlEncode(target) + "</code><br />SHA-256 <code>"
            + hash + "</code>";
    }

</script>
<asp:Content ContentPlaceHolderID="Main" runat="server">

<h1>File upload</h1>
<p class="lede">
    <code>&lt;asp:FileUpload&gt;</code> switches the server form to multipart;
    <code>System.Web</code> parses the body — not the host — and
    <code>HttpPostedFile.SaveAs</code> writes it out, from memory or from the request's spill
    file, exactly as Framework decides it.
</p>

<div class="panel">
    <p>
        <asp:FileUpload ID="Picker" runat="server" />
        <asp:Button ID="Save" runat="server" Text="Upload and save" OnClick="Save_Click" />
    </p>
    <p><asp:Label ID="Result" runat="server" /></p>
</div>

</asp:Content>
