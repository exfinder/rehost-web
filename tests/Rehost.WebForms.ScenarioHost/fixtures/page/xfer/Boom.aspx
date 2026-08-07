<%@ Page Language="C#" %>
<script runat="server">
    protected void Page_Load(object sender, EventArgs e) {
        throw new InvalidOperationException("boom-from-page");
    }
</script>
