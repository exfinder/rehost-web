<%@ Page Language="C#" %>
<script runat="server">
    protected void Page_Load(object sender, EventArgs e) {
        foreach (SiteMapNode n in SiteMap.RootNode.ChildNodes)
            Response.Write(n.Title + "=" + n.Url + "|key=" + n.Key + "\n");
        string[] lookups = { "/sitemap/A.aspx", "~/sitemap/B.aspx", "/sitemap/C.aspx", "C:\\outside\\D.aspx", "//srv/share/E.aspx" };
        foreach (string x in lookups) {
            SiteMapNode f = SiteMap.Provider.FindSiteMapNode(x);
            Response.Write("find(" + x + ")=" + (f == null ? "null" : f.Title) + "\n");
        }
    }
</script>
