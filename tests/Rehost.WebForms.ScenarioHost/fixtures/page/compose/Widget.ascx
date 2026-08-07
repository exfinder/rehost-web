<%@ Control Language="C#" %>
<script runat="server">
    protected void Page_Load(object sender, EventArgs e) {
        WidgetLabel.Text = "widget-load-ran:" + Request.QueryString["q"];
    }
</script>
<span id="widget"><asp:Label ID="WidgetLabel" runat="server" /></span>
